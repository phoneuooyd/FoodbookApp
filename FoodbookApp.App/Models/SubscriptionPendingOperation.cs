namespace Foodbook.Models;

public sealed class SubscriptionPendingOperation
{
    public Guid Id { get; set; }
    public SubscriptionPlan TargetPlan { get; set; }
    public SubscriptionOperationStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastAttemptUtc { get; set; }
}
