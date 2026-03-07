namespace Foodbook.Models;

public sealed class FeatureAccessSnapshot
{
    public bool IsPremiumUser { get; set; }
    public DateTime LastSyncedUtc { get; set; } = DateTime.UtcNow;
    public FeatureUsageLimits Limits { get; set; } = new();
    public List<FeatureEntitlement> Entitlements { get; set; } = new();
    public List<AdUnlockState> AdUnlocks { get; set; } = new();

    public bool IsFeatureAllowed(PremiumFeature feature, DateTime utcNow)
    {
        if (IsPremiumUser)
        {
            return true;
        }

        if (Entitlements.Any(e => e.Feature == feature && e.IsEnabled))
        {
            return true;
        }

        var unlock = AdUnlocks.FirstOrDefault(u => u.Feature == feature);
        return unlock?.ExpiresAtUtc is DateTime expiresAt && expiresAt > utcNow;
    }

    public bool CanCreatePlan(DateTime utcNow)
    {
        if (IsPremiumUser)
        {
            return true;
        }

        if (Limits.MonthlyPlanCreationLimit is null)
        {
            return true;
        }

        return Limits.MonthlyPlanCreationsUsed < Limits.MonthlyPlanCreationLimit.Value;
    }
}

public sealed class FeatureUsageLimits
{
    public int? MonthlyPlanCreationLimit { get; set; }
    public int MonthlyPlanCreationsUsed { get; set; }
}

public sealed class FeatureEntitlement
{
    public PremiumFeature Feature { get; set; }
    public bool IsEnabled { get; set; }
}

public sealed class AdUnlockState
{
    public PremiumFeature Feature { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class AdUnlockResult
{
    public bool Success { get; set; }
    public PremiumFeature Feature { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string Message { get; set; } = string.Empty;
}
