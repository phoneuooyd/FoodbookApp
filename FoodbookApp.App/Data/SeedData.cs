using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.Reflection;
using System.Globalization;
using Newtonsoft.Json.Linq;
using FoodbookApp.Interfaces;

namespace Foodbook.Data
{
    public static class SeedData
    {
        // Concurrency guards
        private static readonly object _seedLock = new();
        private static bool _seedStarted = false; // set when seeding begins
        private static bool _seedCompleted = false; // set when seeding (or a decision to skip) finishes

        public static async Task InitializeAsync(AppDbContext context)
        {
            // Pobierz serwis preferencji (może być null bardzo wcześnie)
            var preferencesService = Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services?.GetService<IPreferencesService>();
            bool isFirstLaunch = preferencesService?.IsFirstLaunch() ?? true; // Domyślnie true jeśli brak serwisu

            var hasIngredients = await context.Ingredients.AnyAsync();
            var hasRecipes = await context.Recipes.AnyAsync();

            // Jeżeli pierwszy start i nie ma składników – NIE seedujemy tutaj.
            // Seeding wykona SetupWizard po wyborze języka (przekaże go jawnie), co eliminuje problem języka domyślnego.
            if (isFirstLaunch && !hasIngredients)
            {
                LogDebug("First launch detected – deferring ingredient seeding to SetupWizard (language will be chosen by user)");
            }
            else if (!hasIngredients)
            {
                // Kolejne uruchomienie (setup zakończony) – możemy seeding wykonać jeśli z jakiegoś powodu brak danych.
                var savedLang = preferencesService?.GetSavedLanguage();
                LogDebug($"No ingredients found after initial setup. Seeding now with saved language: {savedLang ?? "(null)"}");
                await SeedIngredientsAsync(context, savedLang);
            }

            // Jeśli mamy już składniki i przepisy – nic dalej.
            if (hasIngredients && hasRecipes)
                return;

            if (hasRecipes)
                return;

#if DEBUG
            try
            {
                var isFirstLaunchForRecipe = isFirstLaunch; // użyj tej samej informacji
                if (isFirstLaunchForRecipe)
                {
                    var recipe = new Recipe
                    {
                        Name = "Prosta sałatka",
                        Description = "Przykładowa sałatka.",
                        Calories = 150,
                        Protein = 3,
                        Fat = 7,
                        Carbs = 18,
                        Ingredients = new List<Ingredient>
                        {
                            new Ingredient { Name = "Sałata", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1, Fat = 0, Carbs = 5 },
                            new Ingredient { Name = "Pomidor", Quantity = 50, Unit = Unit.Gram, Calories = 25, Protein = 1, Fat = 0, Carbs = 5 }
                        }
                    };

                    context.Recipes.Add(recipe);
                    await context.SaveChangesAsync();
                    LogDebug("DEBUG: Added example recipe 'Prosta sałatka' (first launch)");
                }
                else
                {
                    LogDebug("DEBUG: Skipping example recipe seeding (not first launch)");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"DEBUG: Failed to conditionally seed example recipe: {ex.Message}");
            }
#else
            LogDebug("RELEASE: Skipping example recipe seeding");
#endif
        }

