namespace FoodbookApp.Interfaces;

/// <summary>
/// Service for deduplicating sync queue entries against cloud data.
/// Prevents sending items that already exist in Supabase.
/// </summary>
public interface IDeduplicationService
{
    /// <summary>
    /// Fetches cloud data (ingredients, recipes) and caches in memory.
    /// Should be called on login/sync enable.
    /// </summary>
    Task FetchCloudDataAsync(CancellationToken ct = default);

    /// <summary>
    /// Compares local sync queue with cached cloud data and removes duplicates.
    /// Returns number of entries removed from queue.
    /// MODE: Local-First Priority - prevents uploading duplicates to cloud.
    /// </summary>
    Task<int> DeduplicateSyncQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Filters cloud ingredients to exclude those that already exist locally with matching name and macros.
    /// Returns a filtered list of ingredients that should be imported.
    /// MODE: Cloud-First Priority - prevents importing duplicates from cloud.
    /// </summary>
    Task<List<Foodbook.Models.Ingredient>> FilterCloudIngredientsForImportAsync(
        List<Foodbook.Models.Ingredient> cloudIngredients, 
        CancellationToken ct = default);

    /// <summary>
    /// Clears the in-memory cloud data cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Indicates if cloud data has been fetched and cached.
    /// </summary>
    bool IsCachePopulated { get; }

    /// <summary>
    /// Indicates if local base ingredients have been fetched and cached.
    /// </summary>
    bool IsLocalCachePopulated { get; }
}
