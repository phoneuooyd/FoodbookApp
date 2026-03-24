namespace Foodbook.Models;

public sealed class PlanLimitExceededException : Exception
{
    public PlanLimitExceededException(string message)
        : base(message)
    {
    }
}

