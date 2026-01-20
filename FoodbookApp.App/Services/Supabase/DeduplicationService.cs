using System.Collections.Concurrent;
using System.Diagnostics;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace FoodbookApp.Services.Supabase;

/// <summary>
/// Service for deduplicating sync queue entries against cloud data.
/// Fetches cloud ingredients and recipes, compares by name + macros,
/// and removes duplicates from the local sync queue.
/// 
/// DESIGN: This service is an optimization layer, not a blocking requirement.
/// If cloud is empty (new user), sync should proceed normally.
/// If fetch fails, sync should proceed without deduplication.
/// </summary>
public sealed class DeduplicationService : IDeduplicationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISupabaseCrudService _crudService;
    private readonly IAuthTokenStore _tokenStore;

    // In-memory cache for cloud data
    private readonly ConcurrentDictionary<string, CloudIngredientData> _cloudIngredients = new();
    private readonly ConcurrentDictionary<string, CloudRecipeData> _cloudRecipes = new();
    
    private volatile bool _isCachePopulated = false;
    private volatile bool _fetchAttempted = false; // Track if we already tried to fetch
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    public bool IsCachePopulated => _isCachePopulated;

    public DeduplicationService(
        IServiceProvider serviceProvider,
        ISupabaseCrudService crudService,
        IAuthTokenStore tokenStore)
    {
        _serviceProvider = serviceProvider;
        _crudService = crudService;
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Fetches cloud data (ingredients, recipes) and caches in memory.
    /// Runs on background thread, does not block UI.
    /// Returns true if fetch succeeded (even with 0 items), false if error occurred.
    /// </summary>
    public async Task<bool> FetchCloudDataAsync(CancellationToken ct = default)
    {
        // Already attempted - don't retry in same session
        if (_fetchAttempted && _isCachePopulated)
        {
            Log("FetchCloudDataAsync: Using existing cache");
            return true;
        }

        if (!await _fetchLock.WaitAsync(TimeSpan.FromSeconds(10), ct))
        {
            Log("FetchCloudDataAsync: Another fetch in progress, skipping");
            return _isCachePopulated;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            Log("Fetching cloud data for deduplication...");

            // Clear previous cache
            ClearCacheInternal();

            bool ingredientsFetched = false;
            bool recipesFetched = false;

            // Fetch ingredients - catch errors individually
            try
            {
                await FetchIngredientsAsync(ct);
                ingredientsFetched = true;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                Log($"WARNING: Failed to fetch ingredients: {ex.Message}");
                // Continue - partial data is better than nothing
            }

            // Fetch recipes - catch errors individually
            try
            {
                await FetchRecipesAsync(ct);
                recipesFetched = true;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                Log($"WARNING: Failed to fetch recipes: {ex.Message}");
                // Continue - partial data is better than nothing
            }

            _fetchAttempted = true;
            
            // Mark as populated even if cloud has 0 items - that's valid for new users!
            _isCachePopulated = ingredientsFetched || recipesFetched;
            
            sw.Stop();
            Log($"Cloud data fetch completed in {sw.ElapsedMilliseconds}ms: " +
                $"{_cloudIngredients.Count} ingredients, {_cloudRecipes.Count} recipes " +
                $"(cache valid: {_isCachePopulated})");

            return _isCachePopulated;
        }
        catch (OperationCanceledException)
        {
            Log("FetchCloudDataAsync: Cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Log($"ERROR: FetchCloudDataAsync failed: {ex.Message}");
            _fetchAttempted = true; // Don't retry on error
            return false;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    // Implement interface method (returns Task, not Task<bool>)
    async Task IDeduplicationService.FetchCloudDataAsync(CancellationToken ct)
    {
        await FetchCloudDataAsync(ct);
    }

    private async Task FetchIngredientsAsync(CancellationToken ct)
    {
        try
        {
            var cloudIngredients = await _crudService.GetIngredientsAsync(ct);
            
            // Handle null or empty list gracefully
            if (cloudIngredients == null)
            {
                Log("FetchIngredientsAsync: Received null from cloud (treating as empty)");
                return;
            }

            foreach (var ing in cloudIngredients)
            {
                if (ing == null) continue;
                
                var key = (ing.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(key)) continue;
                
                var data = new CloudIngredientData
                {
                    Id = ing.Id,
                    Name = key,
                    Calories = ing.Calories,
                    Protein = ing.Protein,
                    Fat = ing.Fat,
                    Carbs = ing.Carbs
                };
                
                _cloudIngredients.TryAdd(key, data);
            }
            
            Log($"Fetched {cloudIngredients.Count} ingredients from cloud ({_cloudIngredients.Count} cached)");
        }
        catch (Exception ex)
        {
            Log($"ERROR in FetchIngredientsAsync: {ex.Message}");
            throw; // Re-throw to be caught by caller
        }
    }

    private async Task FetchRecipesAsync(CancellationToken ct)
    {
        try
        {
            var cloudRecipes = await _crudService.GetRecipesAsync(ct);
            
            // Handle null or empty list gracefully
            if (cloudRecipes == null)
            {
                Log("FetchRecipesAsync: Received null from cloud (treating as empty)");
                return;
            }

            foreach (var recipe in cloudRecipes)
            {
                if (recipe == null) continue;
                
                var key = (recipe.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(key)) continue;
                
                var data = new CloudRecipeData
                {
                    Id = recipe.Id,
                    Name = key,
                    Calories = recipe.Calories,
                    Protein = recipe.Protein,
                    Fat = recipe.Fat,
                    Carbs = recipe.Carbs
                };
                
                _cloudRecipes.TryAdd(key, data);
            }
            
            Log($"Fetched {cloudRecipes.Count} recipes from cloud ({_cloudRecipes.Count} cached)");
        }
        catch (Exception ex)
        {
            Log($"ERROR in FetchRecipesAsync: {ex.Message}");
            throw; // Re-throw to be caught by caller
        }
    }

    /// <summary>
    /// Compares local sync queue with cached cloud data and removes duplicates.
    /// Returns 0 if cloud is empty (new user) - this is normal, not an error.
    /// Returns -1 if error occurred (but still allows sync to proceed).
    /// </summary>
    public async Task<int> DeduplicateSyncQueueAsync(CancellationToken ct = default)
    {
        try
        {
            // Try to populate cache if not done
            if (!_isCachePopulated && !_fetchAttempted)
            {
                Log("DeduplicateSyncQueueAsync: Cache empty, attempting fetch...");
                var fetchResult = await FetchCloudDataAsync(ct);
                if (!fetchResult)
                {
                    Log("DeduplicateSyncQueueAsync: Fetch failed, skipping deduplication (sync will proceed)");
                    return 0; // Return 0, not error - let sync proceed
                }
            }

            // If cloud is empty, that's fine - new user scenario
            if (_cloudIngredients.Count == 0 && _cloudRecipes.Count == 0)
            {
                Log("DeduplicateSyncQueueAsync: Cloud is empty (new user) - no duplicates to check");
                return 0;
            }

            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
            {
                Log("DeduplicateSyncQueueAsync: No active account, skipping");
                return 0;
            }

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var sw = Stopwatch.StartNew();
            int removedCount = 0;

            // Get pending entries for Ingredient and Recipe types
            var pendingEntries = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value 
                    && e.Status == SyncEntryStatus.Pending
                    && (e.EntityType == "Ingredient" || e.EntityType == "Recipe")
                    && e.OperationType == SyncOperationType.Insert)
                .ToListAsync(ct);

            Log($"Checking {pendingEntries.Count} pending entries against {_cloudIngredients.Count} cloud ingredients and {_cloudRecipes.Count} cloud recipes...");

            // No pending entries - nothing to deduplicate
            if (pendingEntries.Count == 0)
            {
                Log("DeduplicateSyncQueueAsync: No pending entries to check");
                return 0;
            }

            // Process Ingredients first
            var ingredientEntries = pendingEntries.Where(e => e.EntityType == "Ingredient").ToList();
            if (ingredientEntries.Count > 0 && _cloudIngredients.Count > 0)
            {
                removedCount += ProcessIngredientEntries(ingredientEntries, ct);
            }

            // Then process Recipes
            var recipeEntries = pendingEntries.Where(e => e.EntityType == "Recipe").ToList();
            if (recipeEntries.Count > 0 && _cloudRecipes.Count > 0)
            {
                removedCount += ProcessRecipeEntries(recipeEntries, ct);
            }

            if (removedCount > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            sw.Stop();
            Log($"Deduplication complete in {sw.ElapsedMilliseconds}ms: removed {removedCount} duplicates from {pendingEntries.Count} entries");
            
            return removedCount;
        }
        catch (OperationCanceledException)
        {
            Log("DeduplicateSyncQueueAsync: Cancelled");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"ERROR in DeduplicateSyncQueueAsync: {ex.Message}");
            // Return 0, not throw - deduplication is optional, sync should proceed
            return 0;
        }
    }

    private int ProcessIngredientEntries(List<SyncQueueEntry> entries, CancellationToken ct)
    {
        int removed = 0;

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) break;

            if (string.IsNullOrEmpty(entry.Payload))
                continue;

            try
            {
                var localIngredient = System.Text.Json.JsonSerializer.Deserialize<Ingredient>(
                    entry.Payload,
                    new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                if (localIngredient == null)
                    continue;

                var name = (localIngredient.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                // Check if exists in cloud cache
                if (_cloudIngredients.TryGetValue(name, out var cloudData))
                {
                    // Compare all macro values
                    if (IsMacroMatch(
                        localIngredient.Calories, cloudData.Calories,
                        localIngredient.Protein, cloudData.Protein,
                        localIngredient.Fat, cloudData.Fat,
                        localIngredient.Carbs, cloudData.Carbs))
                    {
                        entry.Status = SyncEntryStatus.Completed;
                        entry.LastError = "Duplicate found in cloud - skipped";
                        entry.SyncedUtc = DateTime.UtcNow;
                        removed++;
                        
                        Log($"DUPLICATE: Ingredient '{name}' (ID: {entry.EntityId}) matches cloud");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"WARNING: Error processing ingredient entry {entry.EntityId}: {ex.Message}");
                // Continue with other entries
            }
        }

        return removed;
    }

    private int ProcessRecipeEntries(List<SyncQueueEntry> entries, CancellationToken ct)
    {
        int removed = 0;

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) break;

            if (string.IsNullOrEmpty(entry.Payload))
                continue;

            try
            {
                var localRecipe = System.Text.Json.JsonSerializer.Deserialize<Recipe>(
                    entry.Payload,
                    new System.Text.Json.JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                if (localRecipe == null)
                    continue;

                var name = (localRecipe.Name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                // Check if exists in cloud cache
                if (_cloudRecipes.TryGetValue(name, out var cloudData))
                {
                    // Compare all macro values
                    if (IsMacroMatch(
                        localRecipe.Calories, cloudData.Calories,
                        localRecipe.Protein, cloudData.Protein,
                        localRecipe.Fat, cloudData.Fat,
                        localRecipe.Carbs, cloudData.Carbs))
                    {
                        entry.Status = SyncEntryStatus.Completed;
                        entry.LastError = "Duplicate found in cloud - skipped";
                        entry.SyncedUtc = DateTime.UtcNow;
                        removed++;
                        
                        Log($"DUPLICATE: Recipe '{name}' (ID: {entry.EntityId}) matches cloud");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"WARNING: Error processing recipe entry {entry.EntityId}: {ex.Message}");
                // Continue with other entries
            }
        }

        return removed;
    }

    /// <summary>
    /// Compares macro values with tolerance for floating point precision.
    /// </summary>
    private static bool IsMacroMatch(
        double localCal, double cloudCal,
        double localProt, double cloudProt,
        double localFat, double cloudFat,
        double localCarbs, double cloudCarbs)
    {
        const double tolerance = 0.01;

        return Math.Abs(localCal - cloudCal) < tolerance
            && Math.Abs(localProt - cloudProt) < tolerance
            && Math.Abs(localFat - cloudFat) < tolerance
            && Math.Abs(localCarbs - cloudCarbs) < tolerance;
    }

    public void ClearCache()
    {
        ClearCacheInternal();
        Log("Cloud data cache cleared by external call");
    }

    private void ClearCacheInternal()
    {
        _cloudIngredients.Clear();
        _cloudRecipes.Clear();
        _isCachePopulated = false;
        _fetchAttempted = false;
    }

    private static void Log(string message) 
        => Debug.WriteLine($"[DeduplicationService] {message}");

    #region Cache Models

    private readonly record struct CloudIngredientData
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public double Calories { get; init; }
        public double Protein { get; init; }
        public double Fat { get; init; }
        public double Carbs { get; init; }
    }

    private readonly record struct CloudRecipeData
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public double Calories { get; init; }
        public double Protein { get; init; }
        public double Fat { get; init; }
        public double Carbs { get; init; }
    }

    #endregion
}
