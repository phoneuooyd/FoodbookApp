namespace FoodbookApp.Interfaces;

/// <summary>
/// Service for deduplicating entities during sync operations.
/// Prevents duplicate data by comparing entities by name + macros (case-sensitive).
/// 
/// MODES:
/// 1. CloudFirst: When importing cloud data, detect local duplicates and replace local IDs 
///    with cloud IDs so the cloud entity "wins" without creating a second row.
/// 2. LocalFirst: Before uploading local data, detect cloud duplicates and update cloud 
///    entities to match local IDs (PATCH) so we don't create duplicates in the cloud.
/// </summary>
public interface IDeduplicationService
{
    /// <summary>
    /// Fetches cloud data (ingredients, recipes) and caches in memory.
    /// Should be called on login/sync enable.
    /// </summary>
    Task FetchCloudDataAsync(CancellationToken ct = default);

    /// <summary>
    /// CLOUD-FIRST deduplication: Before importing cloud entities into local DB,
    /// finds local entities that match cloud entities by name + macros.
    /// For each match, replaces the local entity with the cloud entity (cloud ID wins).
    /// Returns the number of local entities that were replaced/merged.
    /// </summary>
    Task<int> DeduplicateForCloudFirstAsync(
        Foodbook.Data.AppDbContext db,
        CloudDataSnapshot cloudData,
        CancellationToken ct = default);

    /// <summary>
    /// LOCAL-FIRST deduplication: Before uploading local entities to cloud,
    /// finds cloud entities that match local entities by name + macros.
    /// For each match, updates the cloud entity to use the local entity's ID (PATCH),
    /// so we don't upload a duplicate. Returns the number of cloud entities updated.
    /// </summary>
    Task<int> DeduplicateForLocalFirstAsync(
        Foodbook.Data.AppDbContext db,
        CancellationToken ct = default);

    /// <summary>
    /// Legacy: Compares local sync queue with cached cloud data and removes duplicates.
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
