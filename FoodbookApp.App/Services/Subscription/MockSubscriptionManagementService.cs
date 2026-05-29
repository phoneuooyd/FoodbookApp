using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Supabase;
using Foodbook.Services;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Services.Subscription;

public sealed class MockSubscriptionManagementService : ISubscriptionManagementService
{
    private const string CacheKey = "feature_access_snapshot_v1";
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan InProgressRecoveryThreshold = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IAccountService _accountService;
    private readonly SupabaseRestClient _restClient;
    private readonly ISecureStorageAdapter _secureStorage;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MockSubscriptionManagementService(
        AppDbContext context,
        IAccountService accountService,
        SupabaseRestClient restClient,
        ISecureStorageAdapter secureStorage,
        IClock clock)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await RecoverStaleInProgressOperationsAsync(ct);

        var account = await GetActiveAccountOrThrowAsync(ct);

        if (targetPlan == SubscriptionPlan.Free)
        {
            await AbandonOpenOperationsAsync(account.Id, "Operacja anulowana przez użytkownika.", ct);
            return await ApplyImmediatePlanChangeAsync(account, targetPlan, ct, "Przełączono konto na plan Free.");
        }

        var existing = await GetLatestOpenOperationAsync(account.Id, ct);
        if (existing != null)
        {
            if (existing.TargetPlan == targetPlan)
            {
                return BuildPendingResult(existing, targetPlan, "Operacja zmiany planu jest już w toku.");
            }

            existing.Status = SubscriptionOperationStatus.Abandoned;
            existing.UpdatedUtc = _clock.UtcNow;
            existing.LastError = "Operacja została zastąpiona nowszą prośbą użytkownika.";
            await _context.SaveChangesAsync(ct);
        }

        var operation = new SubscriptionOperationEntry
        {
            AccountId = account.Id,
            TargetPlan = targetPlan,
            Status = SubscriptionOperationStatus.Pending,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedUtc = _clock.UtcNow,
            UpdatedUtc = _clock.UtcNow
        };

        _context.SubscriptionOperations.Add(operation);
        await _context.SaveChangesAsync(ct);

