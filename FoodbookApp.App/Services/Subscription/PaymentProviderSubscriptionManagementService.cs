using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services.Subscription;

public sealed class PaymentProviderSubscriptionManagementService : ISubscriptionManagementService
{
    public Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct)
    {
        throw new NotSupportedException("Payment provider subscription service is not wired yet.");
    }

    public Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct)
    {
        throw new NotSupportedException("Payment provider subscription service is not wired yet.");
    }
}
