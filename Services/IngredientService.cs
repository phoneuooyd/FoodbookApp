using Foodbook.Data;
using Foodbook.Models;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class IngredientService : IIngredientService
{
    private readonly AppDbContext _context;
    public IngredientService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Ingredient>> GetIngredientsAsync() => await _context.Ingredients.Where(i => i.RecipeId == 0).ToListAsync();

    public async Task<Ingredient?> GetIngredientAsync(int id) => await _context.Ingredients.FindAsync(id);

    public async Task AddIngredientAsync(Ingredient ingredient)
    {
        _context.Ingredients.Add(ingredient);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateIngredientAsync(Ingredient ingredient)
    {
        _context.Ingredients.Update(ingredient);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteIngredientAsync(int id)
    {
        var ing = await _context.Ingredients.FindAsync(id);
        if (ing != null)
        {
            _context.Ingredients.Remove(ing);
            await _context.SaveChangesAsync();
        }
    }
}
