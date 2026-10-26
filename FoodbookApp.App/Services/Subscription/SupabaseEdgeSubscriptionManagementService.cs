using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services.Subscription;

public sealed class SupabaseEdgeSubscriptionManagementService : ISubscriptionManagementService
{
    /// <summary>
    /// Duration of the one-time premium trial granted during onboarding.
    /// </summary>
    public const int FreePremiumTrialMonths = 3;

    /// <summary>
    /// Calculates the UTC expiration timestamp for the one-time free trial.
    /// </summary>
    public static DateTime GetFreePremiumTrialExpirationUtc(DateTime startedAtUtc)
    {
        var utcStart = startedAtUtc.Kind == DateTimeKind.Utc
            ? startedAtUtc
            : DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc);

        return utcStart.AddMonths(FreePremiumTrialMonths);
    }

    public Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct)
    {
        throw new NotSupportedException("Supabase Edge subscription provider is not wired yet.");
    }

    public Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Supabase Edge subscription provider is not wired yet.");
    }

    public Task<SubscriptionPendingOperation?> GetPendingOperationAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Supabase Edge subscription provider is not wired yet.");
    }

    public Task<SubscriptionActionResult> ResumePendingOperationAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Supabase Edge subscription provider is not wired yet.");
    }
}