        /// <summary>
        /// Seed bazowych składników – opcjonalnie z wymuszonym językiem (np. z kreatora).
        /// Zapobiega duplikatom przy równoległych wywołaniach (lock + transakcja + re-check) i usuwa wcześniejsze duplikaty.
        /// </summary>
        public static async Task SeedIngredientsAsync(AppDbContext context, string? languageOverride = null)
        {
            if (_seedCompleted)
            {
                LogDebug("SeedIngredientsAsync skipped - already completed previously");
                return;
            }

            lock (_seedLock)
            {
                if (_seedCompleted)
                {
                    LogDebug("SeedIngredientsAsync skipped (inside lock) - already completed");
                    return;
                }
                if (_seedStarted)
                {
                    LogDebug("SeedIngredientsAsync: another seeding already in progress - skipping");
                    return; // Drugi równoległy wątek odpuści
                }
                _seedStarted = true; // Ten wątek będzie seederem
            }

            try
            {
                using var trx = await context.Database.BeginTransactionAsync();

                int removed = await RemoveDuplicateIngredientsAsync(context);
                if (removed > 0)
                {
                    LogWarning($"Removed {removed} duplicate ingredient rows before seeding");
                }

                bool anyIngredients = await context.Ingredients.AnyAsync();
                if (anyIngredients)
                {
                    LogDebug("Ingredients already exist after re-check in transaction - skipping initial seed");
                    await trx.CommitAsync();
                    return;
                }

                LogDebug($"Starting ingredient seeding (language override: {languageOverride ?? "<null>"})");
                var ingredients = await LoadPopularIngredientsAsync(languageOverride);
                foreach (var ingredient in ingredients)
                {
                    ingredient.RecipeId = null;
                }

                const int batchSize = 100;
                for (int i = 0; i < ingredients.Count; i += batchSize)
                {
                    var batch = ingredients.Skip(i).Take(batchSize);
                    context.Ingredients.AddRange(batch);
                    await context.SaveChangesAsync();
                    LogDebug($"Inserted batch {i / batchSize + 1}/{(ingredients.Count + batchSize - 1) / batchSize}");
                }

                LogDebug("About to commit transaction");
                await trx.CommitAsync();
                LogDebug($"✅ Successfully seeded {ingredients.Count} ingredients (transaction committed)");
            }
            catch (Exception ex)
            {
                LogError($"❌ SeedIngredientsAsync failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                _seedCompleted = true; // Koniec procesu – nawet przy błędzie nie powtarzamy automatycznie
            }
        }

        public static async Task<bool> UpdateIngredientWithOpenFoodFactsAsync(Ingredient ingredient)
        {
            using var httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
            var url = $"https://world.openfoodfacts.org/cgi/search.pl?search_terms={Uri.EscapeDataString(ingredient.Name)}&search_simple=1&json=1";
            try
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    LogError($"{ingredient.Name}: HTTP error {response.StatusCode}");
                    return false;
                }
                var content = await response.Content.ReadAsStringAsync();

                // Parse with strongly-typed JObject to avoid nullability warnings
                var root = JsonConvert.DeserializeObject<JObject>(content);
                var products = root?["products"] as JArray;
                if (products == null || products.Count == 0)
                {
                    LogWarning($"{ingredient.Name}: Product not found in OpenFoodFacts");
                    return false;
                }

                var product = products[0] as JObject;
                var nutriments = product?["nutriments"] as JObject;
                if (nutriments == null)
                {
                    LogWarning($"{ingredient.Name}: No nutritional data in found product");
                    return false;
                }

                double TryGet(JObject src, string key)
                {
                    var token = src[key];
                    if (token == null) return -1;
                    return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : -1;
                }

                var oldCalories = ingredient.Calories;
                var oldProtein = ingredient.Protein;
                var oldFat = ingredient.Fat;
                var oldCarbs = ingredient.Carbs;
                var newCalories = TryGet(nutriments, "energy-kcal_100g");
                var newProtein = TryGet(nutriments, "proteins_100g");
                var newFat = TryGet(nutriments, "fat_100g");
                var newCarbs = TryGet(nutriments, "carbohydrates_100g");
                bool hasValidData = newCalories >= 0 || newProtein >= 0 || newFat >= 0 || newCarbs >= 0;
                if (!hasValidData)
                {
                    LogWarning($"{ingredient.Name}: Product found but no valid nutritional data");
                    return false;
                }
                bool wasUpdated = false;
                var updateThreshold = 0.5;
                if (newCalories >= 0 && Math.Abs(oldCalories - newCalories) > updateThreshold) { ingredient.Calories = newCalories; wasUpdated = true; }
                if (newProtein >= 0 && Math.Abs(oldProtein - newProtein) > updateThreshold) { ingredient.Protein = newProtein; wasUpdated = true; }
                if (newFat >= 0 && Math.Abs(oldFat - newFat) > updateThreshold) { ingredient.Fat = newFat; wasUpdated = true; }
                if (newCarbs >= 0 && Math.Abs(oldCarbs - newCarbs) > updateThreshold) { ingredient.Carbs = newCarbs; wasUpdated = true; }
                LogDebug(wasUpdated ? $"✅ {ingredient.Name} updated: kcal={ingredient.Calories}, P={ingredient.Protein}, F={ingredient.Fat}, C={ingredient.Carbs}" : $"ℹ {ingredient.Name}: OpenFoodFacts data identical to current");
                return wasUpdated;
            }
            catch (Exception ex)
            {
                LogError($"❌ {ingredient.Name}: {ex.Message}");
                return false;
            }
        }

