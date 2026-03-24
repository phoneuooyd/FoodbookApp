using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface ISubscriptionManagementService
{
    Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct);
    Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct);
}
