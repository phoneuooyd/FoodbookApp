namespace Foodbook.Models;

public sealed class SubscriptionActionResult
{
    public bool Success { get; set; }
    public SubscriptionPlan CurrentPlan { get; set; }
    public string UiMessage { get; set; } = string.Empty;
}
