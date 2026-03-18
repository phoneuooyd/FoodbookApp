using Foodbook.Models;
using Foodbook.Models.DTOs;

namespace FoodbookApp.Interfaces;

public interface ISupabaseSyncService : IDisposable
{
    event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;
    event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    Task<SyncState?> GetSyncStateAsync(CancellationToken ct = default);
    Task<bool> IsCloudSyncEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// Enables cloud sync. First call performs smart merge (no user dialog needed).
    /// Subsequent calls resume queue processing.
    /// </summary>
    Task EnableCloudSyncAsync(CancellationToken ct = default);
    Task DisableCloudSyncAsync(CancellationToken ct = default);

    Task QueueForSyncAsync<T>(T entity, SyncOperationType operation, CancellationToken ct = default) where T : class;
    Task QueueBatchForSyncAsync<T>(IEnumerable<T> entities, SyncOperationType operation, CancellationToken ct = default) where T : class;

    Task<SyncResult> ProcessQueueAsync(CancellationToken ct = default);
    Task<SyncResult> ForceSyncAllAsync(CancellationToken ct = default);
    Task<SyncResult> ForceDownloadFromCloudAsync(CancellationToken ct = default);

    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task RetryFailedEntriesAsync(CancellationToken ct = default);
    Task ClearFailedEntriesAsync(CancellationToken ct = default);

    Task<bool> LoadUserPreferencesFromCloudAsync(Guid userId, CancellationToken ct = default);
    Task SaveUserPreferencesToCloudAsync(Guid userId, CancellationToken ct = default);
    bool ApplyUserPreferencesFromSnapshot(UserPreferencesDto cloudPrefs);
}

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

public class SyncStatusChangedEventArgs : EventArgs
{
    public SyncStatus OldStatus { get; init; }
    public SyncStatus NewStatus { get; init; }
    public string? Message { get; init; }
}

public class SyncProgressEventArgs : EventArgs
{
    public int TotalItems { get; init; }
    public int ProcessedItems { get; init; }
    public int FailedItems { get; init; }
    public double ProgressPercentage => TotalItems > 0 ? (double)ProcessedItems / TotalItems * 100 : 0;
    public string? CurrentEntityType { get; init; }
}
