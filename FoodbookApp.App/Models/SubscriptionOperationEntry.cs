using System.ComponentModel.DataAnnotations;

namespace Foodbook.Models;

public class SubscriptionOperationEntry
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AccountId { get; set; }

    public SubscriptionPlan TargetPlan { get; set; }

    public SubscriptionOperationStatus Status { get; set; } = SubscriptionOperationStatus.Pending;

    [Required]
    [MaxLength(120)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public int RetryCount { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastAttemptUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }
}

public enum SubscriptionOperationStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Abandoned = 4
}
