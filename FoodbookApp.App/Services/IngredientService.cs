using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class IngredientService : IIngredientService
{
    private readonly AppDbContext _context;
    
    // Shared (static) cache so different DI scopes see the same cached data
    private static List<Ingredient>? _cachedIngredientsStatic;
    private static DateTime _lastCacheTimeStatic = DateTime.MinValue;
    private static readonly TimeSpan _cacheValidityStatic = TimeSpan.FromMinutes(10);
    private static readonly object _cacheLock = new();
    
    public IngredientService(AppDbContext context)
    {
        _context = context;
    }

    // Getter uses shared static cache with lock for thread-safety
    public async Task<List<Ingredient>> GetIngredientsAsync()
    {
        try
        {
            lock (_cacheLock)
            {
                if (_cachedIngredientsStatic != null && DateTime.Now - _lastCacheTimeStatic <= _cacheValidityStatic)
                {
                    // Return a copy to prevent caller mutation
                    return _cachedIngredientsStatic.ToList();
                }
            }

            // Load from DB outside lock
            var fresh = await _context.Ingredients
                .AsNoTracking()
                .Where(i => i.RecipeId == null)
                .OrderBy(i => i.Name)
                .ToListAsync();

            lock (_cacheLock)
            {
                _cachedIngredientsStatic = fresh;
                _lastCacheTimeStatic = DateTime.Now;
            }

            System.Diagnostics.Debug.WriteLine($"Ingredients cache refreshed (shared): {fresh.Count} items");
            return fresh.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientService] GetIngredientsAsync failed: {ex.Message}");
            return new List<Ingredient>();
        }
    }

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
            
            // Invalidate shared cache only when standalone ingredient added
            if (ingredient.RecipeId == null)
            {
                InvalidateCache();
                System.Diagnostics.Debug.WriteLine($"? Added standalone ingredient: {ingredient.Name}, ID: {ingredient.Id}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"? Added recipe ingredient: {ingredient.Name}, RecipeId: {ingredient.RecipeId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? Error adding ingredient: {ex.Message}");
            throw;
        }
    }

    public async Task UpdateIngredientAsync(Ingredient ingredient)
    {
        try
        {
            var existingIngredient = await _context.Ingredients
                .FirstOrDefaultAsync(i => i.Id == ingredient.Id);
                
            if (existingIngredient != null)
            {
                existingIngredient.Name = ingredient.Name;
                existingIngredient.Quantity = ingredient.Quantity;
                existingIngredient.Unit = ingredient.Unit;
                existingIngredient.Calories = ingredient.Calories;
                existingIngredient.Protein = ingredient.Protein;
                existingIngredient.Fat = ingredient.Fat;
                existingIngredient.Carbs = ingredient.Carbs;

                await _context.SaveChangesAsync();

                // Invalidate shared cache for standalone ingredients
                if (existingIngredient.RecipeId == null)
                {
                    InvalidateCache();
                    System.Diagnostics.Debug.WriteLine($"? Updated standalone ingredient: {ingredient.Name}, ID: {ingredient.Id}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"? Updated recipe ingredient: {ingredient.Name}, RecipeId: {existingIngredient.RecipeId}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"? Ingredient with ID {ingredient.Id} not found for update");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? Error updating ingredient: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteIngredientAsync(int id)
    {
        try
        {
            var ingredient = await _context.Ingredients.FindAsync(id);
            bool wasStandalone = ingredient?.RecipeId == null;
            
            if (ingredient != null)
            {
                _context.Ingredients.Remove(ingredient);
                await _context.SaveChangesAsync();
                
                if (wasStandalone)
                {
                    InvalidateCache();
                    System.Diagnostics.Debug.WriteLine($"? Deleted standalone ingredient ID: {id}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"? Deleted recipe ingredient ID: {id}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? Error deleting ingredient: {ex.Message}");
            
            try
            {
                var ing = await _context.Ingredients.FindAsync(id);
                if (ing != null)
                {
                    bool wasStandalone = ing.RecipeId == null;
                    _context.Ingredients.Remove(ing);
                    await _context.SaveChangesAsync();
                    
                    if (wasStandalone)
                    {
                        InvalidateCache();
                    }
                    System.Diagnostics.Debug.WriteLine($"? Deleted ingredient ID: {id} (fallback method)");
                }
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in fallback delete: {fallbackEx.Message}");
                throw;
            }
        }
    }

    // Invalidate the shared cache
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedIngredientsStatic = null;
            _lastCacheTimeStatic = DateTime.MinValue;
        }
        System.Diagnostics.Debug.WriteLine("Ingredients cache invalidated (shared)");
    }
}
