using System.ComponentModel.DataAnnotations;

namespace Foodbook.Models;

/// <summary>
/// Tracks the synchronization state for a user account.
/// One record per AuthAccount that has cloud sync enabled.
/// </summary>
public class SyncState
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The ID of the AuthAccount this sync state belongs to
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Whether cloud synchronization is enabled for this account
    /// </summary>
    public bool IsCloudSyncEnabled { get; set; }

    /// <summary>
    /// Whether initial synchronization has been completed
    /// </summary>
    public bool InitialSyncCompleted { get; set; }

    /// <summary>
    /// When initial sync was started
    /// </summary>
    public DateTime? InitialSyncStartedUtc { get; set; }

    /// <summary>
    /// When initial sync was completed
    /// </summary>
    public DateTime? InitialSyncCompletedUtc { get; set; }

    /// <summary>
    /// When the last successful sync occurred
    /// </summary>
    public DateTime? LastSyncUtc { get; set; }

    /// <summary>
    /// When the last sync attempt occurred (successful or not)
    /// </summary>
    public DateTime? LastSyncAttemptUtc { get; set; }

    /// <summary>
    /// Last error message if sync failed
    /// </summary>
    [MaxLength(1000)]
    public string? LastSyncError { get; set; }

    /// <summary>
    /// Total number of items successfully synced
    /// </summary>
    public int TotalItemsSynced { get; set; }

    /// <summary>
    /// Number of items currently pending in the queue
    /// </summary>
    public int PendingItemsCount { get; set; }

    /// <summary>
    /// Current sync status
    /// </summary>
    public SyncStatus Status { get; set; } = SyncStatus.Idle;

    /// <summary>
    /// Version hash of the last known server state (for conflict detection)
    /// </summary>
    [MaxLength(64)]
    public string? LastKnownServerHash { get; set; }

    /// <summary>
    /// Sync interval in minutes (default: 5 minutes)
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// When this sync state was created
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this sync state was last updated
    /// </summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual AuthAccount? Account { get; set; }
}

/// <summary>
/// Current status of the sync service for a user
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Sync is not active, waiting for next interval
    /// </summary>
    Idle = 0,

    /// <summary>
    /// Currently performing sync operations
    /// </summary>
    Syncing = 1,

    /// <summary>
    /// Performing initial synchronization (uploading all data)
    /// </summary>
    InitialSync = 2,

    /// <summary>
    /// Sync is paused (e.g., no network, user disabled)
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Sync encountered an error
    /// </summary>
    Error = 4,

    /// <summary>
    /// User has not enabled cloud sync
    /// </summary>
    Disabled = 5
}
