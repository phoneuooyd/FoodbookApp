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
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("🌱 Starting ingredient seeding...");
                var ingredients = await LoadPopularIngredientsAsync();

                foreach (var ingredient in ingredients)
                {
                    ingredient.RecipeId = null;
                }

                context.Ingredients.AddRange(ingredients);
                await context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"✅ Successfully added {ingredients.Count} ingredients to database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error seeding ingredients: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
            httpClient.Timeout = TimeSpan.FromSeconds(10); // Timeout dla API
            
            var url = $"https://world.openfoodfacts.org/cgi/search.pl?search_terms={Uri.EscapeDataString(ingredient.Name)}&search_simple=1&json=1";

            try
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) 
                {
                    System.Diagnostics.Debug.WriteLine($"❌ {ingredient.Name}: HTTP błąd {response.StatusCode}");
                    return false;
                }

                var content = await response.Content.ReadAsStringAsync();
                dynamic? result = JsonConvert.DeserializeObject(content);
                
                // Sprawdź czy znaleziono produkty
                if (result?.products == null || result.products.Count == 0) 
                {
                    System.Diagnostics.Debug.WriteLine($"❌ {ingredient.Name}: Nie znaleziono produktu w OpenFoodFacts");
                    return false;
                }

                var product = result.products[0];
                var nutriments = product.nutriments;

                // Sprawdź czy nutriments istnieją
                if (nutriments == null)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ {ingredient.Name}: Brak danych odżywczych w znalezionym produkcie");
                    return false;
                }

                // Funkcja pomocnicza do bezpiecznego pobierania wartości
                double TryGet(dynamic src, string key)
                {
                    try
                    {
                        var value = src?[key];
                        if (value == null) return -1; // -1 oznacza brak danych
                        
                        if (double.TryParse(value.ToString(), out double parsed))
                            return parsed;
                        
                        return -1; // Nie można sparsować
                    }
                    catch
                    {
                        return -1; // Błąd podczas dostępu
                    }
                }

                // Zapisz oryginalne wartości
                var oldCalories = ingredient.Calories;
                var oldProtein = ingredient.Protein;
                var oldFat = ingredient.Fat;
                var oldCarbs = ingredient.Carbs;

                // Pobierz nowe wartości
                var newCalories = TryGet(nutriments, "energy-kcal_100g");
                var newProtein = TryGet(nutriments, "proteins_100g");
                var newFat = TryGet(nutriments, "fat_100g");
                var newCarbs = TryGet(nutriments, "carbohydrates_100g");

                // Sprawdź czy udało się pobrać chociaż jedną wartość odżywczą
                bool hasValidData = newCalories >= 0 || newProtein >= 0 || newFat >= 0 || newCarbs >= 0;
                
                if (!hasValidData)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ {ingredient.Name}: Znaleziono produkt, ale brak prawidłowych danych odżywczych");
                    return false;
                }

                // Aktualizuj tylko te wartości, które zostały znalezione (>=0)
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
                    System.Diagnostics.Debug.WriteLine($"✅ {ingredient.Name} → kcal: {ingredient.Calories}, P: {ingredient.Protein}, F: {ingredient.Fat}, C: {ingredient.Carbs}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ℹ️ {ingredient.Name}: Dane z OpenFoodFacts są identyczne z obecnymi");
                }
                
                return wasUpdated;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ {ingredient.Name}: {ex.Message}");
                return false;
            }
        }

        private class IngredientInfo
        {
            public string Name { get; set; } = string.Empty;
            public double Calories { get; set; }
            public double Protein { get; set; }
            public double Fat { get; set; }
            public double Carbs { get; set; }
            public double Amount { get; set; }
            public string Unit { get; set; } = string.Empty;
        }

        private static async Task<List<Ingredient>> LoadPopularIngredientsAsync()
        {
            string json;

            try
            {
                System.Diagnostics.Debug.WriteLine("📂 Attempting to load ingredients.json...");
                
                // Pierwsza próba: MAUI App Package (najlepszy sposób dla .NET MAUI)
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("ingredients.json");
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                    System.Diagnostics.Debug.WriteLine("✅ Loaded ingredients.json from App Package");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Failed to load from App Package: {ex.Message}");
                    
                    // Druga próba: Embedded Resource
                    try
                    {
                        var assembly = Assembly.GetExecutingAssembly();
                        var resourceNames = assembly.GetManifestResourceNames();
                        System.Diagnostics.Debug.WriteLine($"🔍 Available resources: {string.Join(", ", resourceNames)}");
                        
                        var resourceName = resourceNames.FirstOrDefault(name => name.EndsWith("ingredients.json"));
                        
                        if (!string.IsNullOrEmpty(resourceName))
                        {
                            using var stream = assembly.GetManifestResourceStream(resourceName);
                            if (stream != null)
                            {
                                using var reader = new StreamReader(stream);
                                json = await reader.ReadToEndAsync();
                                System.Diagnostics.Debug.WriteLine($"✅ Loaded ingredients.json from Embedded Resource: {resourceName}");
                            }
                            else
                            {
                                throw new FileNotFoundException($"Resource stream is null for {resourceName}");
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException("No ingredients.json resource found");
                        }
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ Failed to load from Embedded Resource: {ex2.Message}");
                        
                        // Trzecia próba: File System (najmniej preferowana)
                        string[] paths = new[]
                        {
                            Path.Combine(AppContext.BaseDirectory, "ingredients.json"),
                            Path.Combine(Environment.CurrentDirectory, "ingredients.json"),
                            Path.Combine(FileSystem.AppDataDirectory, "ingredients.json")
                        };

                        json = null;
                        foreach (var path in paths)
                        {
                            System.Diagnostics.Debug.WriteLine($"🔍 Checking path: {path}");
                            if (File.Exists(path))
                            {
                                json = await File.ReadAllTextAsync(path);
                                System.Diagnostics.Debug.WriteLine($"✅ Loaded ingredients.json from: {path}");
                                break;
                            }
                        }

                        if (string.IsNullOrEmpty(json))
                        {
                            System.Diagnostics.Debug.WriteLine("⚠️ ingredients.json not found, using fallback data");
                            // Fallback: utworz podstawowe składniki programowo
                            return GetFallbackIngredients();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Critical error loading ingredients.json: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("🔄 Using fallback ingredients data");
                return GetFallbackIngredients();
            }

            try
            {
                var infos = JsonConvert.DeserializeObject<List<IngredientInfo>>(json) ?? new();
                System.Diagnostics.Debug.WriteLine($"📊 Parsed {infos.Count} ingredients from JSON");
                
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

                System.Diagnostics.Debug.WriteLine($"🔧 Created {ingredients.Count} ingredient objects");
                
                // USUNIĘTO: await UpdateWithOpenFoodFactsDataAsync(ingredients);
                // OpenFoodFacts weryfikacja została przeniesiona do opcjonalnej funkcjonalności
                System.Diagnostics.Debug.WriteLine("ℹ️ Skipping OpenFoodFacts verification during initial seeding for faster startup");
                
                return ingredients;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error parsing ingredients JSON: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("🔄 Using fallback ingredients data");
                return GetFallbackIngredients();
            }
        }

        /// <summary>
        /// Fallback lista podstawowych składników w przypadku problemów z ładowaniem pliku JSON
        /// </summary>
        private static List<Ingredient> GetFallbackIngredients()
        {
            System.Diagnostics.Debug.WriteLine("🆘 Creating fallback ingredients list");
            
            return new List<Ingredient>
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
                new Ingredient { Name = "Ziemniak", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1.0, Fat = 0.0, Carbs = 5.0, RecipeId = null },
                new Ingredient { Name = "Marchew", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1.0, Fat = 0.0, Carbs = 5.0, RecipeId = null },
                new Ingredient { Name = "Ser żółty", Quantity = 100, Unit = Unit.Gram, Calories = 150, Protein = 7.0, Fat = 8.0, Carbs = 5.0, RecipeId = null },
                new Ingredient { Name = "Ryż", Quantity = 100, Unit = Unit.Gram, Calories = 350, Protein = 10.0, Fat = 2.0, Carbs = 70.0, RecipeId = null },
                new Ingredient { Name = "Makaron", Quantity = 100, Unit = Unit.Gram, Calories = 350, Protein = 10.0, Fat = 2.0, Carbs = 70.0, RecipeId = null },
                new Ingredient { Name = "Wołowina", Quantity = 100, Unit = Unit.Gram, Calories = 200, Protein = 26.0, Fat = 10.0, Carbs = 0.0, RecipeId = null },
                new Ingredient { Name = "Jabłko", Quantity = 100, Unit = Unit.Gram, Calories = 50, Protein = 1.0, Fat = 0.0, Carbs = 13.0, RecipeId = null },
                new Ingredient { Name = "Banan", Quantity = 100, Unit = Unit.Gram, Calories = 50, Protein = 1.0, Fat = 0.0, Carbs = 13.0, RecipeId = null },
                new Ingredient { Name = "Sałata", Quantity = 100, Unit = Unit.Gram, Calories = 25, Protein = 1.0, Fat = 0.0, Carbs = 5.0, RecipeId = null }
            };
        }

        private static async Task UpdateWithOpenFoodFactsDataAsync(List<Ingredient> ingredients)
        {
            System.Diagnostics.Debug.WriteLine($"🌐 Starting OpenFoodFacts verification for {ingredients.Count} ingredients...");
            
            foreach (var ingredient in ingredients)
            {
                await UpdateIngredientWithOpenFoodFactsAsync(ingredient);
                // Małe opóźnienie między zapytaniami aby nie przeciążyć API
                await Task.Delay(100);
            }
            
            System.Diagnostics.Debug.WriteLine("✅ OpenFoodFacts verification completed");
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
    }
}
