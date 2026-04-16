using System.Net;
using System.Text;
using FluentAssertions;
using Foodbook.Data;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using FoodbookApp.Services.Subscription;
using FoodbookApp.Services.Supabase;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Tests;

public class MockSubscriptionManagementServiceTests
{
    [Fact]
    public async Task ChangePlanAsync_WhenSupabaseUnavailable_ShouldReturnPendingAndPersistOperation()
    {
        var context = CreateInMemoryContext(nameof(ChangePlanAsync_WhenSupabaseUnavailable_ShouldReturnPendingAndPersistOperation));
        var account = CreateActiveAccount();
        var clock = new FakeClock(new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var handler = new SwitchableHttpHandler { FailRequests = true };

        var service = CreateService(context, account, storage, clock, handler);

        var result = await service.ChangePlanAsync(SubscriptionPlan.PremiumMonthly, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ActionState.Should().Be(SubscriptionActionState.Pending);
        result.OperationId.Should().NotBeNull();

        var operation = await context.SubscriptionOperations.SingleAsync();
        operation.Status.Should().Be(SubscriptionOperationStatus.Pending);
        operation.TargetPlan.Should().Be(SubscriptionPlan.PremiumMonthly);

        var pending = await service.GetPendingOperationAsync(CancellationToken.None);
        pending.Should().NotBeNull();
        pending!.TargetPlan.Should().Be(SubscriptionPlan.PremiumMonthly);
    }

    [Fact]
    public async Task ResumePendingOperationAsync_WhenConnectionRestored_ShouldCompleteOperation()
    {
        var context = CreateInMemoryContext(nameof(ResumePendingOperationAsync_WhenConnectionRestored_ShouldCompleteOperation));
        var account = CreateActiveAccount();
        var clock = new FakeClock(new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var handler = new SwitchableHttpHandler { FailRequests = true };

        var service = CreateService(context, account, storage, clock, handler);

        var pendingResult = await service.ChangePlanAsync(SubscriptionPlan.PremiumMonthly, CancellationToken.None);
        pendingResult.ActionState.Should().Be(SubscriptionActionState.Pending);

        handler.FailRequests = false;

        var resumed = await service.ResumePendingOperationAsync(CancellationToken.None);

        resumed.Success.Should().BeTrue();
        resumed.ActionState.Should().Be(SubscriptionActionState.Completed);
        resumed.CurrentPlan.Should().Be(SubscriptionPlan.PremiumMonthly);

        var operation = await context.SubscriptionOperations.SingleAsync();
        operation.Status.Should().Be(SubscriptionOperationStatus.Completed);

        var pending = await service.GetPendingOperationAsync(CancellationToken.None);
        pending.Should().BeNull();
    }

    [Fact]
    public async Task CancelSubscriptionAsync_ShouldAbandonOpenOperationAndApplyFreePlan()
    {
        var context = CreateInMemoryContext(nameof(CancelSubscriptionAsync_ShouldAbandonOpenOperationAndApplyFreePlan));
        var account = CreateActiveAccount();
        var clock = new FakeClock(new DateTime(2026, 4, 15, 10, 0, 0, DateTimeKind.Utc));
        var storage = new InMemorySecureStorage();
        var handler = new SwitchableHttpHandler { FailRequests = true };

        var service = CreateService(context, account, storage, clock, handler);

        var pendingResult = await service.ChangePlanAsync(SubscriptionPlan.PremiumYearly, CancellationToken.None);
        pendingResult.ActionState.Should().Be(SubscriptionActionState.Pending);

        handler.FailRequests = false;

        var cancelled = await service.CancelSubscriptionAsync(CancellationToken.None);

        cancelled.Success.Should().BeTrue();
        cancelled.CurrentPlan.Should().Be(SubscriptionPlan.Free);
        cancelled.ActionState.Should().Be(SubscriptionActionState.Completed);

        var operations = await context.SubscriptionOperations.ToListAsync();
        operations.Should().ContainSingle(o => o.Status == SubscriptionOperationStatus.Abandoned);
    }

    private static MockSubscriptionManagementService CreateService(
        AppDbContext context,
        AuthAccount account,
        ISecureStorageAdapter storage,
        IClock clock,
        SwitchableHttpHandler handler)
    {
        var client = new HttpClient(handler);
        var restClient = new SupabaseRestClient(client, new FakeTokenStore(account.Id), "https://example.supabase.co", "anon");
        var accountService = new FakeAccountService(account);

        return new MockSubscriptionManagementService(context, accountService, restClient, storage, clock);
    }

    private static AppDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new AppDbContext(options);
    }

    private static AuthAccount CreateActiveAccount()
        => new()
        {
            Id = Guid.NewGuid(),
            SupabaseUserId = Guid.NewGuid().ToString(),
            Email = "demo@foodbook.app"
        };

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; private set; }
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
        private readonly Guid _accountId;

        public FakeTokenStore(Guid accountId)
        {
            _accountId = accountId;
        }

        public Task ClearAllAsync() => Task.CompletedTask;
        public Task ClearTokensAsync(Guid accountId) => Task.CompletedTask;
        public Task<Guid?> GetActiveAccountIdAsync() => Task.FromResult<Guid?>(_accountId);
        public Task<string?> GetAccessTokenAsync(Guid accountId) => Task.FromResult<string?>(null);
        public Task<DateTimeOffset?> GetExpiresAtAsync(Guid accountId) => Task.FromResult<DateTimeOffset?>(null);
        public Task<string?> GetRefreshTokenAsync(Guid accountId) => Task.FromResult<string?>(null);
        public Task SetActiveAccountIdAsync(Guid? accountId) => Task.CompletedTask;
        public Task SetTokensAsync(Guid accountId, string? accessToken, string? refreshToken, DateTimeOffset? expiresAt) => Task.CompletedTask;
    }

    private sealed class FakeAccountService : IAccountService
    {
        private readonly AuthAccount _account;

        public FakeAccountService(AuthAccount account)
        {
            _account = account;
        }

        public Task<IReadOnlyList<AuthAccount>> GetAccountsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuthAccount>>(new List<AuthAccount> { _account });

        public Task<AuthAccount?> GetActiveAccountAsync(CancellationToken ct = default)
            => Task.FromResult<AuthAccount?>(_account);

        public Task<AuthAccount> SignInAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default)
            => Task.FromResult(_account);

        public Task<AuthAccount> SignUpAsync(string email, string password, bool enableAutoLogin, CancellationToken ct = default)
            => Task.FromResult(_account);

        public Task SignOutAsync(bool clearAutoLogin, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SwitchAccountAsync(Guid accountId, bool enableAutoLogin, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> TryAutoLoginAsync(CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class SwitchableHttpHandler : HttpMessageHandler
    {
        public bool FailRequests { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (FailRequests)
            {
                throw new HttpRequestException("network unavailable");
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
