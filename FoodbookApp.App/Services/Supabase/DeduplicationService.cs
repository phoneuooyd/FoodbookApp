using System.Collections.Concurrent;
using System.Diagnostics;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Services.Supabase;

/// <summary>
/// Service for deduplicating sync queue entries against cloud data.
/// Fetches cloud ingredients and recipes, compares by name + macros,
/// and removes duplicates from the local sync queue.
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
    /// </summary>
    public async Task FetchCloudDataAsync(CancellationToken ct = default)
    {
        if (!await _fetchLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            Log("FetchCloudDataAsync: Another fetch already in progress, skipping");
            return;
        }

        try
        {
            var sw = Stopwatch.StartNew();
            Log("Fetching cloud data for deduplication...");

            // Clear previous cache
            ClearCache();

            // Fetch ingredients first (as per requirement)
            await FetchIngredientsAsync(ct);

            // Then fetch recipes
            await FetchRecipesAsync(ct);

            _isCachePopulated = true;
            sw.Stop();
            
            Log($"Cloud data fetched in {sw.ElapsedMilliseconds}ms: {_cloudIngredients.Count} ingredients, {_cloudRecipes.Count} recipes");
        }
        catch (OperationCanceledException)
        {
            Log("FetchCloudDataAsync cancelled");
        }
        catch (Exception ex)
        {
            Log($"Error fetching cloud data: {ex.Message}");
            // Don't throw - deduplication is optional optimization
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task FetchIngredientsAsync(CancellationToken ct)
    {
        try
        {
            var cloudIngredients = await _crudService.GetIngredientsAsync(ct);
            
            foreach (var ing in cloudIngredients)
            {
                // Key: name (case-sensitive)
                var key = ing.Name ?? string.Empty;
                var data = new CloudIngredientData
                {
                    Id = ing.Id,
                    Name = ing.Name ?? string.Empty,
                    Calories = ing.Calories,
                    Protein = ing.Protein,
                    Fat = ing.Fat,
                    Carbs = ing.Carbs
                };
                
                _cloudIngredients.TryAdd(key, data);
            }
            
            Log($"Fetched {cloudIngredients.Count} ingredients from cloud");
        }
        catch (Exception ex)
        {
            Log($"Error fetching cloud ingredients: {ex.Message}");
        }
    }

    private async Task FetchRecipesAsync(CancellationToken ct)
    {
        try
        {
            var cloudRecipes = await _crudService.GetRecipesAsync(ct);
            
            foreach (var recipe in cloudRecipes)
            {
                // Key: name (case-sensitive)
                var key = recipe.Name ?? string.Empty;
                var data = new CloudRecipeData
                {
                    Id = recipe.Id,
                    Name = recipe.Name ?? string.Empty,
                    Calories = recipe.Calories,
                    Protein = recipe.Protein,
                    Fat = recipe.Fat,
                    Carbs = recipe.Carbs
                };
                
                _cloudRecipes.TryAdd(key, data);
            }
            
            Log($"Fetched {cloudRecipes.Count} recipes from cloud");
        }
        catch (Exception ex)
        {
            Log($"Error fetching cloud recipes: {ex.Message}");
        }
    }

    /// <summary>
    /// Compares local sync queue with cached cloud data and removes duplicates.
    /// Order: Ingredients first, then Recipes (as per requirement).
    /// </summary>
    public async Task<int> DeduplicateSyncQueueAsync(CancellationToken ct = default)
    {
        if (!_isCachePopulated)
        {
            Log("DeduplicateSyncQueueAsync: Cache not populated, fetching first...");
            await FetchCloudDataAsync(ct);
        }

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
        {
            Log("DeduplicateSyncQueueAsync: No active account");
            return 0;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sw = Stopwatch.StartNew();
        int removedCount = 0;

        try
        {
            // Get pending entries for Ingredient and Recipe types
            var pendingEntries = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value 
                    && e.Status == SyncEntryStatus.Pending
                    && (e.EntityType == "Ingredient" || e.EntityType == "Recipe")
                    && e.OperationType == SyncOperationType.Insert)
                .ToListAsync(ct);

            Log($"Checking {pendingEntries.Count} pending entries for duplicates...");

            // Process Ingredients first
            var ingredientEntries = pendingEntries.Where(e => e.EntityType == "Ingredient").ToList();
            removedCount += await ProcessIngredientEntriesAsync(db, ingredientEntries, ct);

            // Then process Recipes
            var recipeEntries = pendingEntries.Where(e => e.EntityType == "Recipe").ToList();
            removedCount += await ProcessRecipeEntriesAsync(db, recipeEntries, ct);

            if (removedCount > 0)
            {
                await db.SaveChangesAsync(ct);
            }

            sw.Stop();
            Log($"Deduplication complete in {sw.ElapsedMilliseconds}ms: removed {removedCount} duplicates");
        }
        catch (Exception ex)
        {
            Log($"Error during deduplication: {ex.Message}");
        }

        return removedCount;
    }

    private async Task<int> ProcessIngredientEntriesAsync(
        AppDbContext db, 
        List<SyncQueueEntry> entries, 
        CancellationToken ct)
    {
        int removed = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

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

                // Check if exists in cloud cache (case-sensitive name match)
                if (_cloudIngredients.TryGetValue(localIngredient.Name ?? string.Empty, out var cloudData))
                {
                    // Compare all macro values (must ALL match)
                    if (IsMacroMatch(
                        localIngredient.Calories, cloudData.Calories,
                        localIngredient.Protein, cloudData.Protein,
                        localIngredient.Fat, cloudData.Fat,
                        localIngredient.Carbs, cloudData.Carbs))
                    {
                        // Duplicate found - mark as completed (skip sending)
                        entry.Status = SyncEntryStatus.Completed;
                        entry.LastError = "Duplicate found in cloud - skipped";
                        entry.SyncedUtc = DateTime.UtcNow;
                        removed++;
                        
                        Log($"DUPLICATE: Ingredient '{localIngredient.Name}' already exists in cloud");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error processing ingredient entry {entry.EntityId}: {ex.Message}");
            }
        }

        return removed;
    }

    private async Task<int> ProcessRecipeEntriesAsync(
        AppDbContext db, 
        List<SyncQueueEntry> entries, 
        CancellationToken ct)
    {
        int removed = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

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

                // Check if exists in cloud cache (case-sensitive name match)
                if (_cloudRecipes.TryGetValue(localRecipe.Name ?? string.Empty, out var cloudData))
                {
                    // Compare all macro values (must ALL match)
                    if (IsMacroMatch(
                        localRecipe.Calories, cloudData.Calories,
                        localRecipe.Protein, cloudData.Protein,
                        localRecipe.Fat, cloudData.Fat,
                        localRecipe.Carbs, cloudData.Carbs))
                    {
                        // Duplicate found - mark as completed (skip sending)
                        entry.Status = SyncEntryStatus.Completed;
                        entry.LastError = "Duplicate found in cloud - skipped";
                        entry.SyncedUtc = DateTime.UtcNow;
                        removed++;
                        
                        Log($"DUPLICATE: Recipe '{localRecipe.Name}' already exists in cloud");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error processing recipe entry {entry.EntityId}: {ex.Message}");
            }
        }

        return removed;
    }

    /// <summary>
    /// Compares macro values with tolerance for floating point precision.
    /// All values must match for this to return true.
    /// </summary>
    private static bool IsMacroMatch(
        double localCal, double cloudCal,
        double localProt, double cloudProt,
        double localFat, double cloudFat,
        double localCarbs, double cloudCarbs)
    {
        const double tolerance = 0.01; // Small tolerance for floating point

        return Math.Abs(localCal - cloudCal) < tolerance
            && Math.Abs(localProt - cloudProt) < tolerance
            && Math.Abs(localFat - cloudFat) < tolerance
            && Math.Abs(localCarbs - cloudCarbs) < tolerance;
    }

    public void ClearCache()
    {
        _cloudIngredients.Clear();
        _cloudRecipes.Clear();
        _isCachePopulated = false;
        Log("Cloud data cache cleared");
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
