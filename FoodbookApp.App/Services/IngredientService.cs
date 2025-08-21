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
            .AsNoTracking() // Improves performance for read-only operations
            .Where(i => i.RecipeId == null)
            .OrderBy(i => i.Name)
            .ToListAsync();

    public async Task<Ingredient?> GetIngredientAsync(int id) => 
        await _context.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

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
            // Use more efficient update approach
            var existingIngredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Id == ingredient.Id);
                
            if (existingIngredient != null)
            {
                // Update only the fields we want to change
                existingIngredient.Name = ingredient.Name;
                existingIngredient.Quantity = ingredient.Quantity;
                existingIngredient.Unit = ingredient.Unit;
                existingIngredient.Calories = ingredient.Calories;
                existingIngredient.Protein = ingredient.Protein;
                existingIngredient.Fat = ingredient.Fat;
                existingIngredient.Carbs = ingredient.Carbs;
                // RecipeId stays the same

                await _context.SaveChangesAsync();
                System.Diagnostics.Debug.WriteLine($"Updated ingredient: {ingredient.Name}, ID: {ingredient.Id}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Ingredient with ID {ingredient.Id} not found for update");
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
            // More efficient delete - no need to load the entity first
            var ingredient = new Ingredient { Id = id };
            _context.Ingredients.Attach(ingredient);
            _context.Ingredients.Remove(ingredient);
            await _context.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"Deleted ingredient ID: {id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting ingredient: {ex.Message}");
            // Fallback to traditional delete if attach/remove fails
            try
            {
                var ing = await _context.Ingredients.FindAsync(id);
                if (ing != null)
                {
                    _context.Ingredients.Remove(ing);
                    await _context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"Deleted ingredient ID: {id} (fallback method)");
                }
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error in fallback delete: {fallbackEx.Message}");
                throw;
            }
        }
    }
}
