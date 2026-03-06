using FoodbookApp.Models;
using System.ComponentModel.DataAnnotations;

namespace Foodbook.Models;

public class SyncState
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public bool IsCloudSyncEnabled { get; set; }
    public bool InitialSyncCompleted { get; set; }
    public DateTime? InitialSyncStartedUtc { get; set; }
    public DateTime? InitialSyncCompletedUtc { get; set; }
    public int PendingItemsCount { get; set; }
    public int TotalItemsSynced { get; set; }
    public string? LastKnownServerHash { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public DateTime? LastSyncAttemptUtc { get; set; }
    public DateTime? LastCloudPollUtc { get; set; }
    public string? LastSyncError { get; set; }
    public SyncStatus Status { get; set; }
    public SyncPriority Priority { get; set; } = SyncPriority.Local;
    public int SyncIntervalMinutes { get; set; } = 5;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public AuthAccount? Account { get; set; }
}

public enum SyncStatus { Disabled = 0, Idle = 1, InitialSync = 2, Syncing = 3, Error = 4 }

public enum SyncPriority 
{ 
    /// <summary>Local data takes precedence - upload first, then poll cloud</summary>
    Local = 0, 
    /// <summary>Cloud data takes precedence - download first, then upload local-only</summary>
    Cloud = 1 
}
