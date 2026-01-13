using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models;

/// <summary>
/// Represents a single entry in the synchronization queue.
/// Each entry corresponds to a change (insert, update, delete) that needs to be synced to Supabase.
/// </summary>
public class SyncQueueEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The ID of the AuthAccount that owns this sync entry
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Type of entity being synchronized (e.g., "Recipe", "Ingredient", "Folder")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the entity being synchronized
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// The type of operation: Insert, Update, Delete
    /// </summary>
    public SyncOperationType OperationType { get; set; }

    /// <summary>
    /// JSON serialized payload of the entity data (for Insert/Update operations)
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// When this entry was created (queued)
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of retry attempts for this entry
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Last error message if sync failed
    /// </summary>
    [MaxLength(1000)]
    public string? LastError { get; set; }

    /// <summary>
    /// Current status of this sync entry
    /// </summary>
    public SyncEntryStatus Status { get; set; } = SyncEntryStatus.Pending;

    /// <summary>
    /// When this entry was last attempted to sync
    /// </summary>
    public DateTime? LastAttemptUtc { get; set; }

    /// <summary>
    /// When this entry was successfully synced
    /// </summary>
    public DateTime? SyncedUtc { get; set; }

    /// <summary>
    /// Priority of this entry (lower = higher priority). Initial sync entries have priority 0.
    /// </summary>
    public int Priority { get; set; } = 10;

    /// <summary>
    /// Batch ID for grouping related sync operations (e.g., initial sync)
    /// </summary>
    public Guid? BatchId { get; set; }
}

/// <summary>
/// Type of sync operation
/// </summary>
public enum SyncOperationType
{
    Insert = 0,
    Update = 1,
    Delete = 2
}

/// <summary>
/// Status of a sync queue entry
/// </summary>
public enum SyncEntryStatus
{
    /// <summary>
    /// Entry is waiting to be processed
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Entry is currently being processed
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Entry was successfully synced
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Entry failed to sync (will be retried)
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Entry permanently failed after max retries
    /// </summary>
    Abandoned = 4
}
