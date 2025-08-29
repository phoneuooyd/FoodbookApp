using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.Reflection;

namespace Foodbook.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext context)
        {
            var hasIngredients = await context.Ingredients.AnyAsync();
            var hasRecipes = await context.Recipes.AnyAsync();

            if (hasIngredients && hasRecipes)
                return;

            if (!hasIngredients)
            {
                await SeedIngredientsAsync(context);
            }

            if (hasRecipes)
                return;

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
        }

        public static async Task SeedIngredientsAsync(AppDbContext context)
        {
            if (await context.Ingredients.AnyAsync())
            {
                LogDebug("Ingredients already exist in database - skipping seed");
                return;
            }

            try
            {
                LogDebug("Starting ingredient seeding");
                var ingredients = await LoadPopularIngredientsAsync();

                foreach (var ingredient in ingredients)
                {
                    ingredient.RecipeId = null;
                }

                // ✅ OPTYMALIZACJA: Batch inserting dla lepszej wydajności
                LogDebug($"Adding {ingredients.Count} ingredients to database in batches");
                
                const int batchSize = 50;
                for (int i = 0; i < ingredients.Count; i += batchSize)
                {
                    var batch = ingredients.Skip(i).Take(batchSize);
                    context.Ingredients.AddRange(batch);
                    await context.SaveChangesAsync();
                    
                    LogDebug($"✅ Processed batch {i / batchSize + 1}/{(ingredients.Count + batchSize - 1) / batchSize}");
                    
                    // Krótka pauza dla UI responsiveness
                    await Task.Delay(10);
                }
                
                LogDebug($"✅ Successfully added {ingredients.Count} ingredients to database");
            }
            catch (Exception ex)
            {
                LogError($"❌ Error seeding ingredients: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                LogWarning("Continuing without seeded ingredients - user can add manually");
            }
        }

        /// <summary>
        /// Publiczna metoda do weryfikacji i aktualizacji danych składnika z OpenFoodFacts
        /// </summary>
        /// <param name="ingredient">Składnik do zaktualizowania</param>
        /// <returns>True jeśli dane zostały zaktualizowane, False w przeciwnym przypadku</returns>
        public static async Task<bool> UpdateIngredientWithOpenFoodFactsAsync(Ingredient ingredient)
        {
            // ✅ OPTYMALIZACJA: Timeout dla szybszej odpowiedzi
            using var httpClient = new HttpClient() 
            { 
                Timeout = TimeSpan.FromSeconds(10) // Skrócony timeout
            };
            
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
                dynamic? result = JsonConvert.DeserializeObject(content);
                
                if (result?.products == null || result.products.Count == 0) 
                {
                    LogWarning($"{ingredient.Name}: Product not found in OpenFoodFacts");
                    return false;
                }

                var product = result.products[0];
                var nutriments = product.nutriments;

                if (nutriments == null)
                {
                    LogWarning($"{ingredient.Name}: No nutritional data in found product");
                    return false;
                }

                double TryGet(dynamic src, string key)
                {
                    try
                    {
                        var value = src?[key];
                        if (value == null) return -1;
                        
                        if (double.TryParse(value.ToString(), out double parsed))
                            return parsed;
                        
                        return -1;
                    }
                    catch
                    {
                        return -1;
                    }
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
                
                // ✅ OPTYMALIZACJA: Większy próg dla unikania nadmiernych zmian
                var updateThreshold = 0.5; // Zmienione z 0.1 na 0.5
                
                if (newCalories >= 0 && Math.Abs(oldCalories - newCalories) > updateThreshold)
                {
                    ingredient.Calories = newCalories;
                    wasUpdated = true;
                }
                
                if (newProtein >= 0 && Math.Abs(oldProtein - newProtein) > updateThreshold)
                {
                    ingredient.Protein = newProtein;
                    wasUpdated = true;
                }
                
                if (newFat >= 0 && Math.Abs(oldFat - newFat) > updateThreshold)
                {
                    ingredient.Fat = newFat;
                    wasUpdated = true;
                }
                
                if (newCarbs >= 0 && Math.Abs(oldCarbs - newCarbs) > updateThreshold)
                {
                    ingredient.Carbs = newCarbs;
                    wasUpdated = true;
                }

                if (wasUpdated)
                {
                    LogDebug($"✅ {ingredient.Name} updated: kcal={ingredient.Calories}, P={ingredient.Protein}, F={ingredient.Fat}, C={ingredient.Carbs}");
                }
                else
                {
                    LogDebug($"ℹ {ingredient.Name}: OpenFoodFacts data identical to current");
                }
                
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
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;
            
            [JsonProperty("calories")]
            public double Calories { get; set; }
            
            [JsonProperty("protein")]
            public double Protein { get; set; }
            
            [JsonProperty("fat")]
            public double Fat { get; set; }
            
            [JsonProperty("carbs")]
            public double Carbs { get; set; }
            
            [JsonProperty("amount")]
            public double Amount { get; set; }
            
            [JsonProperty("unit")]
            public string Unit { get; set; } = string.Empty;
        }

        private static async Task<List<Ingredient>> LoadPopularIngredientsAsync()
        {
            string json;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("ingredients.json"));

                if (!string.IsNullOrEmpty(resourceName))
                {
                    LogDebug($"Found embedded resource: {resourceName}");
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        json = await reader.ReadToEndAsync();
                        LogDebug($"Loaded {json.Length} characters from embedded resource");
                    }
                    else
                    {
                        throw new FileNotFoundException("Embedded resource stream is null");
                    }
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
                    
                    string[] paths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "ingredients.json"),
                        Path.Combine(Environment.CurrentDirectory, "ingredients.json"),
                        Path.Combine(FileSystem.AppDataDirectory, "ingredients.json")
                    };

                    LogDebug("Trying file system paths");
                    json = null;
                    foreach (var path in paths)
                    {
                        LogDebug($"Checking path: {path}");
                        if (File.Exists(path))
                        {
                            json = await File.ReadAllTextAsync(path);
                            LogDebug($"Loaded {json.Length} characters from {path}");
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(json))
                    {
                        LogError("All loading methods failed! Creating fallback data");
                        return CreateFallbackIngredients();
                    }
                }
            }

            try
            {
                LogDebug("Deserializing JSON");
                var infos = JsonConvert.DeserializeObject<List<IngredientInfo>>(json) ?? new();
                LogDebug($"Deserialized {infos.Count} ingredient infos");

                var ingredients = infos.Select(i => new Ingredient
                {
                    Name = i.Name,
                    Quantity = i.Amount,
                    Unit = ParseUnit(i.Unit),
                    Calories = i.Calories,
                    Protein = i.Protein,
                    Fat = i.Fat,
                    Carbs = i.Carbs,
                    RecipeId = null
                }).ToList();

                LogDebug($"Created {ingredients.Count} ingredients");

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

        private static List<Ingredient> CreateFallbackIngredients()
        {
            LogDebug("Creating fallback ingredients");
            
            var fallbackIngredients = new List<Ingredient>
            {
                new Ingredient { Name = "Jajka", Quantity = 1, Unit = Unit.Piece, Calories = 155, Protein = 13.0, Fat = 11.0, Carbs = 1.0, RecipeId = null },
                new Ingredient { Name = "Mleko", Quantity = 100, Unit = Unit.Milliliter, Calories = 64, Protein = 3.4, Fat = 3.5, Carbs = 4.8, RecipeId = null },
                new Ingredient { Name = "Masło", Quantity = 100, Unit = Unit.Gram, Calories = 717, Protein = 0.9, Fat = 81.0, Carbs = 0.1, RecipeId = null },
                new Ingredient { Name = "Cukier", Quantity = 100, Unit = Unit.Gram, Calories = 387, Protein = 0.0, Fat = 0.0, Carbs = 100.0, RecipeId = null },
                new Ingredient { Name = "Sól", Quantity = 100, Unit = Unit.Gram, Calories = 0, Protein = 0.0, Fat = 0.0, Carbs = 0.0, RecipeId = null },
                new Ingredient { Name = "Mąka", Quantity = 100, Unit = Unit.Gram, Calories = 364, Protein = 10.0, Fat = 1.0, Carbs = 76.0, RecipeId = null },
                new Ingredient { Name = "Pierś z kurczaka", Quantity = 100, Unit = Unit.Gram, Calories = 200, Protein = 26.0, Fat = 10.0, Carbs = 0.0, RecipeId = null },
                new Ingredient { Name = "Oliwa z oliwek", Quantity = 100, Unit = Unit.Gram, Calories = 884, Protein = 0.0, Fat = 100.0, Carbs = 0.0, RecipeId = null },
                new Ingredient { Name = "Czosnek", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1.0, Fat = 0.0, Carbs = 5.0, RecipeId = null },
                new Ingredient { Name = "Cebula", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1.0, Fat = 0.0, Carbs = 5.0, RecipeId = null },
                new Ingredient { Name = "Pomidor", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1.0, Fat = 0.0, Carbs = 5.0, RecipeId = null },
                new Ingredient { Name = "Ziemniak", Quantity = 100, Unit = Unit.Gram, Calories = 77, Protein = 2.0, Fat = 0.1, Carbs = 17.0, RecipeId = null },
                new Ingredient { Name = "Marchew", Quantity = 100, Unit = Unit.Gram, Calories = 41, Protein = 0.9, Fat = 0.2, Carbs = 9.6, RecipeId = null },
                new Ingredient { Name = "Ser żółty", Quantity = 100, Unit = Unit.Gram, Calories = 113, Protein = 25.0, Fat = 28.0, Carbs = 1.3, RecipeId = null },
                new Ingredient { Name = "Ryż", Quantity = 100, Unit = Unit.Gram, Calories = 130, Protein = 2.7, Fat = 0.3, Carbs = 28.0, RecipeId = null }
            };

            LogDebug($"Created {fallbackIngredients.Count} fallback ingredients");
            return fallbackIngredients;
        }

        private static async Task UpdateWithOpenFoodFactsDataAsync(List<Ingredient> ingredients)
        {
            foreach (var ingredient in ingredients)
            {
                await UpdateIngredientWithOpenFoodFactsAsync(ingredient);
            }
        }

        private static Unit ParseUnit(string unitString)
        {
            return unitString?.ToLowerInvariant() switch
            {
                "gram" => Unit.Gram,
                "milliliter" => Unit.Milliliter,
                "piece" => Unit.Piece,
                _ => Unit.Piece
            };
        }

        /// <summary>
        /// Metoda diagnostyczna do testowania ładowania składników z JSON
        /// </summary>
        /// <returns>Liczba załadowanych składników lub -1 w przypadku błędu</returns>
        public static async Task<int> TestLoadIngredientsAsync()
        {
            try
            {
                LogDebug("DIAGNOSTIC: Testing ingredient loading");
                var ingredients = await LoadPopularIngredientsAsync();
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

        // Centralized logging methods
        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[SeedData] {message}");
        }

        private static void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[SeedData] WARNING: {message}");
        }

        private static void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[SeedData] ERROR: {message}");
        }
    }
}
