using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IFeatureAccessService
{
    Task<bool> CanCreatePlanAsync();
    Task<PlanLimitDecision> CanCreatePlanAsync(PlanType type, DateTime nowUtc);
    Task RegisterPlanCreationAsync(PlanType type, DateTime nowUtc);
    Task<bool> CanUsePremiumFeatureAsync(PremiumFeature feature);
    Task<AdUnlockResult> RequestAdUnlockAsync(PremiumFeature feature);
    bool IsAdUnlockActive(PremiumFeature feature);
    Task RefreshAccessAsync();
}
