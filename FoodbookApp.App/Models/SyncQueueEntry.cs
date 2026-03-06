using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models;

public class SyncQueueEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    [Required]
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public SyncOperationType OperationType { get; set; }
    public string? Payload { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    [MaxLength(1000)]
    public string? LastError { get; set; }
    public SyncEntryStatus Status { get; set; } = SyncEntryStatus.Pending;
    public DateTime? LastAttemptUtc { get; set; }
    public DateTime? SyncedUtc { get; set; }
    public int Priority { get; set; } = 10;
    public Guid? BatchId { get; set; }
}

public enum SyncOperationType { Insert = 0, Update = 1, Delete = 2 }
public enum SyncEntryStatus { Pending = 0, InProgress = 1, Completed = 2, Failed = 3, Abandoned = 4 }