        private class IngredientInfo
        {
            [JsonProperty("name_pl")] public string NamePl { get; set; } = string.Empty;
            [JsonProperty("name_en")] public string NameEn { get; set; } = string.Empty;
            [JsonProperty("name_de")] public string NameDe { get; set; } = string.Empty;
            [JsonProperty("name_es")] public string NameEs { get; set; } = string.Empty;
            [JsonProperty("name_fr")] public string NameFr { get; set; } = string.Empty;
            [JsonProperty("name_kr")] public string NameKr { get; set; } = string.Empty;
            [JsonProperty("calories")] public double Calories { get; set; }
            [JsonProperty("protein")] public double Protein { get; set; }
            [JsonProperty("fat")] public double Fat { get; set; }
            [JsonProperty("carbs")] public double Carbs { get; set; }
            [JsonProperty("amount")] public double Amount { get; set; }
            [JsonProperty("unit")] public string Unit { get; set; } = string.Empty;
            [JsonProperty("unit_weight")] public double UnitWeight { get; set; } = 1.0;
        }

        private static async Task<List<Ingredient>> LoadPopularIngredientsAsync(string? languageOverride)
        {
            string json;
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceNames = assembly.GetManifestResourceNames();
                LogDebug($"Manifest resources: {string.Join(", ", resourceNames)}");
                var resourceName = resourceNames.FirstOrDefault(name => name.EndsWith("ingredients.json"));
                LogDebug($"Trying embedded resource: {resourceName ?? "null"}");
                if (!string.IsNullOrEmpty(resourceName))
                {
                    LogDebug($"Found embedded resource: {resourceName}");
                    using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException("Embedded resource stream is null");
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                    LogDebug($"Loaded {json.Length} characters from embedded resource");
                }
                else
                {
                    LogWarning("No embedded resource found, trying app package");
                    throw new FileNotFoundException("No embedded resource found");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"Embedded resource failed: {ex.Message}");
                try
                {
                    LogDebug("Trying to load from app package");
                    using var stream = await FileSystem.OpenAppPackageFileAsync("ingredients.json");
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                    LogDebug($"Loaded {json.Length} characters from app package");
                }
                catch (Exception ex2)
                {
                    LogWarning($"App package failed: {ex2.Message}");
                    string[] paths =
                    {
                        Path.Combine(AppContext.BaseDirectory, "ingredients.json"),
                        Path.Combine(Environment.CurrentDirectory, "ingredients.json"),
                        Path.Combine(FileSystem.AppDataDirectory, "ingredients.json")
                    };
                    LogDebug("Trying file system paths");
                    json = string.Empty; // avoid assigning null to non-nullable
                    foreach (var path in paths)
                    {
                        LogDebug($"Checking path: {path} - Exists: {File.Exists(path)}");
                        if (File.Exists(path)) { json = await File.ReadAllTextAsync(path); LogDebug($"Loaded {json.Length} characters from {path}"); break; }
                    }
                    if (string.IsNullOrEmpty(json)) { LogError("All loading methods failed! Creating fallback data"); return CreateFallbackIngredients(); }
                }
            }

