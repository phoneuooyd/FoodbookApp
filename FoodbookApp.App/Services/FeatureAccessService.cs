using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Supabase;
using Microsoft.Maui.Storage;

namespace Foodbook.Services;

public sealed class FeatureAccessService : IFeatureAccessService
{
    private const string CacheKey = "feature_access_snapshot_v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan AdUnlockDuration = TimeSpan.FromMinutes(30);

    private readonly SupabaseRestClient _restClient;
    private readonly IAccountService _accountService;
    private readonly ISecureStorageAdapter _secureStorage;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private FeatureAccessSnapshot? _cachedSnapshot;
    private DateTime _cacheExpiresAtUtc = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FeatureAccessService(SupabaseRestClient restClient, IAccountService accountService)
        : this(restClient, accountService, new SecureStorageAdapter(), new SystemClock())
    {
    }

    public FeatureAccessService(
        SupabaseRestClient restClient,
        IAccountService accountService,
        ISecureStorageAdapter secureStorage,
        IClock clock)
    {
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<bool> CanCreatePlanAsync()
    {
        var snapshot = await GetSnapshotAsync(forceRefresh: false);
        return snapshot.CanCreatePlan(_clock.UtcNow);
    }

    public async Task<bool> CanUsePremiumFeatureAsync(PremiumFeature feature)
    {
        var snapshot = await GetSnapshotAsync(forceRefresh: false);
        return snapshot.IsFeatureAllowed(feature, _clock.UtcNow);
    }

    public async Task<AdUnlockResult> RequestAdUnlockAsync(PremiumFeature feature)
    {
        var snapshot = await GetSnapshotAsync(forceRefresh: false);

        if (snapshot.IsPremiumUser)
        {
            return new AdUnlockResult
            {
                Success = true,
                Feature = feature,
                ExpiresAtUtc = null,
                Message = "Premium account already has full access."
            };
        }

        var now = _clock.UtcNow;
        var expiresAt = now.Add(AdUnlockDuration);
        var existingUnlock = snapshot.AdUnlocks.FirstOrDefault(x => x.Feature == feature);

        if (existingUnlock is null)
        {
            snapshot.AdUnlocks.Add(new AdUnlockState { Feature = feature, ExpiresAtUtc = expiresAt });
        }
        else
        {
            existingUnlock.ExpiresAtUtc = expiresAt;
        }

        snapshot.LastSyncedUtc = now;
        await SaveSnapshotToCacheAsync(snapshot);

        return new AdUnlockResult
        {
            Success = true,
            Feature = feature,
            ExpiresAtUtc = expiresAt,
            Message = "Ad unlock granted."
        };
    }

    public bool IsAdUnlockActive(PremiumFeature feature)
    {
        var now = _clock.UtcNow;
        var unlock = _cachedSnapshot?.AdUnlocks.FirstOrDefault(x => x.Feature == feature);
        return unlock?.ExpiresAtUtc > now;
    }

    public async Task RefreshAccessAsync()
    {
        await GetSnapshotAsync(forceRefresh: true);
    }

    private async Task<FeatureAccessSnapshot> GetSnapshotAsync(bool forceRefresh)
    {
        var now = _clock.UtcNow;

        if (!forceRefresh && _cachedSnapshot is not null && _cacheExpiresAtUtc > now)
        {
            return _cachedSnapshot;
        }

        await _syncLock.WaitAsync();
        try
        {
            now = _clock.UtcNow;
            if (!forceRefresh && _cachedSnapshot is not null && _cacheExpiresAtUtc > now)
            {
                return _cachedSnapshot;
            }

            if (!forceRefresh)
            {
                var cached = await ReadSnapshotFromCacheAsync();
                if (cached is not null)
                {
                    _cachedSnapshot = cached;
                    _cacheExpiresAtUtc = now.Add(CacheTtl);
                    return cached;
                }
            }

            var fresh = await TryFetchSnapshotFromSupabaseAsync() ?? CreateDefaultSnapshot(now);
            fresh.LastSyncedUtc = now;
            await SaveSnapshotToCacheAsync(fresh);

            return fresh;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<FeatureAccessSnapshot?> TryFetchSnapshotFromSupabaseAsync()
    {
        try
        {
            var account = await _accountService.GetActiveAccountAsync();
            if (account is null || string.IsNullOrWhiteSpace(account.SupabaseUserId))
            {
                return null;
            }

            var filter = $"user_id=eq.{account.SupabaseUserId}&order=updated_at.desc&limit=1";
            var rows = await _restClient.GetAsync<FeatureAccessSnapshotRow>("feature_access_snapshot", filter);
            var row = rows.FirstOrDefault();
            return row?.ToSnapshot() ?? null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FeatureAccessService] Failed to fetch access snapshot: {ex.Message}");
            return null;
        }
    }

    private static FeatureAccessSnapshot CreateDefaultSnapshot(DateTime nowUtc)
    {
        return new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            LastSyncedUtc = nowUtc,
            Limits = new FeatureUsageLimits
            {
                MonthlyPlanCreationLimit = 10,
                MonthlyPlanCreationsUsed = 0
            }
        };
    }

    private async Task<FeatureAccessSnapshot?> ReadSnapshotFromCacheAsync()
    {
        try
        {
            var raw = await _secureStorage.GetAsync(CacheKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<FeatureAccessCacheEnvelope>(raw, JsonOptions);
            if (cache?.Snapshot is null || cache.ExpiresAtUtc <= _clock.UtcNow)
            {
                return null;
            }

            return cache.Snapshot;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FeatureAccessService] Failed to read cache: {ex.Message}");
            return null;
        }
    }

    private async Task SaveSnapshotToCacheAsync(FeatureAccessSnapshot snapshot)
    {
        var expiresAt = _clock.UtcNow.Add(CacheTtl);
        var envelope = new FeatureAccessCacheEnvelope
        {
            Snapshot = snapshot,
            ExpiresAtUtc = expiresAt
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        await _secureStorage.SetAsync(CacheKey, json);

        _cachedSnapshot = snapshot;
        _cacheExpiresAtUtc = expiresAt;
    }

    private sealed class FeatureAccessCacheEnvelope
    {
        public DateTime ExpiresAtUtc { get; set; }
        public FeatureAccessSnapshot? Snapshot { get; set; }
    }

    private sealed class FeatureAccessSnapshotRow
    {
        public bool IsPremium { get; set; }
        public int? MonthlyPlanCreationLimit { get; set; }
        public int MonthlyPlanCreationsUsed { get; set; }
        public bool AutoPlannerEnabled { get; set; }
        public bool PlanRecyclingEnabled { get; set; }
        public bool AiRecipeCreationEnabled { get; set; }
        public bool FoodbookTemplatesEnabled { get; set; }
        public bool PremiumRecipePacksEnabled { get; set; }

        public FeatureAccessSnapshot ToSnapshot()
        {
            return new FeatureAccessSnapshot
            {
                IsPremiumUser = IsPremium,
                Limits = new FeatureUsageLimits
                {
                    MonthlyPlanCreationLimit = MonthlyPlanCreationLimit,
                    MonthlyPlanCreationsUsed = MonthlyPlanCreationsUsed
                },
                Entitlements = new List<FeatureEntitlement>
                {
                    new() { Feature = PremiumFeature.AutoPlanner, IsEnabled = AutoPlannerEnabled },
                    new() { Feature = PremiumFeature.PlanRecycling, IsEnabled = PlanRecyclingEnabled },
                    new() { Feature = PremiumFeature.AiRecipeCreation, IsEnabled = AiRecipeCreationEnabled },
                    new() { Feature = PremiumFeature.FoodbookTemplates, IsEnabled = FoodbookTemplatesEnabled },
                    new() { Feature = PremiumFeature.PremiumRecipePacks, IsEnabled = PremiumRecipePacksEnabled }
                }
            };
        }
    }
}

public interface ISecureStorageAdapter
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}

public sealed class SecureStorageAdapter : ISecureStorageAdapter
{
    public Task<string?> GetAsync(string key) => SecureStorage.GetAsync(key);

    public Task SetAsync(string key, string value) => SecureStorage.SetAsync(key, value);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
