using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Constants;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using FoodbookApp.Services.Supabase;
using Foodbook.Services;
using Microsoft.EntityFrameworkCore;
#if ANDROID
using Android.BillingClient.Api;
#endif

namespace FoodbookApp.Services.Subscription;

public sealed class PaymentProviderSubscriptionManagementService : ISubscriptionManagementService
{
    private const string CacheKey = "feature_access_snapshot_v1";
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan InProgressRecoveryThreshold = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _context;
    private readonly IAccountService _accountService;
    private readonly SupabaseRestClient _restClient;
    private readonly ISecureStorageAdapter _secureStorage;
    private readonly IClock _clock;
    private readonly HttpClient _httpClient;
    private readonly IAuthTokenStore _tokenStore;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private bool _restoreAttempted;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private TaskCompletionSource<PurchaseResultEventArgs>? _purchaseTcs;
#if ANDROID
    private static BillingClient? _sharedClient;
#endif

    public PaymentProviderSubscriptionManagementService(
        AppDbContext context,
        IAccountService accountService,
        SupabaseRestClient restClient,
        ISecureStorageAdapter secureStorage,
        IClock clock,
        HttpClient httpClient,
        IAuthTokenStore tokenStore)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    }

    public async Task<SubscriptionActionResult> ChangePlanAsync(SubscriptionPlan targetPlan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await RecoverStaleInProgressOperationsAsync(ct);
#if ANDROID
        await TryRestorePurchasesAsync(ct);
#endif

        var account = await GetActiveAccountOrThrowAsync(ct);

        if (targetPlan == SubscriptionPlan.Free)
        {
            await AbandonOpenOperationsAsync(account.Id, "User requested free plan via cancel.", ct);
            OpenGooglePlayManageSubscriptions();
            return new SubscriptionActionResult
            {
                Success = true,
                CurrentPlan = SubscriptionPlan.Free,
                ActionState = SubscriptionActionState.Completed,
                UiMessage = "Otworzono strone zarzadzania subskrypcja w Google Play."
            };
        }

        var existing = await GetLatestOpenOperationAsync(account.Id, ct);
        if (existing != null)
        {
            if (existing.TargetPlan == targetPlan)
            {
                return BuildPendingResult(existing, targetPlan, "Operacja zmiany planu jest juz w toku.");
            }

            existing.Status = SubscriptionOperationStatus.Abandoned;
            existing.UpdatedUtc = _clock.UtcNow;
            existing.LastError = "Operacja zastapiona nowsza prosba uzytkownika.";
            await _context.SaveChangesAsync(ct);
        }

        var productId = BillingConstants.PlanProductMap.GetValueOrDefault(targetPlan);
        if (string.IsNullOrWhiteSpace(productId))
        {
            return new SubscriptionActionResult
            {
                Success = false,
                CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                ActionState = SubscriptionActionState.Failed,
                UiMessage = "Nieznany plan subskrypcji."
            };
        }

        try
        {
            string? existingPurchaseToken = null;
#if ANDROID
            try
            {
                var client = await EnsureBillingClientAsync(ct);
                existingPurchaseToken = await GetExistingPurchaseTokenAsync(client, ct);
            }
            catch
            {
                // Non-critical: plan change will create a new independent subscription
            }
#endif

            var purchaseResult = await LaunchGooglePlayPurchaseFlowAsync(productId, ct, existingPurchaseToken);

            if (!purchaseResult.Success)
            {
                if (purchaseResult.UserCanceled)
                {
                    return new SubscriptionActionResult
                    {
                        Success = false,
                        CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                        ActionState = SubscriptionActionState.Completed,
                        UiMessage = "Anulowano proces zakupu."
                    };
                }

                return new SubscriptionActionResult
                {
                    Success = false,
                    CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                    ActionState = SubscriptionActionState.Failed,
                    UiMessage = $"Blad zakupu: {purchaseResult.ErrorMessage}"
                };
            }

            var operation = new SubscriptionOperationEntry
            {
                AccountId = account.Id,
                TargetPlan = targetPlan,
                Status = SubscriptionOperationStatus.Pending,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                PaymentProvider = "GooglePlay",
                ProviderProductId = purchaseResult.ProductId,
                PurchaseToken = purchaseResult.PurchaseToken,
                PurchaseTimeUtc = _clock.UtcNow,
                CreatedUtc = _clock.UtcNow,
                UpdatedUtc = _clock.UtcNow
            };

            _context.SubscriptionOperations.Add(operation);
            await _context.SaveChangesAsync(ct);

            return await ProcessOperationAsync(account, operation, ct);
        }
        catch (NotSupportedException)
        {
            return new SubscriptionActionResult
            {
                Success = false,
                CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                ActionState = SubscriptionActionState.Failed,
                UiMessage = "Google Play Billing jest dostepny tylko na Androidzie."
            };
        }
        catch (Exception ex)
        {
            return new SubscriptionActionResult
            {
                Success = false,
                CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                ActionState = SubscriptionActionState.Failed,
                UiMessage = $"Nie oczekiwany blad: {ex.Message}"
            };
        }
    }

    public async Task<SubscriptionActionResult> CancelSubscriptionAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var account = await GetActiveAccountOrThrowAsync(ct);
        await AbandonOpenOperationsAsync(account.Id, "Subskrypcja anulowana przez uzytkownika.", ct);

        OpenGooglePlayManageSubscriptions();

        return new SubscriptionActionResult
        {
            Success = true,
            CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
            ActionState = SubscriptionActionState.Completed,
            UiMessage = "Otworzono strone zarzadzania subskrypcja. Premium pozostaje aktywne do konca okresu rozliczeniowego."
        };
    }

    public async Task<SubscriptionPendingOperation?> GetPendingOperationAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await RecoverStaleInProgressOperationsAsync(ct);

        var account = await _accountService.GetActiveAccountAsync(ct);
        if (account == null) return null;

        var operation = await GetLatestOpenOperationAsync(account.Id, ct);
        return operation == null ? null : MapToPendingOperation(operation);
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
                    UiMessage = "Nie znaleziono operacji subskrypcji."
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
                    UiMessage = "Operacja juz zakonczona."
                };
            }

            if (string.IsNullOrWhiteSpace(tracked.PurchaseToken))
            {
                tracked.Status = SubscriptionOperationStatus.Failed;
                tracked.LastError = "Brak PurchaseToken. Wykonaj ponownie zakup.";
                tracked.UpdatedUtc = _clock.UtcNow;
                await _context.SaveChangesAsync(ct);

                return new SubscriptionActionResult
                {
                    Success = false,
                    CurrentPlan = await GetCurrentPlanFromSnapshotAsync(),
                    ActionState = SubscriptionActionState.Failed,
                    OperationId = tracked.Id,
                    UiMessage = "Brak tokena zakupu. Wykonaj ponownie zakup w Google Play."
                };
            }

            var now = _clock.UtcNow;
            tracked.Status = SubscriptionOperationStatus.InProgress;
            tracked.LastAttemptUtc = now;
            tracked.UpdatedUtc = now;
            await _context.SaveChangesAsync(ct);

            try
            {
                var verifyResult = await VerifyPurchaseWithBackendAsync(
                    tracked.ProviderProductId ?? BillingConstants.PlanProductMap[tracked.TargetPlan],
                    tracked.PurchaseToken,
                    BillingConstants.PackageName,
                    ct);

                if (verifyResult.IsValid)
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
                            SubscriptionPlan.PremiumMonthly => "Aktywowano plan Premium Miesieczny.",
                            _ => "Plan subskrypcji zostal zmieniony."
                        }
                    };
                }

                tracked.RetryCount++;
                tracked.LastError = $"Weryfikacja zakupu nieudana: {verifyResult.Error}";
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
                        UiMessage = "Nie udalo sie zweryfikowac zakupu. Sprobuj ponownie lub skontaktuj sie z supportem."
                    };
                }

                tracked.Status = SubscriptionOperationStatus.Pending;
                await _context.SaveChangesAsync(ct);

                return BuildPendingResult(
                    tracked,
                    tracked.TargetPlan,
                    "Weryfikacja zakupu oczekuje. Wznow po odzyskaniu polaczenia.");
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
                        UiMessage = "Nie udalo sie zakonczyc operacji. Wznow recznie po sprawdzeniu polaczenia."
                    };
                }

                tracked.Status = SubscriptionOperationStatus.Pending;
                await _context.SaveChangesAsync(ct);

                return BuildPendingResult(
                    tracked,
                    tracked.TargetPlan,
                    "Operacja zapisana. Wznow po odzyskaniu polaczenia.");
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<VerifyPurchaseResponse> VerifyPurchaseWithBackendAsync(
        string productId,
        string purchaseToken,
        string packageName,
        CancellationToken ct)
    {
        var activeAccountId = await _tokenStore.GetActiveAccountIdAsync();
        string? token = null;
        if (activeAccountId.HasValue)
        {
            token = await _tokenStore.GetAccessTokenAsync(activeAccountId.Value);
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new VerifyPurchaseResponse { IsValid = false, Error = "Brak tokena autoryzacji." };
        }

        var request = new VerifyPurchaseRequest
        {
            ProductId = productId,
            PurchaseToken = purchaseToken,
            PackageName = packageName
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BillingConstants.SupabaseEdgeFunctionUrl)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new VerifyPurchaseResponse { IsValid = false, Error = $"Backend error {response.StatusCode}: {body}" };
        }

        return JsonSerializer.Deserialize<VerifyPurchaseResponse>(body, JsonOptions)
               ?? new VerifyPurchaseResponse { IsValid = false, Error = "Pusta odpowiedz backendu." };
    }

    private async Task<PurchaseResultEventArgs> LaunchGooglePlayPurchaseFlowAsync(string productId, CancellationToken ct, string? existingPurchaseToken = null)
    {
#if ANDROID
        _purchaseTcs = new TaskCompletionSource<PurchaseResultEventArgs>();

        var client = await EnsureBillingClientAsync(ct);
        var activity = Platform.CurrentActivity;
        if (activity == null)
            throw new InvalidOperationException("Brak aktywnego kontekstu Androida.");

        var productDetails = await QueryProductDetailsAsync(client, productId, ct);
        if (productDetails == null)
            throw new InvalidOperationException($"Nie znaleziono produktu: {productId}");

        var offerToken = productDetails.GetSubscriptionOfferDetails()?.FirstOrDefault()?.OfferToken;
        if (string.IsNullOrWhiteSpace(offerToken))
            throw new InvalidOperationException($"Brak oferty subskrypcyjnej dla: {productId}");

        var productDetailsParams = BillingFlowParams.ProductDetailsParams.NewBuilder()
            .SetProductDetails(productDetails)
            .SetOfferToken(offerToken)
            .Build();

        var flowBuilder = BillingFlowParams.NewBuilder()
            .SetProductDetailsParamsList([productDetailsParams]);

        if (!string.IsNullOrWhiteSpace(existingPurchaseToken))
        {
            try
            {
                var updateParams = BuildSubscriptionUpdateParams(existingPurchaseToken);
                flowBuilder.SetSubscriptionUpdateParams(updateParams);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaymentProvider] Failed to set subscription update params: {ex.Message}");
            }
        }

        var billingFlowParams = flowBuilder.Build();

        var billingResult = client.LaunchBillingFlow(activity, billingFlowParams);

        if (billingResult.ResponseCode != BillingResponseCode.Ok)
        {
            return new PurchaseResultEventArgs
            {
                Success = false,
                ErrorMessage = $"Blad uruchomienia platnosci: {billingResult.ResponseCode}"
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            return await _purchaseTcs.Task.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return new PurchaseResultEventArgs
            {
                Success = false,
                ErrorMessage = "Uplynal limit czasu oczekiwania na zakup.",
                UserCanceled = true
            };
        }
        finally
        {
            _purchaseTcs = null;
        }
#else
        await Task.CompletedTask;
        throw new NotSupportedException("Google Play Billing is only available on Android.");
#endif
    }

