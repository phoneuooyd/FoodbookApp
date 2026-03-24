using FluentAssertions;
using System.Text.Json;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using FoodbookApp.Services.Supabase;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Tests;

public class FeatureAccessServiceTests
{
    [Fact]
    public void CanCreatePlan_ShouldRespectMonthlyLimitForFreeUser()
    {
        var snapshot = new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            Limits = new FeatureUsageLimits
            {
                MonthlyPlanCreationLimit = 2,
                MonthlyPlanCreationsUsed = 2
            }
        };

        snapshot.CanCreatePlan(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsFeatureAllowed_ShouldAllowFeatureWhenAdUnlockIsActive()
    {
        var now = DateTime.UtcNow;
        var snapshot = new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            AdUnlocks = new List<AdUnlockState>
            {
                new() { Feature = PremiumFeature.AiRecipeCreation, ExpiresAtUtc = now.AddMinutes(10) }
            }
        };

        snapshot.IsFeatureAllowed(PremiumFeature.AiRecipeCreation, now).Should().BeTrue();
    }

    [Fact]
    public async Task CanUsePremiumFeatureAsync_ShouldUseMemoryCacheUntilTtlExpires()
    {
        var fakeClock = new FakeClock(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();

        var initial = new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            Entitlements = new List<FeatureEntitlement>
            {
                new() { Feature = PremiumFeature.AutoPlanner, IsEnabled = true }
            }
        };

        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(initial, fakeClock.UtcNow.AddHours(2)));

        var service = CreateService(storage, fakeClock);
        (await service.CanUsePremiumFeatureAsync(PremiumFeature.AutoPlanner)).Should().BeTrue();

