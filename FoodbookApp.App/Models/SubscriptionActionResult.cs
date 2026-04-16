namespace Foodbook.Models;

public sealed class SubscriptionActionResult
{
    public bool Success { get; set; }
    public SubscriptionPlan CurrentPlan { get; set; }
    public SubscriptionActionState ActionState { get; set; } = SubscriptionActionState.Completed;
    public Guid? OperationId { get; set; }
    public string UiMessage { get; set; } = string.Empty;
}
