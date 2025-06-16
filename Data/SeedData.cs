using Foodbook.Models;
using Microsoft.EntityFrameworkCore;

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
                Name = "Sample Salad",
                Description = "Simple healthy salad.",
                Calories = 150,
                Protein = 3,
                Fat = 7,
                Carbs = 18,
                Ingredients = new List<Ingredient>
                {
                    new Ingredient { Name = "Lettuce", Quantity = 100, Unit = Unit.Gram },
                    new Ingredient { Name = "Tomato", Quantity = 50, Unit = Unit.Gram }
                }
            };

            context.Recipes.Add(recipe);
            await context.SaveChangesAsync();
        }
    }
}