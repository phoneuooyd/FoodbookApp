using Foodbook.Models;

namespace FoodbookApp.Interfaces;

/// <summary>
/// Service for synchronizing local data with Supabase cloud.
/// Manages the sync queue, handles initial synchronization, and processes incremental changes.
/// </summary>
public interface ISupabaseSyncService
{
    /// <summary>
    /// Gets the current sync state for the active user
    /// </summary>
    Task<SyncState?> GetSyncStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Enables cloud synchronization for the current user.
    /// Triggers initial sync if not already completed.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task EnableCloudSyncAsync(CancellationToken ct = default);

    /// <summary>
    /// Disables cloud synchronization for the current user.
    /// Stops the sync timer but preserves the queue.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task DisableCloudSyncAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if cloud sync is enabled for the current user
    /// </summary>
    Task<bool> IsCloudSyncEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Queues an entity for synchronization.
    /// Called by services when data changes locally.
    /// </summary>
    /// <typeparam name="T">Type of entity</typeparam>
    /// <param name="entity">The entity that changed</param>
    /// <param name="operation">Type of operation (Insert, Update, Delete)</param>
    /// <param name="ct">Cancellation token</param>
    Task QueueForSyncAsync<T>(T entity, SyncOperationType operation, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Queues multiple entities for synchronization in a single batch.
    /// </summary>
    /// <typeparam name="T">Type of entities</typeparam>
    /// <param name="entities">The entities that changed</param>
    /// <param name="operation">Type of operation</param>
    /// <param name="ct">Cancellation token</param>
    Task QueueBatchForSyncAsync<T>(IEnumerable<T> entities, SyncOperationType operation, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Triggers initial synchronization - uploads all local data to Supabase.
    /// This is called when user first enables cloud sync.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task<bool> StartInitialSyncAsync(CancellationToken ct = default);

    /// <summary>
    /// Processes pending items in the sync queue.
    /// Called automatically by the timer or manually for immediate sync.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task<SyncResult> ProcessQueueAsync(CancellationToken ct = default);

    /// <summary>
    /// Forces immediate synchronization of all pending items.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task<SyncResult> ForceSyncAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the number of pending items in the sync queue
    /// </summary>
    Task<int> GetPendingCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears failed/abandoned entries from the queue
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task ClearFailedEntriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retries all failed entries
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task RetryFailedEntriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Event raised when sync status changes
    /// </summary>
    event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;

    /// <summary>
    /// Event raised when sync progress updates
    /// </summary>
    event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    /// <summary>
    /// Starts the background sync timer
    /// </summary>
    void StartSyncTimer();

    /// <summary>
    /// Stops the background sync timer
    /// </summary>
    void StopSyncTimer();
}

/// <summary>
/// Result of a sync operation
/// </summary>
public record SyncResult
{
    public bool Success { get; init; }
    public int ItemsProcessed { get; init; }
    public int ItemsFailed { get; init; }
    public int ItemsRemaining { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    public static SyncResult Ok(int processed, int remaining, TimeSpan duration) => new()
    {
        Success = true,
        ItemsProcessed = processed,
        ItemsRemaining = remaining,
        Duration = duration
    };

    public static SyncResult Error(string message, int failed = 0) => new()
    {
        Success = false,
        ItemsFailed = failed,
        ErrorMessage = message
    };
}

/// <summary>
/// Event args for sync status changes
/// </summary>
public class SyncStatusChangedEventArgs : EventArgs
{
    public SyncStatus OldStatus { get; init; }
    public SyncStatus NewStatus { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Event args for sync progress updates
/// </summary>
public class SyncProgressEventArgs : EventArgs
{
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int FailedItems { get; init; }
    public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    public string? CurrentEntityType { get; init; }
}
