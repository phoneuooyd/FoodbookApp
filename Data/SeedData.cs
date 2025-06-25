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
            // Seed popular ingredients from embedded JSON (if not already added)
            if (!await context.Ingredients.AnyAsync(i => i.RecipeId == 0))
            {
                var popularIngredients = await LoadPopularIngredientsAsync();
                context.Ingredients.AddRange(popularIngredients);
            }

            
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();
        }

        private static async Task<List<Ingredient>> LoadPopularIngredientsAsync()
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("Data/ingredients.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            var names = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            return names.Select(n => new Ingredient { Name = n, Quantity = 0, Unit = Unit.Gram }).ToList();
        }
    }
}