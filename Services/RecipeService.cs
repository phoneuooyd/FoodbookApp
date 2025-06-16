using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly AppDbContext _context;

        public RecipeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Recipe>> GetRecipesAsync() => await _context.Recipes.Include(r => r.Ingredients).ToListAsync();
        public async Task<Recipe?> GetRecipeAsync(int id) => await _context.Recipes.Include(r => r.Ingredients).FirstOrDefaultAsync(r => r.Id == id);
        public async Task AddRecipeAsync(Recipe recipe)
        {
            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();
        }
        public async Task UpdateRecipeAsync(Recipe recipe)
        {
            _context.Recipes.Update(recipe);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteRecipeAsync(int id)
        {
            var recipe = await _context.Recipes.FindAsync(id);
            if (recipe != null)
            {
                _context.Recipes.Remove(recipe);
                await _context.SaveChangesAsync();
            }
        }
    }
}