        return await ProcessOperationAsync(account, operation, ct);
    }

    public async Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var account = await GetActiveAccountOrThrowAsync(ct);
        await AbandonOpenOperationsAsync(account.Id, "Subskrypcja została anulowana przez użytkownika.", ct);

        return await ApplyImmediatePlanChangeAsync(
            account,
            SubscriptionPlan.Free,
            ct,
            "Subskrypcja została anulowana. Konto działa teraz w planie Free.");
    }

    public async Task<SubscriptionPendingOperation?> GetPendingOperationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await RecoverStaleInProgressOperationsAsync(ct);

        var account = await _accountService.GetActiveAccountAsync(ct);
        if (account == null)
        {
            return null;
        }

        var operation = await GetLatestOpenOperationAsync(account.Id, ct);
        return operation == null
            ? null
            : new SubscriptionPendingOperation
            {
                Id = operation.Id,
                TargetPlan = operation.TargetPlan,
                Status = operation.Status,
                RetryCount = operation.RetryCount,
                LastError = operation.LastError,
                CreatedUtc = operation.CreatedUtc,
                LastAttemptUtc = operation.LastAttemptUtc
            };
    }

    public async Task<SubscriptionActionResult> ResumePendingOperationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await RecoverStaleInProgressOperationsAsync(ct);

        var account = await GetActiveAccountOrThrowAsync(ct);
        var operation = await GetLatestOpenOperationAsync(account.Id, ct);
        if (operation == null)
        {
            return new SubscriptionActionResult
            {
                Success = true,
                ActionState = SubscriptionActionState.Completed,
                CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                UiMessage = "Brak operacji subskrypcji do wznowienia."
            };
        }

        return await ProcessOperationAsync(account, operation, ct);
    }

    private async Task<SubscriptionActionResult> ApplyImmediatePlanChangeAsync(
        AuthAccount account,
        SubscriptionPlan targetPlan,
        CancellationToken ct,
        string successMessage)
    {
        await _operationLock.WaitAsync(ct);
        try
        {
            await UpsertSupabaseUserPlanAsync(account, targetPlan, ct);

            var snapshot = await GetSnapshotAsync();
            ApplyPlan(snapshot, targetPlan);
            snapshot.LastSyncedUtc = _clock.UtcNow;
            await SaveSnapshotAsync(snapshot);

            return new SubscriptionActionResult
            {
                Success = true,
                CurrentPlan = targetPlan,
                ActionState = SubscriptionActionState.Completed,
                UiMessage = successMessage
            };
        }
        catch (Exception ex)
        {
            return new SubscriptionActionResult
            {
                Success = false,
                CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                ActionState = SubscriptionActionState.Failed,
                UiMessage = $"Nie udało się zapisać zmiany planu: {ex.Message}"
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<SubscriptionActionResult> ProcessOperationAsync(
        AuthAccount account,
        SubscriptionOperationEntry operation,
        CancellationToken ct)
    {
        await _operationLock.WaitAsync(ct);
        try
        {
            var tracked = await _context.SubscriptionOperations
                .FirstOrDefaultAsync(e => e.Id == operation.Id, ct);

            if (tracked == null)
            {
                return new SubscriptionActionResult
                {
                    Success = false,
                    CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                    ActionState = SubscriptionActionState.Failed,
                    UiMessage = "Nie znaleziono operacji subskrypcji do przetworzenia."
                };
            }

            if (tracked.Status == SubscriptionOperationStatus.Completed)
            {
                return new SubscriptionActionResult
                {
                    Success = true,
                    CurrentPlan = tracked.TargetPlan,
                    ActionState = SubscriptionActionState.Completed,
                    OperationId = tracked.Id,
                    UiMessage = "Operacja subskrypcji została już zakończona."
                };
            }

            var now = _clock.UtcNow;
            tracked.Status = SubscriptionOperationStatus.InProgress;
            tracked.LastAttemptUtc = now;
            tracked.UpdatedUtc = now;
            await _context.SaveChangesAsync(ct);

            try
            {
                await UpsertSupabaseUserPlanAsync(account, tracked.TargetPlan, ct);

                var snapshot = await GetSnapshotAsync();
                ApplyPlan(snapshot, tracked.TargetPlan);
                snapshot.LastSyncedUtc = _clock.UtcNow;
                await SaveSnapshotAsync(snapshot);

                tracked.Status = SubscriptionOperationStatus.Completed;
                tracked.CompletedUtc = _clock.UtcNow;
                tracked.LastError = null;
                tracked.UpdatedUtc = _clock.UtcNow;
                await _context.SaveChangesAsync(ct);

                return new SubscriptionActionResult
                {
                    Success = true,
                    CurrentPlan = tracked.TargetPlan,
                    ActionState = SubscriptionActionState.Completed,
                    OperationId = tracked.Id,
                    UiMessage = tracked.TargetPlan switch
                    {
                        SubscriptionPlan.PremiumYearly => "Aktywowano plan Premium Roczny.",
                        SubscriptionPlan.PremiumMonthly => "Aktywowano plan Premium Miesięczny.",
                        _ => "Plan subskrypcji został zmieniony."
                    }
                };
            }
            catch (Exception ex)
            {
                tracked.RetryCount++;
                tracked.LastError = ex.Message;
                tracked.LastAttemptUtc = _clock.UtcNow;
                tracked.UpdatedUtc = _clock.UtcNow;

                if (tracked.RetryCount >= MaxRetryCount)
                {
                    tracked.Status = SubscriptionOperationStatus.Failed;
                    await _context.SaveChangesAsync(ct);

                    return new SubscriptionActionResult
                    {
                        Success = false,
                        CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                        ActionState = SubscriptionActionState.Failed,
                        OperationId = tracked.Id,
                        UiMessage = "Nie udało się aktywować planu. Wznów operację ręcznie po sprawdzeniu połączenia."
                    };
                }

                tracked.Status = SubscriptionOperationStatus.Pending;
                await _context.SaveChangesAsync(ct);

                return BuildPendingResult(
                    tracked,
                    tracked.TargetPlan,
                    "Proces aktywacji planu został zapisany. Możesz go wznowić po odzyskaniu połączenia.");
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task RecoverStaleInProgressOperationsAsync(CancellationToken ct)
    {
        var threshold = _clock.UtcNow - InProgressRecoveryThreshold;
        var staleEntries = await _context.SubscriptionOperations
            .Where(e => e.Status == SubscriptionOperationStatus.InProgress
                        && e.LastAttemptUtc.HasValue
                        && e.LastAttemptUtc.Value < threshold)
            .ToListAsync(ct);

        if (staleEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in staleEntries)
        {
            entry.Status = SubscriptionOperationStatus.Pending;
            entry.UpdatedUtc = _clock.UtcNow;
            entry.LastError = "Wykryto przerwaną operację. Gotowa do wznowienia.";
        }

        await _context.SaveChangesAsync(ct);
    }

    private async Task<SubscriptionOperationEntry?> GetLatestOpenOperationAsync(Guid accountId, CancellationToken ct)
        => await _context.SubscriptionOperations
            .Where(e => e.AccountId == accountId &&
                        (e.Status == SubscriptionOperationStatus.Pending ||
                         e.Status == SubscriptionOperationStatus.InProgress ||
                         e.Status == SubscriptionOperationStatus.Failed))
            .OrderByDescending(e => e.UpdatedUtc)
            .FirstOrDefaultAsync(ct);

    private async Task AbandonOpenOperationsAsync(Guid accountId, string reason, CancellationToken ct)
    {
        var openEntries = await _context.SubscriptionOperations
            .Where(e => e.AccountId == accountId &&
                        (e.Status == SubscriptionOperationStatus.Pending ||
                         e.Status == SubscriptionOperationStatus.InProgress ||
                         e.Status == SubscriptionOperationStatus.Failed))
            .ToListAsync(ct);

        if (openEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in openEntries)
        {
            entry.Status = SubscriptionOperationStatus.Abandoned;
            entry.UpdatedUtc = _clock.UtcNow;
            entry.LastError = reason;
        }

        await _context.SaveChangesAsync(ct);
    }

    private static SubscriptionActionResult BuildPendingResult(
        SubscriptionOperationEntry entry,
        SubscriptionPlan targetPlan,
        string message)
        => new()
        {
            Success = false,
            CurrentPlan = targetPlan,
            ActionState = SubscriptionActionState.Pending,
            OperationId = entry.Id,
            UiMessage = message
        };

    private async Task<AuthAccount> GetActiveAccountOrThrowAsync(CancellationToken ct)
    {
        var account = await _accountService.GetActiveAccountAsync(ct);
        if (account == null)
        {
            throw new InvalidOperationException("Brak aktywnego konta.");
        }

        return account;
    }

    private async Task UpsertSupabaseUserPlanAsync(AuthAccount account, SubscriptionPlan targetPlan, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(account.SupabaseUserId))
        {
            throw new InvalidOperationException("Aktywne konto nie ma powiązanego identyfikatora Supabase.");
        }

        if (!Guid.TryParse(account.SupabaseUserId, out var supabaseUserId))
        {
            throw new InvalidOperationException("Nieprawidłowy format identyfikatora użytkownika Supabase.");
        }

        var now = _clock.UtcNow;
        var payload = BuildSupabaseUserPayload(account.Email, supabaseUserId, targetPlan, now);
        var existing = await _restClient.GetAsync<SupabaseUserRow>("users", $"id=eq.{account.SupabaseUserId}&limit=1", ct);

        if (existing.Count == 0)
        {
            await _restClient.InsertAsync("users", payload, ct);
            return;
        }

        await _restClient.UpdateAsync("users", supabaseUserId, payload, ct);
    }

    private static SupabaseUserWriteRow BuildSupabaseUserPayload(
        string? email,
        Guid userId,
        SubscriptionPlan targetPlan,
        DateTime utcNow)
    {
        var isPremium = targetPlan is SubscriptionPlan.PremiumMonthly or SubscriptionPlan.PremiumYearly;
        DateTime? premiumFrom = null;
        DateTime? premiumTo = null;
        var userType = 0;

        if (targetPlan == SubscriptionPlan.PremiumMonthly)
        {
            premiumFrom = utcNow;
            premiumTo = utcNow.AddMonths(1);
            userType = 1;
        }
        else if (targetPlan == SubscriptionPlan.PremiumYearly)
        {
            premiumFrom = utcNow;
            premiumTo = utcNow.AddYears(1);
            userType = 1;
        }

        return new SupabaseUserWriteRow
        {
            Id = userId,
            Email = email,
            IsPremium = isPremium,
            PremiumFrom = premiumFrom,
            PremiumTo = premiumTo,
            UserType = userType
        };
    }

    private async Task<SubscriptionPlan> GetCurrentPlanFromSnapshotAsync()
    {
        var snapshot = await GetSnapshotAsync();
        return snapshot.IsPremiumUser ? SubscriptionPlan.PremiumMonthly : SubscriptionPlan.Free;
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
                MonthlyPlanCreationLimit = 4,
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

    private sealed class SupabaseUserRow
    {
        public Guid Id { get; set; }
    }

    private sealed class SupabaseUserWriteRow
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        public bool IsPremium { get; set; }
        public DateTime? PremiumFrom { get; set; }
        public DateTime? PremiumTo { get; set; }
        public int UserType { get; set; }
    }
}
