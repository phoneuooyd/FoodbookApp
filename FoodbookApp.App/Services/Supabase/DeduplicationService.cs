using System.Collections.Concurrent;
using System.Diagnostics;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Services.Supabase;

/// <summary>
/// Service for deduplicating entities during sync operations.
/// Prevents duplicate data by comparing entities by name (case-sensitive) + macros/kcal.
/// 
/// CLOUD-FIRST: Replaces local entities with matching cloud entities (cloud ID wins).
///   - For Ingredient: match by Name (exact) + Calories + Protein + Fat + Carbs
///   - For Recipe: match by Name (exact) + Calories + Protein + Fat + Carbs + same ingredient count + matching ingredient names
/// 
/// LOCAL-FIRST: Removes cloud duplicates before uploading, so local entities replace cloud ones.
///   - Same matching criteria as CloudFirst but in reverse direction.
/// </summary>
public sealed class DeduplicationService : IDeduplicationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISupabaseCrudService _crudService;
    private readonly IAuthTokenStore _tokenStore;

    // In-memory cache for cloud data
    private readonly ConcurrentDictionary<string, List<CachedIngredient>> _cloudIngredients = new();
    private readonly ConcurrentDictionary<string, List<CachedRecipe>> _cloudRecipes = new();

    private volatile bool _isCachePopulated;
    private volatile bool _fetchAttempted;
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    private const double MacroTolerance = 0.1;

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

    #region Cache Management

    public async Task<bool> FetchCloudDataAsync(CancellationToken ct = default)
    {
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

            ClearCacheInternal();

            bool ingredientsFetched = false;
            bool recipesFetched = false;

            try
            {
                var cloudIngredients = await _crudService.GetIngredientsAsync(ct);
                if (cloudIngredients != null)
                {
                    foreach (var ing in cloudIngredients)
                    {
                        if (ing == null) continue;
                        var key = (ing.Name ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(key)) continue;

                        var cached = new CachedIngredient
                        {
                            Id = ing.Id,
                            Name = key,
                            Calories = ing.Calories,
                            Protein = ing.Protein,
                            Fat = ing.Fat,
                            Carbs = ing.Carbs,
                            RecipeId = ing.RecipeId
                        };

                        _cloudIngredients.AddOrUpdate(
                            key,
                            _ => [cached],
                            (_, existing) => { existing.Add(cached); return existing; });
                    }
                }
                ingredientsFetched = true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log($"WARNING: Failed to fetch ingredients: {ex.Message}"); }

            try
            {
                var cloudRecipes = await _crudService.GetRecipesAsync(ct);
                if (cloudRecipes != null)
                {
                    foreach (var recipe in cloudRecipes)
                    {
                        if (recipe == null) continue;
                        var key = (recipe.Name ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(key)) continue;

                        var cached = new CachedRecipe
                        {
                            Id = recipe.Id,
                            Name = key,
                            Calories = recipe.Calories,
                            Protein = recipe.Protein,
                            Fat = recipe.Fat,
                            Carbs = recipe.Carbs
                        };

                        _cloudRecipes.AddOrUpdate(
                            key,
                            _ => [cached],
                            (_, existing) => { existing.Add(cached); return existing; });
                    }
                }
                recipesFetched = true;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log($"WARNING: Failed to fetch recipes: {ex.Message}"); }

            _fetchAttempted = true;
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
            _fetchAttempted = true;
            return false;
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    // Explicit interface implementation
    async Task IDeduplicationService.FetchCloudDataAsync(CancellationToken ct)
    {
        await FetchCloudDataAsync(ct);
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

    #endregion

    #region CloudFirst Deduplication

    /// <summary>
    /// CLOUD-FIRST: Before importing cloud data into local DB, find local duplicates and
    /// remove them so the cloud import creates entities with cloud IDs.
    /// 
    /// Algorithm:
    /// 1. For each cloud ingredient, find local match by Name(exact) + macros
    /// 2. If match found with DIFFERENT ID: delete old local row (cloud import will create with cloud ID)
    /// 3. For each cloud recipe, find local match by Name(exact) + macros + same ingredient names
    /// 4. If match found with DIFFERENT ID: remap PlannedMeals, remove local recipe + ingredients
    /// </summary>
    public async Task<int> DeduplicateForCloudFirstAsync(
        AppDbContext db, CloudDataSnapshot cloudData, CancellationToken ct = default)
    {
        int mergedCount = 0;
        var sw = Stopwatch.StartNew();

        try
        {
            Log("=== DeduplicateForCloudFirstAsync START ===");

            // --- INGREDIENTS ---
            var cloudBaseIngredients = cloudData.Ingredients
                .Where(i => i.RecipeId == null)
                .ToList();

            if (cloudBaseIngredients.Count > 0)
            {
                var localIngredients = await db.Ingredients
                    .Where(i => i.RecipeId == null)
                    .ToListAsync(ct);

                // Group by name for fast lookup (case-sensitive)
                var localByName = localIngredients
                    .GroupBy(i => i.Name?.Trim() ?? string.Empty)
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var cloudIng in cloudBaseIngredients)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = (cloudIng.Name ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(name)) continue;

                    if (!localByName.TryGetValue(name, out var localMatches)) continue;

                    var allMatches = localMatches.Where(l =>
                        l.Id != cloudIng.Id &&
                        IsMacroMatch(l.Calories, cloudIng.Calories,
                                     l.Protein, cloudIng.Protein,
                                     l.Fat, cloudIng.Fat,
                                     l.Carbs, cloudIng.Carbs))
                        .ToList();

                    foreach (var match in allMatches)
                    {
                        Log($"  MERGE ingredient: local '{match.Name}' ({match.Id}) -> cloud ({cloudIng.Id})");
                        db.Ingredients.Remove(match);
                        mergedCount++;
                    }
                }

                if (mergedCount > 0)
                    await db.SaveChangesAsync(ct);

                Log($"  Ingredients merged: {mergedCount}");
            }

            // --- RECIPES ---
            int recipeMerged = 0;
            if (cloudData.Recipes.Count > 0)
            {
                // Build cloud ingredient lookup: RecipeId -> list of cloud ingredients
                var cloudRecipeIngredients = cloudData.Ingredients
                    .Where(i => i.RecipeId != null)
                    .GroupBy(i => i.RecipeId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var localRecipes = await db.Recipes
                    .Include(r => r.Ingredients)
                    .ToListAsync(ct);

                var localByName = localRecipes
                    .GroupBy(r => r.Name?.Trim() ?? string.Empty)
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var cloudRecipe in cloudData.Recipes)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = (cloudRecipe.Name ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(name)) continue;

                    if (!localByName.TryGetValue(name, out var localMatches)) continue;

                    cloudRecipeIngredients.TryGetValue(cloudRecipe.Id, out var cloudIngs);
                    var cloudIngCount = cloudIngs?.Count ?? 0;

                    var allMatches = localMatches.Where(localR =>
                    {
                        if (localR.Id == cloudRecipe.Id)
                            return false;

                        if (!IsMacroMatch(localR.Calories, cloudRecipe.Calories,
                                          localR.Protein, cloudRecipe.Protein,
                                          localR.Fat, cloudRecipe.Fat,
                                          localR.Carbs, cloudRecipe.Carbs))
                            return false;

                        var localIngCount = localR.Ingredients?.Count ?? 0;
                        if (localIngCount != cloudIngCount)
                            return false;

                        // Verify ingredient names match (case-sensitive, sorted)
                        if (cloudIngs != null && localR.Ingredients != null && cloudIngCount > 0)
                        {
                            var cloudIngNames = cloudIngs
                                .Select(ci => ci.Name?.Trim() ?? string.Empty)
                                .OrderBy(n => n, StringComparer.Ordinal)
                                .ToList();
                            var localIngNames = localR.Ingredients
                                .Select(li => li.Name?.Trim() ?? string.Empty)
                                .OrderBy(n => n, StringComparer.Ordinal)
                                .ToList();

                            if (!cloudIngNames.SequenceEqual(localIngNames, StringComparer.Ordinal))
                                return false;
                        }

                        return true;
                    }).ToList();

                    foreach (var match in allMatches)
                    {
                        Log($"  MERGE recipe: local '{match.Name}' ({match.Id}) -> cloud ({cloudRecipe.Id})");

                        // Remap PlannedMeals from local recipe ID to cloud recipe ID
                        var affectedMeals = await db.PlannedMeals
                            .Where(pm => pm.RecipeId == match.Id)
                            .ToListAsync(ct);
                        foreach (var meal in affectedMeals)
                        {
                            meal.RecipeId = cloudRecipe.Id;
                            Log($"    Remapped PlannedMeal {meal.Id} -> recipe {cloudRecipe.Id}");
                        }

                        // Remove local recipe's ingredients (cloud import will create new ones)
                        if (match.Ingredients != null)
                            db.Ingredients.RemoveRange(match.Ingredients);

                        db.Recipes.Remove(match);
                        recipeMerged++;
                    }
                }

                if (recipeMerged > 0)
                    await db.SaveChangesAsync(ct);

                Log($"  Recipes merged: {recipeMerged}");
                mergedCount += recipeMerged;
            }

            sw.Stop();
            Log($"=== DeduplicateForCloudFirstAsync COMPLETE: {mergedCount} entities merged in {sw.ElapsedMilliseconds}ms ===");
        }
        catch (OperationCanceledException)
        {
            Log("DeduplicateForCloudFirstAsync: Cancelled");
        }
        catch (Exception ex)
        {
            Log($"ERROR in DeduplicateForCloudFirstAsync: {ex.Message}");
        }

        return mergedCount;
    }

    #endregion

    #region LocalFirst Deduplication

    /// <summary>
    /// LOCAL-FIRST: Before uploading local entities, find cloud duplicates.
    /// For each match with different ID, delete the cloud duplicate so the local upload
    /// creates the entity with local ID. For same-ID matches, mark as completed.
    /// </summary>
    public async Task<int> DeduplicateForLocalFirstAsync(
        AppDbContext db, CancellationToken ct = default)
    {
        int deduplicatedCount = 0;
        var sw = Stopwatch.StartNew();

        try
        {
            Log("=== DeduplicateForLocalFirstAsync START ===");

            if (!_isCachePopulated)
            {
                var fetched = await FetchCloudDataAsync(ct);
                if (!fetched)
                {
                    Log("DeduplicateForLocalFirstAsync: Could not fetch cloud data, skipping");
                    return 0;
                }
            }

            if (_cloudIngredients.Count == 0 && _cloudRecipes.Count == 0)
            {
                Log("DeduplicateForLocalFirstAsync: Cloud is empty (new user), no deduplication needed");
                return 0;
            }

            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
            {
                Log("DeduplicateForLocalFirstAsync: No active account");
                return 0;
            }

            // --- INGREDIENTS ---
            var pendingIngredientEntries = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value
                    && e.Status == SyncEntryStatus.Pending
                    && e.EntityType == "Ingredient"
                    && e.OperationType == SyncOperationType.Insert)
                .ToListAsync(ct);

            if (pendingIngredientEntries.Count > 0 && _cloudIngredients.Count > 0)
            {
                foreach (var entry in pendingIngredientEntries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Payload)) continue;

                    try
                    {
                        var localIng = System.Text.Json.JsonSerializer.Deserialize<Ingredient>(
                            entry.Payload,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (localIng == null) continue;

                        var name = (localIng.Name ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(name)) continue;
                        if (localIng.RecipeId != null) continue;

                        if (_cloudIngredients.TryGetValue(name, out var cloudList))
                        {
                            var matches = cloudList
                                .Where(c => c.RecipeId == null && IsMacroMatch(
                                    localIng.Calories, c.Calories,
                                    localIng.Protein, c.Protein,
                                    localIng.Fat, c.Fat,
                                    localIng.Carbs, c.Carbs))
                                .ToList();

                            bool sameIdFound = false;
                            foreach (var cloudData in matches)
                            {
                                if (entry.EntityId == cloudData.Id)
                                {
                                    sameIdFound = true;
                                    continue;
                                }

                                try
                                {
                                    Log($"  LOCAL-FIRST dedup ingredient: local '{name}' ({entry.EntityId}) matches cloud ({cloudData.Id})");
                                    await _crudService.DeleteIngredientAsync(cloudData.Id, ct);
                                    Log($"    Deleted cloud duplicate {cloudData.Id}");
                                    deduplicatedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Log($"    WARNING: Failed to dedup cloud ingredient {cloudData.Id}: {ex.Message}");
                                }
                            }

                            if (sameIdFound && matches.Count == 1)
                            {
                                entry.Status = SyncEntryStatus.Completed;
                                entry.LastError = "Already exists in cloud with same ID";
                                entry.SyncedUtc = DateTime.UtcNow;
                                deduplicatedCount++;
                                Log($"  SKIP ingredient '{name}' - already in cloud with same ID");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"WARNING: Error processing ingredient entry {entry.EntityId}: {ex.Message}");
                    }
                }
            }

            // --- RECIPES ---
            var pendingRecipeEntries = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value
                    && e.Status == SyncEntryStatus.Pending
                    && e.EntityType == "Recipe"
                    && e.OperationType == SyncOperationType.Insert)
                .ToListAsync(ct);

            if (pendingRecipeEntries.Count > 0 && _cloudRecipes.Count > 0)
            {
                foreach (var entry in pendingRecipeEntries)
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(entry.Payload)) continue;

                    try
                    {
                        var localRecipe = System.Text.Json.JsonSerializer.Deserialize<Recipe>(
                            entry.Payload,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (localRecipe == null) continue;

                        var name = (localRecipe.Name ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(name)) continue;

                        if (_cloudRecipes.TryGetValue(name, out var cloudList))
                        {
                            var matches = cloudList
                                .Where(c => IsMacroMatch(
                                    localRecipe.Calories, c.Calories,
                                    localRecipe.Protein, c.Protein,
                                    localRecipe.Fat, c.Fat,
                                    localRecipe.Carbs, c.Carbs))
                                .ToList();

                            bool sameIdFound = false;
                            foreach (var cloudRecipeData in matches)
                            {
                                if (entry.EntityId == cloudRecipeData.Id)
                                {
                                    sameIdFound = true;
                                    continue;
                                }

                                try
                                {
                                    Log($"  LOCAL-FIRST dedup recipe: local '{name}' ({entry.EntityId}) matches cloud ({cloudRecipeData.Id})");
                                    await _crudService.DeleteRecipeAsync(cloudRecipeData.Id, ct);
                                    Log($"    Deleted cloud duplicate recipe {cloudRecipeData.Id}");
                                    deduplicatedCount++;
                                }
                                catch (Exception ex)
                                {
                                    Log($"    WARNING: Failed to dedup cloud recipe {cloudRecipeData.Id}: {ex.Message}");
                                }
                            }

                            if (sameIdFound && matches.Count == 1)
                            {
                                entry.Status = SyncEntryStatus.Completed;
                                entry.LastError = "Already exists in cloud with same ID";
                                entry.SyncedUtc = DateTime.UtcNow;
                                deduplicatedCount++;
                                Log($"  SKIP recipe '{name}' - already in cloud with same ID");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"WARNING: Error processing recipe entry {entry.EntityId}: {ex.Message}");
                    }
                }
            }

            if (deduplicatedCount > 0)
                await db.SaveChangesAsync(ct);

            sw.Stop();
            Log($"=== DeduplicateForLocalFirstAsync COMPLETE: {deduplicatedCount} entities deduplicated in {sw.ElapsedMilliseconds}ms ===");
        }
        catch (OperationCanceledException)
        {
            Log("DeduplicateForLocalFirstAsync: Cancelled");
        }
        catch (Exception ex)
        {
            Log($"ERROR in DeduplicateForLocalFirstAsync: {ex.Message}");
        }

        return deduplicatedCount;
    }

    #endregion

    #region Legacy Queue Deduplication

    /// <summary>
    /// Legacy deduplication: marks sync queue entries as Completed if they already exist in cloud.
    /// </summary>
    public async Task<int> DeduplicateSyncQueueAsync(CancellationToken ct = default)
    {
        try
        {
            if (!_isCachePopulated && !_fetchAttempted)
            {
                var fetchResult = await FetchCloudDataAsync(ct);
                if (!fetchResult)
                {
                    Log("DeduplicateSyncQueueAsync: Fetch failed, skipping");
                    return 0;
                }
            }

            if (_cloudIngredients.Count == 0 && _cloudRecipes.Count == 0)
            {
                Log("DeduplicateSyncQueueAsync: Cloud is empty, no duplicates to check");
                return 0;
            }

            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue) return 0;

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pendingEntries = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value
                    && e.Status == SyncEntryStatus.Pending
                    && (e.EntityType == "Ingredient" || e.EntityType == "Recipe")
                    && e.OperationType == SyncOperationType.Insert)
                .ToListAsync(ct);

            if (pendingEntries.Count == 0) return 0;

            int removedCount = 0;

            foreach (var entry in pendingEntries)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(entry.Payload)) continue;

                try
                {
                    if (entry.EntityType == "Ingredient")
                    {
                        var ing = System.Text.Json.JsonSerializer.Deserialize<Ingredient>(
                            entry.Payload,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (ing == null) continue;

                        var name = (ing.Name ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(name)) continue;

                        if (_cloudIngredients.TryGetValue(name, out var cloudList) &&
                            cloudList.Any(c => IsMacroMatch(ing.Calories, c.Calories, ing.Protein, c.Protein,
                                         ing.Fat, c.Fat, ing.Carbs, c.Carbs)))
                        {
                            entry.Status = SyncEntryStatus.Completed;
                            entry.LastError = "Duplicate found in cloud - skipped upload";
                            entry.SyncedUtc = DateTime.UtcNow;
                            removedCount++;
                        }
                    }
                    else if (entry.EntityType == "Recipe")
                    {
                        var recipe = System.Text.Json.JsonSerializer.Deserialize<Recipe>(
                            entry.Payload,
                            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (recipe == null) continue;

                        var name = (recipe.Name ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(name)) continue;

                        if (_cloudRecipes.TryGetValue(name, out var cloudList) &&
                            cloudList.Any(c => IsMacroMatch(recipe.Calories, c.Calories, recipe.Protein, c.Protein,
                                         recipe.Fat, c.Fat, recipe.Carbs, c.Carbs)))
                        {
                            entry.Status = SyncEntryStatus.Completed;
                            entry.LastError = "Duplicate found in cloud - skipped upload";
                            entry.SyncedUtc = DateTime.UtcNow;
                            removedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"WARNING: Error processing entry {entry.EntityId}: {ex.Message}");
                }
            }

            if (removedCount > 0)
                await db.SaveChangesAsync(ct);

            Log($"DeduplicateSyncQueueAsync: removed {removedCount} duplicates");
            return removedCount;
        }
        catch (Exception ex)
        {
            Log($"ERROR in DeduplicateSyncQueueAsync: {ex.Message}");
            return 0;
        }
    }

    #endregion

    #region Matching Logic

    private static bool IsMacroMatch(
        double cal1, double cal2,
        double prot1, double prot2,
        double fat1, double fat2,
        double carbs1, double carbs2)
    {
        return Math.Abs(cal1 - cal2) < MacroTolerance
            && Math.Abs(prot1 - prot2) < MacroTolerance
            && Math.Abs(fat1 - fat2) < MacroTolerance
            && Math.Abs(carbs1 - carbs2) < MacroTolerance;
    }

    #endregion

    #region Logging

    private static void Log(string message)
        => Debug.WriteLine($"[DeduplicationService] {message}");

    #endregion

    #region Cache Models

    private readonly record struct CachedIngredient
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public double Calories { get; init; }
        public double Protein { get; init; }
        public double Fat { get; init; }
        public double Carbs { get; init; }
        public Guid? RecipeId { get; init; }
    }

    private readonly record struct CachedRecipe
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
