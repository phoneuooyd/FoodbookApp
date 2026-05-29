using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Models;
using Foodbook.Data;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Supabase;
using Microsoft.Maui.Storage;
using Microsoft.EntityFrameworkCore;

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
    private readonly AppDbContext _context;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private FeatureAccessSnapshot? _cachedSnapshot;
    private DateTime _cacheExpiresAtUtc = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FeatureAccessService(
        SupabaseRestClient restClient,
        IAccountService accountService,
        AppDbContext context)
        : this(restClient, accountService, context, new SecureStorageAdapter(), new SystemClock())
    {
    }

    public FeatureAccessService(
        SupabaseRestClient restClient,
        IAccountService accountService,
        AppDbContext context,
        ISecureStorageAdapter secureStorage,
        IClock clock)
    {
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<bool> CanCreatePlanAsync()
    {
        var decision = await CanCreatePlanAsync(PlanType.Planner, _clock.UtcNow);
        return decision.IsAllowed;
    }

    public async Task<PlanLimitDecision> CanCreatePlanAsync(PlanType type, DateTime nowUtc)
    {
        var snapshot = await GetSnapshotAsync(forceRefresh: false);
        if (snapshot.IsPremiumUser)
        {
            return PlanLimitDecision.Allowed();
        }

        if (type == PlanType.Foodbook)
        {
            var activeFoodbooksCount = await _context.Plans
                .CountAsync(p => p.Type == PlanType.Foodbook && !p.IsArchived);

            if (activeFoodbooksCount >= 5)
            {
                return PlanLimitDecision.Denied("W planie Free możesz mieć maksymalnie 5 aktywnych Foodbooków.");
            }
        }

        if (type == PlanType.Planner)
        {
            var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (snapshot.Limits.LastPlannerCreationMonthUtc != monthStartUtc)
            {
                snapshot.Limits.LastPlannerCreationMonthUtc = monthStartUtc;
                snapshot.Limits.MonthlyPlanCreationsUsed = 0;
                await SaveSnapshotToCacheAsync(snapshot);
            }

            if (snapshot.Limits.MonthlyPlanCreationsUsed >= 4)
            {
                return PlanLimitDecision.Denied("W planie Free możesz utworzyć maksymalnie 4 nowe planery miesięcznie.");
            }
        }

        return PlanLimitDecision.Allowed();
    }

    public async Task RegisterPlanCreationAsync(PlanType type, DateTime nowUtc)
    {
        if (type != PlanType.Planner)
        {
            return;
        }

        var snapshot = await GetSnapshotAsync(forceRefresh: false);
        if (snapshot.IsPremiumUser)
        {
            return;
        }

        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (snapshot.Limits.LastPlannerCreationMonthUtc != monthStartUtc)
        {
            snapshot.Limits.LastPlannerCreationMonthUtc = monthStartUtc;
            snapshot.Limits.MonthlyPlanCreationsUsed = 0;
        }

        snapshot.Limits.MonthlyPlanCreationsUsed++;
        snapshot.LastSyncedUtc = nowUtc;
        await SaveSnapshotToCacheAsync(snapshot);
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

            var filter = $"id=eq.{account.SupabaseUserId}&limit=1";
            var rows = await _restClient.GetAsync<SupabaseUserRow>("users", filter);
            var row = rows.FirstOrDefault();
            return row?.ToSnapshot(_clock.UtcNow);
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
                MonthlyPlanCreationLimit = 4,
                MonthlyPlanCreationsUsed = 0,
                LastPlannerCreationMonthUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
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

    private sealed class SupabaseUserRow
    {
        public Guid Id { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumFrom { get; set; }
        public DateTime? PremiumTo { get; set; }
        public int? UserType { get; set; }
        public int? PlanCountThisMonth { get; set; }
        public int? ActiveListsCount { get; set; }
        public JsonElement? AdUnlocksJson { get; set; }

        public FeatureAccessSnapshot ToSnapshot(DateTime utcNow)
        {
            var isPremiumActive = IsPremium && (!PremiumTo.HasValue || PremiumTo.Value > utcNow);
            var monthStartUtc = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            return new FeatureAccessSnapshot
            {
                IsPremiumUser = isPremiumActive,
                Limits = new FeatureUsageLimits
                {
                    MonthlyPlanCreationLimit = isPremiumActive ? null : 4,
                    MonthlyPlanCreationsUsed = Math.Max(0, PlanCountThisMonth ?? 0),
                    LastPlannerCreationMonthUtc = monthStartUtc
                },
                Entitlements = new List<FeatureEntitlement>
                {
                    new() { Feature = PremiumFeature.AutoPlanner, IsEnabled = isPremiumActive },
                    new() { Feature = PremiumFeature.PlanRecycling, IsEnabled = isPremiumActive },
                    new() { Feature = PremiumFeature.AiRecipeCreation, IsEnabled = isPremiumActive },
                    new() { Feature = PremiumFeature.FoodbookTemplates, IsEnabled = isPremiumActive },
                    new() { Feature = PremiumFeature.PremiumRecipePacks, IsEnabled = isPremiumActive }
                },
                AdUnlocks = ParseAdUnlocks(AdUnlocksJson, utcNow)
            };
        }

        private static List<AdUnlockState> ParseAdUnlocks(JsonElement? adUnlocksJson, DateTime utcNow)
        {
            var result = new List<AdUnlockState>();
            if (!adUnlocksJson.HasValue || adUnlocksJson.Value.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in adUnlocksJson.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryResolveFeature(item, out var feature))
                {
                    continue;
                }

                if (!TryResolveExpiry(item, out var expiresAtUtc))
                {
                    continue;
                }

                if (expiresAtUtc <= utcNow)
                {
                    continue;
                }

                result.Add(new AdUnlockState
                {
                    Feature = feature,
                    ExpiresAtUtc = expiresAtUtc
                });
            }

            return result;
        }

        private static bool TryResolveFeature(JsonElement item, out PremiumFeature feature)
        {
            feature = default;

            if (!TryGetPropertyIgnoreCase(item, "feature", out var featureElement))
            {
                return false;
            }

            if (featureElement.ValueKind == JsonValueKind.Number && featureElement.TryGetInt32(out var numericFeature))
            {
                if (!Enum.IsDefined(typeof(PremiumFeature), numericFeature))
                {
                    return false;
                }

                feature = (PremiumFeature)numericFeature;
                return true;
            }

            if (featureElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var raw = featureElement.GetString();
            return Enum.TryParse(raw, ignoreCase: true, out feature);
        }

        private static bool TryResolveExpiry(JsonElement item, out DateTime expiresAtUtc)
        {
            expiresAtUtc = default;

            var candidateKeys = new[] { "expiresAtUtc", "expires_at_utc", "expiresAt", "expires_at" };
            foreach (var key in candidateKeys)
            {
                if (!TryGetPropertyIgnoreCase(item, key, out var expiryElement))
                {
                    continue;
                }

                if (expiryElement.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(expiryElement.GetString(), out var parsed))
                {
                    expiresAtUtc = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement item, string propertyName, out JsonElement value)
        {
            foreach (var property in item.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
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
