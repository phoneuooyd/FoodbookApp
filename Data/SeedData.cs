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
                var ingredients = await LoadPopularIngredientsAsync();

                foreach (var ingredient in ingredients)
                {
                    ingredient.RecipeId = null;
                }

                context.Ingredients.AddRange(ingredients);
                await context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"Successfully added {ingredients.Count} ingredients to database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error seeding ingredients: {ex.Message}");
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
            
            var url = $"https://world.openfoodfacts.org/cgi/search.pl?search_terms={Uri.EscapeDataString(ingredient.Name)}&search_simple=1&json=1";

            try
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                var content = await response.Content.ReadAsStringAsync();
                dynamic? result = JsonConvert.DeserializeObject(content);
                if (result?.products == null || result.products.Count == 0) return false;

                var product = result.products[0];
                var nutriments = product.nutriments;

                double TryGet(dynamic src, string key) =>
                    double.TryParse((string?)src?[key], out var val) ? val : 0;

                var oldCalories = ingredient.Calories;
                var oldProtein = ingredient.Protein;
                var oldFat = ingredient.Fat;
                var oldCarbs = ingredient.Carbs;

                ingredient.Calories = TryGet(nutriments, "energy-kcal_100g");
                ingredient.Protein = TryGet(nutriments, "proteins_100g");
                ingredient.Fat = TryGet(nutriments, "fat_100g");
                ingredient.Carbs = TryGet(nutriments, "carbohydrates_100g");

                System.Diagnostics.Debug.WriteLine($"✅ {ingredient.Name} → kcal: {ingredient.Calories}, P: {ingredient.Protein}, F: {ingredient.Fat}, C: {ingredient.Carbs}");
                
                // Zwróć true jeśli dane się zmieniły
                return oldCalories != ingredient.Calories || 
                       oldProtein != ingredient.Protein || 
                       oldFat != ingredient.Fat || 
                       oldCarbs != ingredient.Carbs;
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
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(name => name.EndsWith("ingredients.json"));

                if (!string.IsNullOrEmpty(resourceName))
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
                else
                {
                    throw new FileNotFoundException("Resource not found");
                }
            }
            catch
            {
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("ingredients.json");
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
                catch
                {
                    string[] paths = new[]
                    {
                        Path.Combine(AppContext.BaseDirectory, "ingredients.json"),
                        Path.Combine(Environment.CurrentDirectory, "ingredients.json")
                    };

                    json = null;
                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            json = await File.ReadAllTextAsync(path);
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(json))
                        throw new FileNotFoundException("ingredients.json not found in known locations");
                }
            }

            var infos = JsonConvert.DeserializeObject<List<IngredientInfo>>(json) ?? new();
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

            await UpdateWithOpenFoodFactsDataAsync(ingredients);
            return ingredients;
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
    }
}
