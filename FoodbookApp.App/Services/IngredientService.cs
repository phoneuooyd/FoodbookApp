using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class IngredientService : IIngredientService
{
    private readonly AppDbContext _context;
    private readonly ISupabaseSyncService? _syncService;
    
    // Shared (static) cache so different DI scopes see the same cached data
    private static List<Ingredient>? _cachedIngredientsStatic;
    private static DateTime _lastCacheTimeStatic = DateTime.MinValue;
    private static readonly TimeSpan _cacheValidityStatic = TimeSpan.FromMinutes(10);
    private static readonly object _cacheLock = new();
    
    private static List<string>? _cachedIngredientNamesStatic;
    private static DateTime _lastNamesCacheTimeStatic = DateTime.MinValue;
    
    public IngredientService(AppDbContext context, IServiceProvider serviceProvider)
    {
        _context = context;
        
        // Try to resolve sync service
        try
        {
            _syncService = serviceProvider.GetService(typeof(ISupabaseSyncService)) as ISupabaseSyncService;
        }
        catch
        {
            _syncService = null;
        }
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
                
                // ? OPTYMALIZACJA: Aktualizuj te¿ cache nazw
                _cachedIngredientNamesStatic = fresh.Select(i => i.Name).ToList();
                _lastNamesCacheTimeStatic = DateTime.Now;
            }

            System.Diagnostics.Debug.WriteLine($"? Ingredients cache refreshed (shared): {fresh.Count} items");
            return fresh.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? [IngredientService] GetIngredientsAsync failed: {ex.Message}");
            return new List<Ingredient>();
        }
    }

    /// <summary>
    /// ? NOWA METODA: Szybkie pobieranie tylko nazw sk³adników (lightweight)
    /// </summary>
    public async Task<List<string>> GetIngredientNamesAsync()
    {
        try
        {
            lock (_cacheLock)
            {
                if (_cachedIngredientNamesStatic != null && DateTime.Now - _lastNamesCacheTimeStatic <= _cacheValidityStatic)
                {
                    System.Diagnostics.Debug.WriteLine($"? [IngredientService] Returning {_cachedIngredientNamesStatic.Count} names from cache");
                    return _cachedIngredientNamesStatic.ToList();
                }
            }

            System.Diagnostics.Debug.WriteLine("?? [IngredientService] Loading ingredient names from database...");

            // ? KRYTYCZNA OPTYMALIZACJA: Pobierz TYLKO nazwy bez ca³ych obiektów
            var names = await _context.Ingredients
                .AsNoTracking()
                .Where(i => i.RecipeId == null)
                .OrderBy(i => i.Name)
                .Select(i => i.Name)
                .ToListAsync();

            lock (_cacheLock)
            {
                _cachedIngredientNamesStatic = names;
                _lastNamesCacheTimeStatic = DateTime.Now;
            }

            System.Diagnostics.Debug.WriteLine($"? [IngredientService] Loaded {names.Count} ingredient names");
            return names.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? [IngredientService] GetIngredientNamesAsync failed: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<Ingredient?> GetIngredientAsync(Guid id) => 
        await _context.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task AddIngredientAsync(Ingredient ingredient)
    {
        if (ingredient.Id == Guid.Empty)
            ingredient.Id = Guid.NewGuid();

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
                    
                    // Queue for cloud sync (Update operation)
                    if (_syncService != null)
                    {
                        try
                        {
                            await _syncService.QueueForSyncAsync(ingredient, SyncOperationType.Update);
                            System.Diagnostics.Debug.WriteLine($"[IngredientService] Queued ingredient {ingredient.Id} for Update sync");
                        }
                        catch (Exception syncEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[IngredientService] Failed to queue sync: {syncEx.Message}");
                        }
                    }
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

    public async Task DeleteIngredientAsync(Guid id)
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
                    
                    // Queue for cloud sync (Delete operation)
                    if (_syncService != null)
                    {
                        try
                        {
                            var deleteEntity = new Ingredient { Id = id, Name = ingredient.Name };
                            await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
                            System.Diagnostics.Debug.WriteLine($"[IngredientService] Queued ingredient {id} for Delete sync");
                        }
                        catch (Exception syncEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[IngredientService] Failed to queue sync: {syncEx.Message}");
                        }
                    }
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
                        
                        // Queue for cloud sync (Delete operation) in fallback
                        if (_syncService != null)
                        {
                            try
                            {
                                var deleteEntity = new Ingredient { Id = id, Name = ing.Name };
                                await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
                                System.Diagnostics.Debug.WriteLine($"[IngredientService] Queued ingredient {id} for Delete sync (fallback)");
                            }
                            catch (Exception syncEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[IngredientService] Failed to queue sync (fallback): {syncEx.Message}");
                            }
                        }
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
            
            _cachedIngredientNamesStatic = null;
            _lastNamesCacheTimeStatic = DateTime.MinValue;
        }
        System.Diagnostics.Debug.WriteLine("? Ingredients cache invalidated (shared + names)");
    }
}
