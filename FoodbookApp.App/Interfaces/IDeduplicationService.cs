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
    /// </summary>
    Task<int> DeduplicateSyncQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears the in-memory cloud data cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Indicates if cloud data has been fetched and cached.
    /// </summary>
    bool IsCachePopulated { get; }
}
