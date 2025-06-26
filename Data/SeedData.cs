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
            using var stream = await FileSystem.OpenAppPackageFileAsync("ingredients.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var infos = JsonSerializer.Deserialize<List<IngredientInfo>>(json, options) ?? new();

            return infos.Select(i => new Ingredient
            {
                Name = i.Name,
                Quantity = i.Amount,
                Unit = i.Unit,
                RecipeId = null
            }).ToList();
        }
    }
}