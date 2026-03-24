using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services.Subscription;

public sealed class MockSubscriptionManagementService : ISubscriptionManagementService
{
    private const string CacheKey = "feature_access_snapshot_v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISecureStorageAdapter _secureStorage;
    private readonly IClock _clock;

    public MockSubscriptionManagementService(ISecureStorageAdapter secureStorage, IClock clock)
    {
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = await GetSnapshotAsync();
        ApplyPlan(snapshot, targetPlan);
        snapshot.LastSyncedUtc = _clock.UtcNow;
        await SaveSnapshotAsync(snapshot);

        return new SubscriptionActionResult
        {
            Success = true,
            CurrentPlan = targetPlan,
            UiMessage = targetPlan switch
            {
                SubscriptionPlan.Free => "Przełączono konto na plan Free.",
                SubscriptionPlan.PremiumYearly => "Aktywowano plan Premium Roczny.",
                SubscriptionPlan.PremiumMonthly => "Aktywowano plan Premium Miesięczny.",
                _ => "Plan subskrypcji został zmieniony."
            }
        };
    }

    public async Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var snapshot = await GetSnapshotAsync();
        ApplyPlan(snapshot, SubscriptionPlan.Free);
        snapshot.LastSyncedUtc = _clock.UtcNow;
        await SaveSnapshotAsync(snapshot);

        return new SubscriptionActionResult
        {
            Success = true,
            CurrentPlan = SubscriptionPlan.Free,
            UiMessage = "Subskrypcja została anulowana. Konto działa teraz w planie Free."
        };
    }

    private async Task<FeatureAccessSnapshot> GetSnapshotAsync()
    {
        try
        {
            var raw = await _secureStorage.GetAsync(CacheKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return CreateDefaultSnapshot();
            }

            var envelope = JsonSerializer.Deserialize<FeatureAccessCacheEnvelope>(raw, JsonOptions);
            return envelope?.Snapshot ?? CreateDefaultSnapshot();
        }
        catch
        {
            return CreateDefaultSnapshot();
        }
    }

    private async Task SaveSnapshotAsync(FeatureAccessSnapshot snapshot)
    {
        var envelope = new FeatureAccessCacheEnvelope
        {
            ExpiresAtUtc = _clock.UtcNow.AddHours(1),
            Snapshot = snapshot
        };

        var raw = JsonSerializer.Serialize(envelope, JsonOptions);
        await _secureStorage.SetAsync(CacheKey, raw);
    }

    private static FeatureAccessSnapshot CreateDefaultSnapshot()
    {
        var snapshot = new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            Limits = new FeatureUsageLimits
            {
                MonthlyPlanCreationLimit = 10,
                MonthlyPlanCreationsUsed = 0
            }
        };

        ApplyPlan(snapshot, SubscriptionPlan.Free);
        return snapshot;
    }

    private static void ApplyPlan(FeatureAccessSnapshot snapshot, SubscriptionPlan plan)
    {
        snapshot.IsPremiumUser = plan is SubscriptionPlan.PremiumMonthly or SubscriptionPlan.PremiumYearly;
        snapshot.Entitlements =
        [
            new() { Feature = PremiumFeature.AutoPlanner, IsEnabled = snapshot.IsPremiumUser },
            new() { Feature = PremiumFeature.PlanRecycling, IsEnabled = snapshot.IsPremiumUser },
            new() { Feature = PremiumFeature.AiRecipeCreation, IsEnabled = snapshot.IsPremiumUser },
            new() { Feature = PremiumFeature.FoodbookTemplates, IsEnabled = snapshot.IsPremiumUser },
            new() { Feature = PremiumFeature.PremiumRecipePacks, IsEnabled = snapshot.IsPremiumUser }
        ];
    }

    private sealed class FeatureAccessCacheEnvelope
    {
        public DateTime ExpiresAtUtc { get; set; }
        public FeatureAccessSnapshot? Snapshot { get; set; }
    }
}
