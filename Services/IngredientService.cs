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

    public async Task<List<Ingredient>> GetIngredientsAsync() => 
        await _context.Ingredients
            .Where(i => i.RecipeId == null)
            .OrderBy(i => i.Name)
            .ToListAsync();

    public async Task<Ingredient?> GetIngredientAsync(int id) => 
        await _context.Ingredients.FindAsync(id);

    public async Task AddIngredientAsync(Ingredient ingredient)
    {
        // Ensure RecipeId is null for standalone ingredients
        if (ingredient.Recipe == null)
            ingredient.RecipeId = null;
        
        try
        {
            _context.Ingredients.Add(ingredient);
            await _context.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"Added ingredient: {ingredient.Name}, ID: {ingredient.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding ingredient: {ex.Message}");
            throw;
        }
    }

    public async Task UpdateIngredientAsync(Ingredient ingredient)
    {
        try
        {
            // Ensure we don't lose the RecipeId status when updating
            var existingIngredient = await _context.Ingredients.FindAsync(ingredient.Id);
            if (existingIngredient != null)
            {
                // Update only the fields we want to change
                existingIngredient.Name = ingredient.Name;
                existingIngredient.Quantity = ingredient.Quantity;
                existingIngredient.Unit = ingredient.Unit;
                // RecipeId stays the same
                
                await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"Updated ingredient: {ingredient.Name}, ID: {ingredient.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating ingredient: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteIngredientAsync(int id)
    {
        try
        {
            var ing = await _context.Ingredients.FindAsync(id);
            if (ing != null)
            {
                _context.Ingredients.Remove(ing);
                await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"Deleted ingredient ID: {id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting ingredient: {ex.Message}");
            throw;
        }
    }
}