        var updated = new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            Entitlements = new List<FeatureEntitlement>
            {
                new() { Feature = PremiumFeature.AutoPlanner, IsEnabled = false }
            }
        };

        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(updated, fakeClock.UtcNow.AddHours(2)));

        (await service.CanUsePremiumFeatureAsync(PremiumFeature.AutoPlanner)).Should().BeTrue();

        fakeClock.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromMinutes(1)));

        (await service.CanUsePremiumFeatureAsync(PremiumFeature.AutoPlanner)).Should().BeFalse();
    }

    [Fact]
    public async Task CanCreatePlanAsync_ForFoodbook_ShouldBlockFreeUserAtFiveActiveFoodbooks()
    {
        var fakeClock = new FakeClock(new DateTime(2026, 3, 24, 9, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var context = CreateInMemoryContext(nameof(CanCreatePlanAsync_ForFoodbook_ShouldBlockFreeUserAtFiveActiveFoodbooks));

        for (var i = 0; i < 5; i++)
        {
            context.Plans.Add(new Plan
            {
                Id = Guid.NewGuid(),
                Type = PlanType.Foodbook,
                IsArchived = false,
                StartDate = fakeClock.UtcNow.Date,
                EndDate = fakeClock.UtcNow.Date.AddDays(6)
            });
        }

        await context.SaveChangesAsync();
        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(new FeatureAccessSnapshot { IsPremiumUser = false }, fakeClock.UtcNow.AddHours(2)));

        var service = CreateService(storage, fakeClock, context);
        var decision = await service.CanCreatePlanAsync(PlanType.Foodbook, fakeClock.UtcNow);

        decision.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task CanCreatePlanAsync_ForFoodbook_ShouldAllowPremiumUserEvenAboveLimit()
    {
        var fakeClock = new FakeClock(new DateTime(2026, 3, 24, 9, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var context = CreateInMemoryContext(nameof(CanCreatePlanAsync_ForFoodbook_ShouldAllowPremiumUserEvenAboveLimit));

        for (var i = 0; i < 7; i++)
        {
            context.Plans.Add(new Plan
            {
                Id = Guid.NewGuid(),
                Type = PlanType.Foodbook,
                IsArchived = false,
                StartDate = fakeClock.UtcNow.Date,
                EndDate = fakeClock.UtcNow.Date.AddDays(6)
            });
        }

        await context.SaveChangesAsync();
        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(new FeatureAccessSnapshot { IsPremiumUser = true }, fakeClock.UtcNow.AddHours(2)));

        var service = CreateService(storage, fakeClock, context);
        var decision = await service.CanCreatePlanAsync(PlanType.Foodbook, fakeClock.UtcNow);

        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreatePlanAsync_ForPlanner_ShouldBlockFreeUserAfterFourMonthlyCreations()
    {
        var fakeClock = new FakeClock(new DateTime(2026, 3, 24, 9, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var context = CreateInMemoryContext(nameof(CanCreatePlanAsync_ForPlanner_ShouldBlockFreeUserAfterFourMonthlyCreations));

        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            Limits = new FeatureUsageLimits
            {
                MonthlyPlanCreationsUsed = 4,
                LastPlannerCreationMonthUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        }, fakeClock.UtcNow.AddHours(2)));

        var service = CreateService(storage, fakeClock, context);
        var decision = await service.CanCreatePlanAsync(PlanType.Planner, fakeClock.UtcNow);

        decision.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task CanCreatePlanAsync_ForPlanner_ShouldAllowPremiumUserAfterFourMonthlyCreations()
    {
        var fakeClock = new FakeClock(new DateTime(2026, 3, 24, 9, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var context = CreateInMemoryContext(nameof(CanCreatePlanAsync_ForPlanner_ShouldAllowPremiumUserAfterFourMonthlyCreations));

        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(new FeatureAccessSnapshot
        {
            IsPremiumUser = true,
            Limits = new FeatureUsageLimits
            {
                MonthlyPlanCreationsUsed = 4,
                LastPlannerCreationMonthUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        }, fakeClock.UtcNow.AddHours(2)));

        var service = CreateService(storage, fakeClock, context);
        var decision = await service.CanCreatePlanAsync(PlanType.Planner, fakeClock.UtcNow);

        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterPlanCreationAsync_ForPlanner_ShouldIncreaseMonthlyCounterForFreeUser()
    {
        var fakeClock = new FakeClock(new DateTime(2026, 3, 24, 9, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var context = CreateInMemoryContext(nameof(RegisterPlanCreationAsync_ForPlanner_ShouldIncreaseMonthlyCounterForFreeUser));

        await storage.SetAsync("feature_access_snapshot_v1", SerializeEnvelope(new FeatureAccessSnapshot
        {
            IsPremiumUser = false,
            Limits = new FeatureUsageLimits
            {
                MonthlyPlanCreationsUsed = 1,
                LastPlannerCreationMonthUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        }, fakeClock.UtcNow.AddHours(2)));

        var service = CreateService(storage, fakeClock, context);
        await service.RegisterPlanCreationAsync(PlanType.Planner, fakeClock.UtcNow);

        var decision = await service.CanCreatePlanAsync(PlanType.Planner, fakeClock.UtcNow);
        decision.IsAllowed.Should().BeTrue();
    }

    private static AppDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static FeatureAccessService CreateService(ISecureStorageAdapter storage, IClock clock, AppDbContext? context = null)
    {
        var httpClient = new HttpClient(new HttpClientHandler());
        var tokenStore = new FakeTokenStore();
        var restClient = new SupabaseRestClient(httpClient, tokenStore, "https://example.supabase.co", "anon");

        context ??= CreateInMemoryContext(Guid.NewGuid().ToString());

        return new FeatureAccessService(restClient, new FakeAccountService(), context, storage, clock);
    }

    private static string SerializeEnvelope(FeatureAccessSnapshot snapshot, DateTime expiresAtUtc)
    {
        var payload = new
        {
            expiresAtUtc,
            snapshot
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; private set; }
        public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    }

    private sealed class InMemorySecureStorage : ISecureStorageAdapter
    {
        private readonly Dictionary<string, string> _store = new();

        public Task<string?> GetAsync(string key)
            => Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenStore : IAuthTokenStore
    {
        public Task ClearAllAsync() => Task.CompletedTask;
        public Task ClearTokensAsync(Guid accountId) => Task.CompletedTask;
        public Task<Guid?> GetActiveAccountIdAsync() => Task.FromResult<Guid?>(null);
        public Task<string?> GetAccessTokenAsync(Guid accountId) => Task.FromResult<string?>(null);
        public Task<DateTimeOffset?> GetExpiresAtAsync(Guid accountId) => Task.FromResult<DateTimeOffset?>(null);
        public Task<string?> GetRefreshTokenAsync(Guid accountId) => Task.FromResult<string?>(null);
        public Task SetActiveAccountIdAsync(Guid? accountId) => Task.CompletedTask;
        public Task SetTokensAsync(Guid accountId, string? accessToken, string? refreshToken, DateTimeOffset? expiresAt) => Task.CompletedTask;
    }

    private sealed class FakeAccountService : IAccountService
    {
        public Task<IReadOnlyList<AuthAccount>> GetAccountsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AuthAccount>>(new List<AuthAccount>());
        public Task<AuthAccount?> GetActiveAccountAsync(CancellationToken ct = default) => Task.FromResult<AuthAccount?>(null);
        public Task<AuthAccount> SignInAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AuthAccount> SignUpAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SignOutAsync(bool clearAutoLogin, CancellationToken ct = default) => Task.CompletedTask;
        public Task SwitchAccountAsync(Guid accountId, bool enableAutoLogin, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> TryAutoLoginAsync(CancellationToken ct = default) => Task.FromResult(false);
    }
}
