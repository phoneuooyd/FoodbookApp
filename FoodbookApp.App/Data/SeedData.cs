using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
using System.Reflection;

namespace Foodbook.Data
{
    public static class SeedData
    {
        /// <summary>
        /// Inicjalizuje bazę danych z transakcją dla zapewnienia integralności danych
        /// </summary>
        /// <param name="context">Kontekst bazy danych</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task InitializeAsync(AppDbContext context)
        {
            // Rozpocznij transakcję dla całej operacji inicjalizacji
            using var transaction = await context.Database.BeginTransactionAsync();
            
            try
            {
                LogDebug("Starting database initialization with transaction");
                
                var hasIngredients = await context.Ingredients.AnyAsync();
                var hasRecipes = await context.Recipes.AnyAsync();

                LogDebug($"Database state: hasIngredients={hasIngredients}, hasRecipes={hasRecipes}");

                // Jeśli wszystko już istnieje, zakończ bez zmian
                if (hasIngredients && hasRecipes)
                {
                    LogDebug("Database already fully populated - rolling back transaction");
                    await transaction.RollbackAsync();
                    return;
                }

                bool dataSeeded = false;

                // Seed ingredients jeśli nie istnieją
                if (!hasIngredients)
                {
                    LogDebug("Seeding ingredients within transaction");
                    await SeedIngredientsInTransactionAsync(context);
                    dataSeeded = true;
                }

                // Seed recipes jeśli nie istnieją
                if (!hasRecipes)
                {
                    LogDebug("Seeding sample recipe within transaction");
                    await SeedSampleRecipeInTransactionAsync(context);
                    dataSeeded = true;
                }

                if (dataSeeded)
                {
                    // Commit transakcji tylko jeśli coś zostało dodane
                    await transaction.CommitAsync();
                    LogDebug("Database initialization transaction committed successfully");
                }
                else
                {
                    // Rollback jeśli nic nie zostało dodane
                    await transaction.RollbackAsync();
                    LogDebug("No data seeded - transaction rolled back");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error during database initialization: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                
                try
                {
                    await transaction.RollbackAsync();
                    LogDebug("Transaction rolled back due to error");
                }
                catch (Exception rollbackEx)
                {
                    LogError($"Error during transaction rollback: {rollbackEx.Message}");
                }
                
                throw; // Re-throw original exception
            }
        }

        /// <summary>
        /// Seeduje składniki w ramach istniejącej transakcji
        /// </summary>
        /// <param name="context">Kontekst bazy danych z aktywną transakcją</param>
        public static async Task SeedIngredientsAsync(AppDbContext context)
        {
            // Sprawdź czy jest aktywna transakcja
            var currentTransaction = context.Database.CurrentTransaction;
            
            if (currentTransaction != null)
            {
                // Jeśli jest aktywna transakcja, użyj jej
                LogDebug("Using existing transaction for ingredient seeding");
                await SeedIngredientsInTransactionAsync(context);
            }
            else
            {
                // Jeśli nie ma transakcji, utwórz nową
                using var transaction = await context.Database.BeginTransactionAsync();
                
                try
                {
                    LogDebug("Creating new transaction for ingredient seeding");
                    await SeedIngredientsInTransactionAsync(context);
                    await transaction.CommitAsync();
                    LogDebug("Ingredient seeding transaction committed successfully");
                }
                catch (Exception ex)
                {
                    LogError($"Error seeding ingredients: {ex.Message}");
                    LogError($"Stack trace: {ex.StackTrace}");
                    
                    try
                    {
                        await transaction.RollbackAsync();
                        LogDebug("Ingredient seeding transaction rolled back due to error");
                    }
                    catch (Exception rollbackEx)
                    {
                        LogError($"Error during transaction rollback: {rollbackEx.Message}");
                    }
                    
                    LogWarning("Continuing without seeded ingredients - user can add manually");
                    throw; // Re-throw to allow caller to handle the error
                }
            }
        }

        /// <summary>
        /// Wewnętrzna metoda seedowania składników bez zarządzania transakcją
        /// </summary>
        /// <param name="context">Kontekst bazy danych</param>
        private static async Task SeedIngredientsInTransactionAsync(AppDbContext context)
        {
            LogDebug("SeedIngredientsInTransactionAsync started");
            
            // Double-check w ramach transakcji
            if (await context.Ingredients.AnyAsync())
            {
                LogDebug("Ingredients already exist in database - skipping seed");
                return;
            }

            LogDebug("Starting ingredient seeding");
            var ingredients = await LoadPopularIngredientsAsync();
            LogDebug($"Loaded {ingredients.Count} ingredients from data source");

            if (ingredients.Count == 0)
            {
                LogWarning("No ingredients loaded from data source!");
                return;
            }

            // Walidacja danych przed dodaniem
            var validIngredients = ValidateIngredients(ingredients);
            LogDebug($"Validated {validIngredients.Count} out of {ingredients.Count} ingredients");

            if (validIngredients.Count == 0)
            {
                LogError("No valid ingredients to seed!");
                throw new InvalidOperationException("No valid ingredients available for seeding");
            }

            // Ustawienie RecipeId na null dla składników głównych
            foreach (var ingredient in validIngredients)
            {
                ingredient.RecipeId = null;
            }

            LogDebug($"Adding {validIngredients.Count} ingredients to database");
            
            // Batch dodawanie z kontrolą pamięci
            const int batchSize = 100;
            var totalAdded = 0;
            
            for (int i = 0; i < validIngredients.Count; i += batchSize)
            {
                var batch = validIngredients.Skip(i).Take(batchSize).ToList();
                context.Ingredients.AddRange(batch);
                
                // Zapisz batch do bazy
                var savedInBatch = await context.SaveChangesAsync();
                totalAdded += savedInBatch;
                
                LogDebug($"Saved batch {(i / batchSize) + 1}: {savedInBatch} ingredients");
                
                // Wyczyść change tracker dla zarządzania pamięcią
                context.ChangeTracker.Clear();
            }

            LogDebug($"Successfully added {totalAdded} ingredients to database");
            
            // Weryfikacja końcowa
            var verifyCount = await context.Ingredients.CountAsync();
            LogDebug($"Verification: {verifyCount} ingredients now in database");
            
            if (verifyCount < validIngredients.Count)
            {
                LogWarning($"Expected {validIngredients.Count} ingredients but found {verifyCount} in database");
            }
        }

        /// <summary>
        /// Seeduje przykładowy przepis w ramach transakcji
        /// </summary>
        /// <param name="context">Kontekst bazy danych</param>
        private static async Task SeedSampleRecipeInTransactionAsync(AppDbContext context)
        {
            LogDebug("SeedSampleRecipeInTransactionAsync started");
            
            // Double-check w ramach transakcji
            if (await context.Recipes.AnyAsync())
            {
                LogDebug("Recipes already exist in database - skipping seed");
                return;
            }

            var recipe = new Recipe
            {
                Name = "Prosta sałatka",
                Description = "Przykładowa sałatka z podstawowymi składnikami.",
                Calories = 150,
                Protein = 3,
                Fat = 7,
                Carbs = 18,
                IloscPorcji = 2,
                Ingredients = new List<Ingredient>
                {
                    new Ingredient 
                    { 
                        Name = "Sałata", 
                        Quantity = 100, 
                        Unit = Unit.Gram, 
                        Calories = 25, 
                        Protein = 1, 
                        Fat = 0, 
                        Carbs = 5 
                    },
                    new Ingredient 
                    { 
                        Name = "Pomidor", 
                        Quantity = 50, 
                        Unit = Unit.Gram, 
                        Calories = 25, 
                        Protein = 1, 
                        Fat = 0, 
                        Carbs = 5 
                    }
                }
            };

            // Walidacja przepisu przed dodaniem
            if (ValidateRecipe(recipe))
            {
                context.Recipes.Add(recipe);
                var savedCount = await context.SaveChangesAsync();
                LogDebug($"Successfully added sample recipe with {savedCount} changes saved");
            }
            else
            {
                LogError("Sample recipe validation failed");
                throw new InvalidOperationException("Sample recipe data is invalid");
            }
        }

        /// <summary>
        /// Waliduje listę składników przed dodaniem do bazy
        /// </summary>
        /// <param name="ingredients">Lista składników do walidacji</param>
        /// <returns>Lista poprawnych składników</returns>
        private static List<Ingredient> ValidateIngredients(List<Ingredient> ingredients)
        {
            var validIngredients = new List<Ingredient>();
            
            foreach (var ingredient in ingredients)
            {
                if (ValidateIngredient(ingredient))
                {
                    validIngredients.Add(ingredient);
                }
                else
                {
                    LogWarning($"Invalid ingredient skipped: {ingredient?.Name ?? "NULL"}");
                }
            }
            
            return validIngredients;
        }

        /// <summary>
        /// Waliduje pojedynczy składnik
        /// </summary>
        /// <param name="ingredient">Składnik do walidacji</param>
        /// <returns>True jeśli składnik jest poprawny</returns>
        private static bool ValidateIngredient(Ingredient? ingredient)
        {
            if (ingredient == null)
            {
                LogWarning("Ingredient is null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(ingredient.Name))
            {
                LogWarning("Ingredient name is empty or null");
                return false;
            }

            if (ingredient.Quantity <= 0)
            {
                LogWarning($"Ingredient {ingredient.Name} has invalid quantity: {ingredient.Quantity}");
                return false;
            }

            if (ingredient.Calories < 0 || ingredient.Protein < 0 || ingredient.Fat < 0 || ingredient.Carbs < 0)
            {
                LogWarning($"Ingredient {ingredient.Name} has negative nutritional values");
                return false;
            }

            if (ingredient.Name.Length > 200) // Assumption about max name length
            {
                LogWarning($"Ingredient name too long: {ingredient.Name.Length} characters");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Waliduje przepis przed dodaniem do bazy
        /// </summary>
        /// <param name="recipe">Przepis do walidacji</param>
        /// <returns>True jeśli przepis jest poprawny</returns>
        private static bool ValidateRecipe(Recipe? recipe)
        {
            if (recipe == null)
            {
                LogWarning("Recipe is null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(recipe.Name))
            {
                LogWarning("Recipe name is empty or null");
                return false;
            }

            if (recipe.IloscPorcji <= 0)
            {
                LogWarning($"Recipe {recipe.Name} has invalid portion count: {recipe.IloscPorcji}");
                return false;
            }

            if (recipe.Calories < 0 || recipe.Protein < 0 || recipe.Fat < 0 || recipe.Carbs < 0)
            {
                LogWarning($"Recipe {recipe.Name} has negative nutritional values");
                return false;
            }

            // Walidacja składników przepisu
            if (recipe.Ingredients != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!ValidateIngredient(ingredient))
                    {
                        LogWarning($"Recipe {recipe.Name} contains invalid ingredient");
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Metoda diagnostyczna do sprawdzania stanu transakcji
        /// </summary>
        /// <param name="context">Kontekst bazy danych</param>
        /// <returns>Informacje o stanie transakcji</returns>
        public static string GetTransactionInfo(AppDbContext context)
        {
            try
            {
                var transaction = context.Database.CurrentTransaction;
                if (transaction == null)
                {
                    return "No active transaction";
                }

                return $"Active transaction ID: {transaction.TransactionId}";
            }
            catch (Exception ex)
            {
                return $"Error getting transaction info: {ex.Message}";
            }
        }

        /// <summary>
        /// Publiczna metoda do weryfikacji i aktualizacji danych składnika z OpenFoodFacts
        /// </summary>
        /// <param name="ingredient">Składnik do zaktualizowania</param>
        /// <returns>True jeśli dane zostały zaktualizowane, False w przeciwnym przypadku</returns>
        public static async Task<bool> UpdateIngredientWithOpenFoodFactsAsync(Ingredient ingredient)
        {
            using var httpClient = new HttpClient();
            
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
                
                if (newCalories >= 0 && Math.Abs(oldCalories - newCalories) > 0.1)
                {
                    ingredient.Calories = newCalories;
                    wasUpdated = true;
                }
                
                if (newProtein >= 0 && Math.Abs(oldProtein - newProtein) > 0.1)
                {
                    ingredient.Protein = newProtein;
                    wasUpdated = true;
                }
                
                if (newFat >= 0 && Math.Abs(oldFat - newFat) > 0.1)
                {
                    ingredient.Fat = newFat;
                    wasUpdated = true;
                }
                
                if (newCarbs >= 0 && Math.Abs(oldCarbs - newCarbs) > 0.1)
                {
                    ingredient.Carbs = newCarbs;
                    wasUpdated = true;
                }

                if (wasUpdated)
                {
                    LogDebug($"{ingredient.Name} updated: kcal={ingredient.Calories}, P={ingredient.Protein}, F={ingredient.Fat}, C={ingredient.Carbs}");
                }
                else
                {
                    LogDebug($"{ingredient.Name}: OpenFoodFacts data identical to current");
                }
                
                return wasUpdated;
            }
            catch (Exception ex)
            {
                LogError($"{ingredient.Name}: {ex.Message}");
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

        /// <summary>
        /// Metoda diagnostyczna do testowania składników w bazie danych
        /// </summary>
        public static async Task<string> DiagnoseDatabaseAsync(AppDbContext context)
        {
            try
            {
                var ingredientCount = await context.Ingredients.CountAsync();
                var recipeCount = await context.Recipes.CountAsync();
                
                var dbPath = context.Database.GetConnectionString();
                var dbExists = !string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath.Replace("Filename=", ""));
                
                var transactionInfo = GetTransactionInfo(context);
                
                LogDebug($"DIAGNOSTIC: Database exists: {dbExists}");
                LogDebug($"DIAGNOSTIC: Ingredients in DB: {ingredientCount}");
                LogDebug($"DIAGNOSTIC: Recipes in DB: {recipeCount}");
                LogDebug($"DIAGNOSTIC: Transaction: {transactionInfo}");
                
                return $"Database: {(dbExists ? "EXISTS" : "MISSING")}\n" +
                       $"Ingredients: {ingredientCount}\n" +
                       $"Recipes: {recipeCount}\n" +
                       $"Transaction: {transactionInfo}\n" +
                       $"Path: {dbPath}";
            }
            catch (Exception ex)
            {
                LogError($"DIAGNOSTIC: Database diagnosis failed: {ex.Message}");
                return $"ERROR: {ex.Message}";
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