#if ANDROID
    private async Task<BillingClient> EnsureBillingClientAsync(CancellationToken ct)
    {
        if (_sharedClient != null && _sharedClient.IsReady)
        {
            return _sharedClient;
        }

        if (_sharedClient != null)
        {
            try { _sharedClient.EndConnection(); } catch { }
            _sharedClient = null;
        }

        var connectTcs = new TaskCompletionSource<bool>();

        var client = BillingClient.NewBuilder(Platform.AppContext!)
            .SetListener(new BillingPurchaseListener(OnPurchasesUpdated))
            .EnablePendingPurchases()
            .Build();

        client.StartConnection(new BillingConnectionCallback(connectTcs));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await connectTcs.Task.WaitAsync(timeoutCts.Token);
            _sharedClient = client;
            return client;
        }
        catch (OperationCanceledException)
        {
            try { client.EndConnection(); } catch { }
            throw new InvalidOperationException("Nie mozna polaczyc sie z Google Play Billing.");
        }
    }

    private static async Task<ProductDetails?> QueryProductDetailsAsync(BillingClient client, string productId, CancellationToken ct)
    {
        var queryParams = QueryProductDetailsParams.NewBuilder()
            .SetProductList(
            [
                QueryProductDetailsParams.Product.NewBuilder()
                    .SetProductId(productId)
                    .SetProductType("subs")
                    .Build()
            ])
            .Build();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            var result = await client.QueryProductDetailsAsync(queryParams).WaitAsync(timeoutCts.Token);
            return ExtractFirstProductDetail(result);
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException($"Timeout pobierania ProductDetails dla: {productId}");
        }
    }

    private void OnPurchasesUpdated(BillingResult billingResult, IList<Purchase>? purchases)
    {
        var tcs = _purchaseTcs;
        if (tcs == null) return;

        if (billingResult.ResponseCode == BillingResponseCode.Ok && purchases != null && purchases.Count > 0)
        {
            var purchase = purchases[0];
            tcs.TrySetResult(new PurchaseResultEventArgs
            {
                Success = true,
                PurchaseToken = purchase.PurchaseToken,
                ProductId = purchase.Products?.FirstOrDefault() ?? string.Empty,
                OrderId = purchase.OrderId,
                PurchaseTime = purchase.PurchaseTime
            });
            return;
        }

        if (billingResult.ResponseCode == (BillingResponseCode)1)
        {
            tcs.TrySetResult(new PurchaseResultEventArgs { Success = false, UserCanceled = true });
            return;
        }

        tcs.TrySetResult(new PurchaseResultEventArgs
        {
            Success = false,
            ErrorMessage = $"Google Play Billing error: {billingResult.ResponseCode} - {billingResult.DebugMessage}"
        });
    }

    private static BillingFlowParams.SubscriptionUpdateParams BuildSubscriptionUpdateParams(string oldPurchaseToken)
    {
        var builder = BillingFlowParams.SubscriptionUpdateParams.NewBuilder();
        builder.SetOldPurchaseToken(oldPurchaseToken);
        return builder.Build();
    }

    private async Task<string?> GetExistingPurchaseTokenAsync(BillingClient client, CancellationToken ct)
    {
        try
        {
            var queryType = typeof(BillingClient).Assembly.GetType("Android.BillingClient.Api.QueryPurchasesParams");
            if (queryType == null) return null;

            var newBuilder = queryType.GetMethod("NewBuilder", Type.EmptyTypes);
            if (newBuilder == null) return null;

            var builder = newBuilder.Invoke(null, null);
            if (builder == null) return null;

            var setProductType = builder.GetType().GetMethod("SetProductType", [typeof(string)]);
            setProductType?.Invoke(builder, ["subs"]);

            var build = builder.GetType().GetMethod("Build", Type.EmptyTypes);
            var queryParams = build?.Invoke(builder, null);
            if (queryParams == null) return null;

            var taskMethod = typeof(BillingClient).GetMethod("QueryPurchasesAsync", [queryType]);
            if (taskMethod == null) return null;

            var task = (Task?)taskMethod.Invoke(client, [queryParams]);
            if (task == null) return null;

            await task;
            var resultProp = task.GetType().GetProperty("Result");
            var result = resultProp?.GetValue(task);
            if (result == null) return null;

            var purchasesListProp = result.GetType().GetProperty("PurchasesList");
            var purchasesList = purchasesListProp?.GetValue(result) as System.Collections.IList;
            var token = purchasesList?.Count > 0
                ? purchasesList[0]?.GetType().GetProperty("PurchaseToken")?.GetValue(purchasesList[0]) as string
                : null;

            return token;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentProvider] Failed to query existing purchases: {ex.Message}");
            return null;
        }
    }

    private async Task TryRestorePurchasesAsync(CancellationToken ct)
    {
        if (_restoreAttempted) return;
        _restoreAttempted = true;

#if ANDROID
        try
        {
            var client = await EnsureBillingClientAsync(ct);
            var token = await GetExistingPurchaseTokenAsync(client, ct);
            if (string.IsNullOrWhiteSpace(token)) return;

            var alreadyRestored = await _context.SubscriptionOperations
                .AnyAsync(e => e.PurchaseToken == token, ct);
            if (alreadyRestored) return;

            var productId = await ResolveProductIdFromPurchaseTokenAsync(client, token, ct);
            if (string.IsNullOrWhiteSpace(productId)) return;

            var verifyResult = await VerifyPurchaseWithBackendAsync(productId, token, BillingConstants.PackageName, ct);
            if (!verifyResult.IsValid) return;

            var account = await _accountService.GetActiveAccountAsync(ct);
            if (account == null) return;

            var plan = MapProductIdToPlan(productId);
            await UpsertSupabaseUserPlanAsync(account, plan, ct);

            var snapshot = await GetSnapshotAsync();
            ApplyPlan(snapshot, plan);
            snapshot.LastSyncedUtc = _clock.UtcNow;
            await SaveSnapshotAsync(snapshot);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentProvider] Restore failed: {ex.Message}");
        }
