using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface ISubscriptionManagementService
{
    Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct);
    Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct);
    Task<SubscriptionPendingOperation?> GetPendingOperationAsync(CancellationToken ct);
    Task<SubscriptionActionResult> ResumePendingOperationAsync(CancellationToken ct);
}
