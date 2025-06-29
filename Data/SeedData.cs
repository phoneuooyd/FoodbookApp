using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
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
            // Skip seeding if any ingredients already exist
            if (await context.Ingredients.AnyAsync())
                return;

            try
            {
                var ingredients = await LoadPopularIngredientsAsync();
                
                // Set RecipeId to null explicitly for standalone ingredients
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
                
                // Add at least a few basic ingredients as fallback
                var basicIngredients = new List<Ingredient>
                {
                    new Ingredient { Name = "Jajka", Quantity = 1, Unit = Unit.Piece, Calories = 155, Protein = 13, Fat = 11, Carbs = 1, RecipeId = null },
                    new Ingredient { Name = "Mleko", Quantity = 100, Unit = Unit.Milliliter, Calories = 150, Protein = 7, Fat = 8, Carbs = 5, RecipeId = null },
                    new Ingredient { Name = "Mąka", Quantity = 100, Unit = Unit.Gram, Calories = 100, Protein = 2, Fat = 2, Carbs = 10, RecipeId = null },
                    new Ingredient { Name = "Cukier", Quantity = 100, Unit = Unit.Gram, Calories = 100, Protein = 2, Fat = 2, Carbs = 10, RecipeId = null },
                    new Ingredient { Name = "Sól", Quantity = 100, Unit = Unit.Gram, Calories = 100, Protein = 2, Fat = 2, Carbs = 10, RecipeId = null }
                };
                
                context.Ingredients.AddRange(basicIngredients);
                await context.SaveChangesAsync();
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
            
            // Metoda 1: Próba odczytu jako embedded resource
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
                // Metoda 2: Próba odczytu z FileSystem (oryginalny sposób)
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("ingredients.json");
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
                catch
                {
                    // Metoda 3: Próba tradycyjnego odczytu pliku
                    try
                    {
                        using var stream = await FileSystem.Current.OpenAppPackageFileAsync("ingredients.json");
                        using var reader = new StreamReader(stream);
                        json = await reader.ReadToEndAsync();
                    }
                    catch
                    {
                        // Metoda 4: Próba odczytu z różnych lokalizacji
                        var possiblePaths = new[]
                        {
                            Path.Combine(AppContext.BaseDirectory, "ingredients.json"),
                            Path.Combine(AppContext.BaseDirectory, "Resources", "Raw", "ingredients.json"),
                            Path.Combine(Environment.CurrentDirectory, "ingredients.json"),
                            Path.Combine(Environment.CurrentDirectory, "Resources", "Raw", "ingredients.json")
                        };

                        json = null;
                        foreach (var path in possiblePaths)
                        {
                            try
                            {
                                if (File.Exists(path))
                                {
                                    json = await File.ReadAllTextAsync(path);
                                    break;
                                }
                            }
                            catch { continue; }
                        }

                        if (string.IsNullOrEmpty(json))
                        {
                            throw new FileNotFoundException("ingredients.json nie został znaleziony w żadnej z lokalizacji");
                        }
                    }
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

            return ingredients;
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