            try
            {
                LogDebug("Deserializing JSON");
                var infos = JsonConvert.DeserializeObject<List<IngredientInfo>>(json) ?? new();
                LogDebug($"Deserialized {infos.Count} ingredient infos");
                if (infos.Count > 0)
                {
                    var firstInfo = infos.First();
                    LogDebug($"First info: NamePl={firstInfo.NamePl}, Unit={firstInfo.Unit}, Calories={firstInfo.Calories}, UnitWeight={firstInfo.UnitWeight}");
                }

                var lang = NormalizeLanguage(languageOverride) ?? NormalizeLanguage(Preferences.Get("SelectedCulture", string.Empty)) ?? NormalizeLanguage(CultureInfo.CurrentUICulture.Name) ?? "en";
                var isPolish = lang.StartsWith("pl", StringComparison.OrdinalIgnoreCase);
                var isGerman = lang.StartsWith("de", StringComparison.OrdinalIgnoreCase);
                var isSpanish = lang.StartsWith("es", StringComparison.OrdinalIgnoreCase);
                var isFrench = lang.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
                var isKorean = lang.StartsWith("kr", StringComparison.OrdinalIgnoreCase);
                LogDebug($"Ingredient language resolved to: {lang} (pl={isPolish}, de={isGerman}, es={isSpanish}, fr={isFrench}, kr={isKorean})");

                var ingredients = infos.Select(i => new Ingredient
                {
                    Name = isPolish ? (string.IsNullOrWhiteSpace(i.NamePl) ? (string.IsNullOrWhiteSpace(i.NameEn) ? "Unknown ingredient" : i.NameEn) : i.NamePl)
                                     : isGerman ? (string.IsNullOrWhiteSpace(i.NameDe) ? (string.IsNullOrWhiteSpace(i.NameEn) ? (string.IsNullOrWhiteSpace(i.NamePl) ? "Unknown ingredient" : i.NamePl) : i.NameEn) : i.NameDe)
                                     : isSpanish ? (string.IsNullOrWhiteSpace(i.NameEs) ? (string.IsNullOrWhiteSpace(i.NameEn) ? (string.IsNullOrWhiteSpace(i.NamePl) ? "Unknown ingredient" : i.NamePl) : i.NameEn) : i.NameEs)
                                     : isFrench ? (string.IsNullOrWhiteSpace(i.NameFr) ? (string.IsNullOrWhiteSpace(i.NameEn) ? (string.IsNullOrWhiteSpace(i.NamePl) ? "Unknown ingredient" : i.NamePl) : i.NameEn) : i.NameFr)
                                     : isKorean ? (string.IsNullOrWhiteSpace(i.NameKr) ? (string.IsNullOrWhiteSpace(i.NameEn) ? (string.IsNullOrWhiteSpace(i.NamePl) ? "Unknown ingredient" : i.NamePl) : i.NameEn) : i.NameKr)
                                     : (string.IsNullOrWhiteSpace(i.NameEn) ? (string.IsNullOrWhiteSpace(i.NamePl) ? "Unknown ingredient" : i.NamePl) : i.NameEn),
                    Quantity = i.Amount,
                    Unit = ParseUnit(i.Unit),
                    Calories = i.Unit == "Piece" && i.UnitWeight > 0 ? i.Calories * (100.0 / i.UnitWeight) : i.Calories,
                    Protein = i.Unit == "Piece" && i.UnitWeight > 0 ? i.Protein * (100.0 / i.UnitWeight) : i.Protein,
                    Fat = i.Unit == "Piece" && i.UnitWeight > 0 ? i.Fat * (100.0 / i.UnitWeight) : i.Fat,
                    Carbs = i.Unit == "Piece" && i.UnitWeight > 0 ? i.Carbs * (100.0 / i.UnitWeight) : i.Carbs,
                    UnitWeight = i.UnitWeight,
                    RecipeId = null
                }).ToList();

                LogDebug($"Created {ingredients.Count} ingredients");
                if (ingredients.Count > 0)
                {
                    var firstIng = ingredients.First();
                    LogDebug($"First ingredient: Name={firstIng.Name}, Unit={firstIng.Unit}, Calories={firstIng.Calories}, UnitWeight={firstIng.UnitWeight}");
                }

                if (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS)
                {
                    LogDebug("Mobile device detected - skipping OpenFoodFacts updates for faster seeding");
                }
                else
                {
                    await UpdateWithOpenFoodFactsDataAsync(ingredients);
                }
                return ingredients;
            }
            catch (JsonException ex)
            {
                LogError($"JSON deserialization failed: {ex.Message}");
                LogDebug("Creating fallback ingredients due to JSON error");
                return CreateFallbackIngredients();
            }
        }

        private static string? NormalizeLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            code = code.Trim();
            if (code.StartsWith("pl", StringComparison.OrdinalIgnoreCase)) return "pl-PL";
            if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en";
            if (code.StartsWith("de", StringComparison.OrdinalIgnoreCase)) return "de";
            if (code.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es";
            if (code.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return "fr";
            if (code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) || code.StartsWith("kr", StringComparison.OrdinalIgnoreCase)) return "kr";
            return null; // fallback wyżej
        }

        private static List<Ingredient> CreateFallbackIngredients() => new()
        {
            new Ingredient { Name = "Jajka", Quantity = 1, Unit = Unit.Piece, Calories = 155, Protein = 13.0, Fat = 11.0, Carbs = 1.0 },
            new Ingredient { Name = "Mleko", Quantity = 100, Unit = Unit.Milliliter, Calories = 64, Protein = 3.4, Fat = 3.5, Carbs = 4.8 },
            new Ingredient { Name = "Masło", Quantity = 100, Unit = Unit.Gram, Calories = 717, Protein = 0.9, Fat = 81.0, Carbs = 0.1 },
            new Ingredient { Name = "Cukier", Quantity = 100, Unit = Unit.Gram, Calories = 387, Protein = 0.0, Fat = 0.0, Carbs = 100.0 },
            new Ingredient { Name = "Sól", Quantity = 100, Unit = Unit.Gram, Calories = 0, Protein = 0.0, Fat = 0.0, Carbs = 0.0 },
            new Ingredient { Name = "Mąka", Quantity = 100, Unit = Unit.Gram, Calories = 364, Protein = 10.0, Fat = 1.0, Carbs = 76.0 }
        };

        private static async Task UpdateWithOpenFoodFactsDataAsync(List<Ingredient> ingredients)
        {
            foreach (var ingredient in ingredients)
            {
                await UpdateIngredientWithOpenFoodFactsAsync(ingredient);
            }
        }

        private static Unit ParseUnit(string unitString) => unitString?.ToLowerInvariant() switch
        {
            "gram" => Unit.Gram,
            "milliliter" => Unit.Milliliter,
            "piece" => Unit.Piece,
            _ => Unit.Piece
        };

        public static async Task<int> TestLoadIngredientsAsync()
        {
            try
            {
                LogDebug("DIAGNOSTIC: Testing ingredient loading");
                var ingredients = await LoadPopularIngredientsAsync(null);
                LogDebug($"DIAGNOSTIC: Successfully loaded {ingredients.Count} ingredients");
                if (ingredients.Count > 0)
                {
                    var firstIngredient = ingredients.First();
                    LogDebug($"DIAGNOSTIC: First ingredient: {firstIngredient.Name}, Calories: {firstIngredient.Calories}, Unit: {firstIngredient.Unit}");
                }
                return ingredients.Count;
            }
            catch (Exception ex)
            {
                LogError($"DIAGNOSTIC: Error loading ingredients: {ex.Message}");
                return -1;
            }
        }

        private static async Task<int> RemoveDuplicateIngredientsAsync(AppDbContext context)
        {
            try
            {
                var groups = await context.Ingredients
                    .GroupBy(i => new { i.Name, i.Calories, i.Protein, i.Fat, i.Carbs })
                    .Select(g => new { g.Key, Ids = g.Select(x => x.Id).OrderBy(id => id).ToList(), Count = g.Count() })
                    .Where(g => g.Count > 1)
                    .ToListAsync();

                int removed = 0;
                foreach (var g in groups)
                {
                    var toRemoveIds = g.Ids.Skip(1).ToList();
                    foreach (var id in toRemoveIds)
                    {
                        var entity = new Ingredient { Id = id };
                        context.Attach(entity);
                        context.Remove(entity);
                        removed++;
                    }
                }
                if (removed > 0)
                {
                    await context.SaveChangesAsync();
                }
                return removed;
            }
            catch (Exception ex)
            {
                LogWarning($"RemoveDuplicateIngredientsAsync failed: {ex.Message}");
                return 0;
            }
        }

        private static void LogDebug(string message) => System.Diagnostics.Debug.WriteLine($"[SeedData] {message}");
        private static void LogWarning(string message) => System.Diagnostics.Debug.WriteLine($"[SeedData] WARNING: {message}");
        private static void LogError(string message) => System.Diagnostics.Debug.WriteLine($"[SeedData] ERROR: {message}");
    }
}