#endif
        await Task.CompletedTask;
    }

    private async Task<string?> ResolveProductIdFromPurchaseTokenAsync(BillingClient client, string purchaseToken, CancellationToken ct)
    {
        try
        {
            var queryType = typeof(BillingClient).Assembly.GetType("Android.BillingClient.Api.QueryPurchasesParams");
            if (queryType == null) return null;

            var newBuilder = queryType.GetMethod("NewBuilder", Type.EmptyTypes);
            if (newBuilder == null) return null;

            var builder = newBuilder.Invoke(null, null);
            if (builder == null) return null;

            var setProductType = builder.GetType().GetMethod("SetProductType", [typeof(string)]);
            setProductType?.Invoke(builder, ["subs"]);

            var build = builder.GetType().GetMethod("Build", Type.EmptyTypes);
            var queryParams = build?.Invoke(builder, null);
            if (queryParams == null) return null;

            var taskMethod = typeof(BillingClient).GetMethod("QueryPurchasesAsync", [queryType]);
            if (taskMethod == null) return null;

            var task = (Task?)taskMethod.Invoke(client, [queryParams]);
            if (task == null) return null;

            await task;
            var resultProp = task.GetType().GetProperty("Result");
            var result = resultProp?.GetValue(task);
            if (result == null) return null;

            var purchasesListProp = result.GetType().GetProperty("PurchasesList");
            var purchasesList = purchasesListProp?.GetValue(result) as System.Collections.IList;
            if (purchasesList == null || purchasesList.Count == 0) return null;

            foreach (var p in purchasesList)
            {
                var pt = p?.GetType().GetProperty("PurchaseToken")?.GetValue(p) as string;
                if (pt == purchaseToken)
                {
                    var products = p?.GetType().GetProperty("Products")?.GetValue(p) as System.Collections.IList;
                    return products?.Count > 0 ? products[0] as string : null;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static SubscriptionPlan MapProductIdToPlan(string productId) => productId switch
    {
        "foodbook_premium_monthly" => SubscriptionPlan.PremiumMonthly,
        "foodbook_premium_yearly" => SubscriptionPlan.PremiumYearly,
        _ => SubscriptionPlan.Free
    };

    private sealed class BillingPurchaseListener : Java.Lang.Object, IPurchasesUpdatedListener
    {
        private readonly Action<BillingResult, IList<Purchase>?> _onUpdated;

        public BillingPurchaseListener(Action<BillingResult, IList<Purchase>?> onUpdated)
        {
            _onUpdated = onUpdated;
        }

        public void OnPurchasesUpdated(BillingResult billingResult, IList<Purchase>? purchases)
        {
            MainThread.BeginInvokeOnMainThread(() => _onUpdated(billingResult, purchases));
        }
    }

    private sealed class BillingConnectionCallback : Java.Lang.Object, IBillingClientStateListener
    {
        private readonly TaskCompletionSource<bool> _tcs;

        public BillingConnectionCallback(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public void OnBillingServiceDisconnected()
        {
            System.Diagnostics.Debug.WriteLine("[PaymentProvider] Billing service disconnected");
        }

        public void OnBillingSetupFinished(BillingResult billingResult)
        {
            if (billingResult.ResponseCode == BillingResponseCode.Ok)
                _tcs.TrySetResult(true);
            else
                _tcs.TrySetException(new InvalidOperationException($"Billing setup failed: {billingResult.ResponseCode}"));
        }
    }

    private static ProductDetails? ExtractFirstProductDetail(object result)
    {
        var list = result?.GetType().GetProperty("ProductDetailsList")?.GetValue(result) as System.Collections.IList;
        return list?.Count > 0 ? list[0] as ProductDetails : null;
    }

#endif

    private static void OpenGooglePlayManageSubscriptions()
    {
#if ANDROID
        try
        {
            var appContext = Platform.AppContext;
            if (appContext == null) return;

            var packageName = appContext.PackageName ?? "com.ppapps.foodbookapp";
            var uri = Android.Net.Uri.Parse($"https://play.google.com/store/account/subscriptions?sku={packageName}");
            if (uri != null)
            {
                var intent = new Android.Content.Intent(Android.Content.Intent.ActionView, uri);
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                appContext.StartActivity(intent);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentProvider] Failed to open Play Store: {ex.Message}");
        }
#endif
    }

    private async Task RecoverStaleInProgressOperationsAsync(CancellationToken ct)
    {
        var threshold = _clock.UtcNow - InProgressRecoveryThreshold;
        var staleEntries = await _context.SubscriptionOperations
            .Where(e => e.Status == SubscriptionOperationStatus.InProgress
                        && e.LastAttemptUtc.HasValue
                        && e.LastAttemptUtc.Value < threshold)
            .ToListAsync(ct);

        if (staleEntries.Count == 0) return;

        foreach (var entry in staleEntries)
        {
            entry.Status = SubscriptionOperationStatus.Pending;
            entry.UpdatedUtc = _clock.UtcNow;
            entry.LastError = "Wykryto przerwana operacje. Gotowa do wznowienia.";
        }

        await _context.SaveChangesAsync(ct);
    }

    private Task<SubscriptionOperationEntry?> GetLatestOpenOperationAsync(Guid accountId, CancellationToken ct)
        => _context.SubscriptionOperations
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

        if (openEntries.Count == 0) return;

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
        return account ?? throw new InvalidOperationException("Brak aktywnego konta.");
    }

    private async Task UpsertSupabaseUserPlanAsync(AuthAccount account, SubscriptionPlan targetPlan, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(account.SupabaseUserId))
            throw new InvalidOperationException("Aktywne konto nie ma powiazanego identyfikatora Supabase.");

        if (!Guid.TryParse(account.SupabaseUserId, out var supabaseUserId))
            throw new InvalidOperationException("Nieprawidlowy format identyfikatora Supabase.");

        var now = _clock.UtcNow;
        var payload = BuildSupabaseUserPayload(account.Email, supabaseUserId, targetPlan, now);
        var existing = await _restClient.GetAsync<SupabaseUserRow>("users", $"id=eq.{account.SupabaseUserId}&limit=1", ct);

        if (existing.Count == 0)
            await _restClient.InsertAsync("users", payload, ct);
        else
            await _restClient.UpdateAsync("users", supabaseUserId, payload, ct);
    }

    private static SupabaseUserWriteRow BuildSupabaseUserPayload(string? email, Guid userId, SubscriptionPlan targetPlan, DateTime utcNow)
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
                return CreateDefaultSnapshot();

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

    private static SubscriptionPendingOperation MapToPendingOperation(SubscriptionOperationEntry entry)
        => new()
        {
            Id = entry.Id,
            TargetPlan = entry.TargetPlan,
            Status = entry.Status,
            RetryCount = entry.RetryCount,
            LastError = entry.LastError,
            CreatedUtc = entry.CreatedUtc,
            LastAttemptUtc = entry.LastAttemptUtc
        };

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

    private sealed class VerifyPurchaseRequest
    {
        public string ProductId { get; set; } = string.Empty;
        public string PurchaseToken { get; set; } = string.Empty;
        public string PackageName { get; set; } = string.Empty;
    }

    private sealed class VerifyPurchaseResponse
    {
        public bool IsValid { get; set; }
        public string? Plan { get; set; }
        public string? PremiumFrom { get; set; }
        public string? PremiumTo { get; set; }
        public string? Error { get; set; }
    }

    private sealed class PurchaseResultEventArgs
    {
        public bool Success { get; set; }
        public bool UserCanceled { get; set; }
        public string? PurchaseToken { get; set; }
        public string? ProductId { get; set; }
        public string? OrderId { get; set; }
        public long PurchaseTime { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
