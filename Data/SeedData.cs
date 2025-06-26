using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Storage;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace Foodbook.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(AppDbContext context)
        {
            await SeedIngredientsAsync(context);

            if (await context.Recipes.AnyAsync())
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
                    new Ingredient { Name = "Sałata", Quantity = 100, Unit = Unit.Gram },
                    new Ingredient { Name = "Pomidor", Quantity = 50, Unit = Unit.Gram }
                }
            };

            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();
        }

        public static async Task SeedIngredientsAsync(AppDbContext context)
        {
            // Check if there are already standalone ingredients (RecipeId is null)
            if (await context.Ingredients.AnyAsync(i => i.RecipeId == null))
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
                    new Ingredient { Name = "Jajka", Quantity = 1, Unit = Unit.Piece, RecipeId = null },
                    new Ingredient { Name = "Mleko", Quantity = 100, Unit = Unit.Milliliter, RecipeId = null },
                    new Ingredient { Name = "Mąka", Quantity = 100, Unit = Unit.Gram, RecipeId = null },
                    new Ingredient { Name = "Cukier", Quantity = 100, Unit = Unit.Gram, RecipeId = null },
                    new Ingredient { Name = "Sól", Quantity = 100, Unit = Unit.Gram, RecipeId = null }
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
            public Unit Unit { get; set; }
        }

        private static async Task<List<Ingredient>> LoadPopularIngredientsAsync()
        {
            // Try different possible paths for the file
            string[] possiblePaths = new[]
            {
                "ingredients.json",          // Direct filename
                "Data/ingredients.json",     // Data subfolder
                "Resources/Data/ingredients.json" // Full path
            };

            Exception? lastException = null;

            // Try each possible path
            foreach (var path in possiblePaths)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Trying to load file from path: {path}");
                    using var stream = await FileSystem.OpenAppPackageFileAsync(path);
                    using var reader = new StreamReader(stream);
                    var json = await reader.ReadToEndAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"Successfully loaded JSON from {path}. First 100 chars: {json.Substring(0, Math.Min(100, json.Length))}...");
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    
                    var infos = JsonSerializer.Deserialize<List<IngredientInfo>>(json, options) ?? new();
                    
                    System.Diagnostics.Debug.WriteLine($"Deserialized {infos.Count} ingredients");
                    
                    // Create ingredients with proper names and values
                    var ingredients = infos.Select(i => new Ingredient
                    {
                        Name = i.Name,
                        Quantity = i.Amount,
                        Unit = i.Unit,
                        RecipeId = null // Explicitly set null for standalone ingredients
                    }).ToList();
                    
                    return ingredients;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading ingredients from {path}: {ex.Message}");
                    lastException = ex;
                    // Continue to try the next path
                }
            }

            // If we get here, all paths failed
            System.Diagnostics.Debug.WriteLine("All file paths failed to load the ingredients file");
            if (lastException != null)
            {
                throw new Exception("Failed to load ingredients file from any of the attempted paths", lastException);
            }
            
            // Return an empty list as a last resort
            return new List<Ingredient>();
        }
    }
}