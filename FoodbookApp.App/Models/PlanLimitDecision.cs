namespace Foodbook.Models;

public sealed class PlanLimitDecision
{
    public static PlanLimitDecision Allowed() => new() { IsAllowed = true };

    public static PlanLimitDecision Denied(string userMessage) => new()
    {
        IsAllowed = false,
        UserMessage = userMessage
    };

    public bool IsAllowed { get; init; }
    public string UserMessage { get; init; } = string.Empty;
}

