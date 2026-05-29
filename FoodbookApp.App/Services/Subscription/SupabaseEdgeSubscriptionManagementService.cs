using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services.Subscription;

public sealed class SupabaseEdgeSubscriptionManagementService : ISubscriptionManagementService
{
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
