using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Data;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views.Components;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace Foodbook.Views;

public partial class ProfilePage : ContentPage
{
    private ISupabaseAuthService? _supabaseAuth;
    private ISupabaseSyncService? _syncService;
    private IAccountService? _accountService;
    private IFeatureAccessService? _featureAccessService;
    private ISubscriptionManagementService? _subscriptionManagementService;
    private CancellationTokenSource? _syncRefreshCts;
    private bool _isLoggedIn;
    private bool _suppressToggle;
    private bool _eventsSubscribed;
    private string _currentUserEmail = string.Empty;
    private DateTime _lastManualSyncQueueRefreshUtc = DateTime.MinValue;
    private bool _syncQueueHasMoreItems;
    private int _syncQueueOffset;
    private SyncQueueSummaryState _syncQueueSummary = SyncQueueSummaryState.Empty;
    private bool _isSubscriptionActionInProgress;
    private bool _subscriptionAutoResumeAttempted;

    private const string SyncChoiceKeyPrefix = "cloudsync.enabled:";
    private const int SyncQueuePageSize = 50;
    private static readonly TimeSpan SyncQueueManualRefreshThrottle = TimeSpan.FromSeconds(7);
    private static readonly Color SyncSuccessColor = Color.FromArgb("#2E7D32");
    private static readonly Color SyncErrorColor = Color.FromArgb("#C62828");
    private static readonly Color SyncNeutralColor = Color.FromArgb("#6B7280");

    public ObservableCollection<SyncQueueDisplayItem> SyncQueueItems { get; } = new();

    public ProfilePage()
    {
        InitializeComponent();
        ShowMainProfileView();
        UpdateUiState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ShowMainProfileView();
        _subscriptionAutoResumeAttempted = false;
        SubscribeToAppEvents();
        StartSyncStatusRefresh();
        await CheckAndRestoreSessionAsync();
        await LoadProfileStatsAsync();
        await RefreshSubscriptionSectionAsync();
        if (_isLoggedIn)
        {
            await LoadSyncStatusAsync();
            await TryAutoResumePendingOperationAsync();
        }
    }

    protected override void OnDisappearing()
    {
        UnsubscribeFromAppEvents();
        StopSyncStatusRefresh();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (PlanDetailsView.IsVisible || SyncQueueDetailsView.IsVisible)
        {
            ShowMainProfileView();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    #region Session

    private async Task CheckAndRestoreSessionAsync()
    {
        try
        {
            var auth = GetSupabaseAuth();
            if (!string.IsNullOrWhiteSpace(auth?.CurrentSession?.AccessToken))
            {
                _isLoggedIn = true;
                _currentUserEmail = auth.CurrentSession.User?.Email ?? string.Empty;
                UpdateUiState();
                await AutoEnableSyncIfSavedAsync();
                return;
            }

            var accountService = GetAccountService();
            if (accountService != null)
            {
                var account = await accountService.GetActiveAccountAsync();
                if (account != null && await accountService.TryAutoLoginAsync())
                {
                    _isLoggedIn = true;
                    _currentUserEmail = GetSupabaseAuth()?.CurrentSession?.User?.Email
                        ?? account.Email ?? string.Empty;
                    UpdateUiState();
                    await AutoEnableSyncIfSavedAsync();
                    return;
                }
            }

            _isLoggedIn = false;
            UpdateUiState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] Session check error: {ex.Message}");
            _isLoggedIn = false;
            UpdateUiState();
        }
    }

    /// <summary>
    /// Re-enables sync silently after auto-login if user had it enabled previously.
    /// </summary>
    private async Task AutoEnableSyncIfSavedAsync()
    {
        try
        {
            var syncService = GetSyncService();
            if (syncService == null) return;

            var key = await GetSyncChoiceKeyAsync();
            if (string.IsNullOrEmpty(key)) return;

            var saved = await SecureStorage.GetAsync(key);
            if (saved == "true" && !await syncService.IsCloudSyncEnabledAsync())
            {
                ShowSpinner("Wznawianie synchronizacji...");
                await syncService.EnableCloudSyncAsync();
                HideSpinner();
            }
        }
        catch (Exception ex)
        {
            HideSpinner();
            Debug.WriteLine($"[ProfilePage] AutoEnableSync error: {ex.Message}");
        }
    }

    #endregion

    #region Sync Status Loop

    private void StartSyncStatusRefresh()
    {
        StopSyncStatusRefresh();
        _syncRefreshCts = new CancellationTokenSource();
        _ = RefreshSyncStatusLoopAsync(_syncRefreshCts.Token);
    }

    private void StopSyncStatusRefresh()
    {
        _syncRefreshCts?.Cancel();
        _syncRefreshCts?.Dispose();
        _syncRefreshCts = null;
    }

    private async Task RefreshSyncStatusLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (_isLoggedIn)
                    await LoadSyncStatusAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task LoadSyncStatusAsync()
    {
        try
        {
            var syncService = GetSyncService();
            if (syncService == null) return;

            var isEnabled = await syncService.IsCloudSyncEnabledAsync();
            var pending = isEnabled ? await syncService.GetPendingCountAsync() : 0;
            Foodbook.Models.SyncState? state = isEnabled ? await syncService.GetSyncStateAsync() : null;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _suppressToggle = true;
                try { CloudSyncSwitch.IsToggled = isEnabled; }
                finally { _suppressToggle = false; }

                if (isEnabled && state != null)
                {
                    string lastSyncText;
                    if (state.LastSyncUtc.HasValue)
                    {
                        lastSyncText = $"Zsynchronizowano • {state.LastSyncUtc.Value.ToLocalTime():HH:mm}";
                    }
                    else
                    {
                        lastSyncText = "Zsynchronizowano";
                    }

                    SyncStatusLabel.Text = pending > 0
                        ? $"Synchronizacja: {pending} oczekujących"
                        : lastSyncText;
                    SyncStatusLabel.IsVisible = true;
                }
                else
                {
                    SyncStatusLabel.IsVisible = false;
                }
            });
        }
        catch (Exception ex) { Debug.WriteLine($"[ProfilePage] LoadSyncStatus: {ex.Message}"); }
    }

    #endregion

    #region Sync Toggle

    private async void OnCloudSyncToggled(object sender, ToggledEventArgs e)
    {
        if (_suppressToggle) return;

        var syncService = GetSyncService();
        if (syncService == null)
        {
            SetToggle(!e.Value);
            await DisplayAlert("Błąd", "Usługa synchronizacji jest niedostępna.", "OK");
            return;
        }

        try
        {
            if (e.Value)
            {
                ShowSpinner("Włączanie synchronizacji...");
                await syncService.EnableCloudSyncAsync();
                await SaveSyncChoiceAsync(true);
                HideSpinner();
                SyncStatusLabel.Text = "Synchronizacja włączona";
                SyncStatusLabel.IsVisible = true;
            }
            else
            {
                ShowSpinner("Wyłączanie synchronizacji...");
                await syncService.DisableCloudSyncAsync();
                await SaveSyncChoiceAsync(false);
                HideSpinner();
                SyncStatusLabel.IsVisible = false;
            }

            await Task.Delay(1500);
            await LoadSyncStatusAsync();
        }
        catch (Exception ex)
        {
            HideSpinner();
            SetToggle(!e.Value);
            await DisplayAlert("Błąd synchronizacji", ex.Message, "OK");
        }
    }

    private void SetToggle(bool value)
    {
        _suppressToggle = true;
        try { CloudSyncSwitch.IsToggled = value; }
        finally { _suppressToggle = false; }
    }

    #endregion

    #region Profile Stats & Subscription

    private void SubscribeToAppEvents()
    {
        if (_eventsSubscribed)
            return;

        AppEvents.PlanChangedAsync += OnAppDataChangedAsync;
        AppEvents.RecipesChangedAsync += OnAppDataChangedAsync;
        _eventsSubscribed = true;
    }

    private void UnsubscribeFromAppEvents()
    {
        if (!_eventsSubscribed)
            return;

        AppEvents.PlanChangedAsync -= OnAppDataChangedAsync;
        AppEvents.RecipesChangedAsync -= OnAppDataChangedAsync;
        _eventsSubscribed = false;
    }

    private async Task OnAppDataChangedAsync()
    {
        await RefreshProfileDataAsync();
    }

    private async Task RefreshProfileDataAsync()
    {
        try
        {
            await LoadProfileStatsAsync();
            await RefreshSubscriptionSectionAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] RefreshProfileDataAsync error: {ex.Message}");
        }
    }

    private async Task LoadProfileStatsAsync()
    {
        try
        {
            var serviceProvider = FoodbookApp.MauiProgram.ServiceProvider;
            if (serviceProvider == null)
                return;

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var utcNow = DateTime.UtcNow;

            var recipesCount = await db.Recipes.CountAsync();
            var plansCount = await db.Plans.CountAsync(p =>
                p.Type == PlanType.Planner &&
                !p.IsArchived &&
                p.StartDate <= utcNow &&
                p.EndDate >= utcNow);
            var listsCount = await db.Plans.CountAsync(p =>
                p.Type == PlanType.ShoppingList &&
                !p.IsArchived);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RecipesCountLabel.Text = recipesCount.ToString();
                PlansCountLabel.Text = plansCount.ToString();
                ListsCountLabel.Text = listsCount.ToString();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] LoadProfileStatsAsync error: {ex.Message}");
        }
    }

    private async Task LoadCurrentSubscriptionUiAsync()
    {
        try
        {
            var featureAccess = GetFeatureAccessService();
            if (featureAccess == null)
                return;

            var isPremium = await featureAccess.CanUsePremiumFeatureAsync(PremiumFeature.AutoPlanner);
            var currentPlanName = isPremium ? "Premium" : "Free";
            var currentPlanStatus = isPremium ? "Aktywny" : "Nieaktywny";
            var renewalText = isPremium
                ? "Dostęp premium aktywny"
                : "Brak aktywnej subskrypcji";
            var limitsText = await BuildPlanLimitsTextAsync(featureAccess, isPremium);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                CurrentPlanNameLabel.Text = currentPlanName;
                CurrentPlanStatusLabel.Text = currentPlanStatus;
                CurrentPlanRenewalLabel.Text = renewalText;

                CurrentPlanNameDetailsLabel.Text = currentPlanName;
                CurrentPlanStatusDetailsLabel.Text = currentPlanStatus;
                CurrentPlanRenewalDetailsLabel.Text = renewalText;
                CurrentPlanLimitsDetailsLabel.Text = limitsText;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] LoadCurrentSubscriptionUiAsync error: {ex.Message}");
        }
    }

    #endregion

    #region Subscription management

    private async Task RefreshSubscriptionSectionAsync()
    {
        await LoadCurrentSubscriptionUiAsync();
        await LoadPendingSubscriptionUiAsync();
    }

    private async Task<string> BuildPlanLimitsTextAsync(IFeatureAccessService featureAccess, bool isPremium)
    {
        if (isPremium)
        {
            return "Limity: brak ograniczeń planu premium.";
        }

        var canCreatePlan = await featureAccess.CanCreatePlanAsync();
        return canCreatePlan
            ? "Limity: możesz tworzyć kolejne plany miesięczne."
            : "Limity: osiągnięto miesięczny limit planów.";
    }

    private async void OnSwitchToFreeClicked(object sender, EventArgs e)
    {
        await ExecuteSubscriptionActionAsync(
            action: service => service.ChangePlanAsync(SubscriptionPlan.Free, CancellationToken.None),
            title: "Zmiana planu");
    }

    private async void OnSwitchToYearlyClicked(object sender, EventArgs e)
    {
        await ExecuteSubscriptionActionAsync(
            action: service => service.ChangePlanAsync(SubscriptionPlan.PremiumYearly, CancellationToken.None),
            title: "Zmiana planu");
    }

    private async void OnSwitchToMonthlyClicked(object sender, EventArgs e)
    {
        await ExecuteSubscriptionActionAsync(
            action: service => service.ChangePlanAsync(SubscriptionPlan.PremiumMonthly, CancellationToken.None),
            title: "Zmiana planu");
    }

    private async void OnCancelSubscriptionClicked(object sender, EventArgs e)
    {
        await ExecuteSubscriptionActionAsync(
            action: service => service.CancelSubscriptionAsync(CancellationToken.None),
            title: "Subskrypcja");
    }

    private async void OnResumePendingSubscriptionClicked(object sender, EventArgs e)
    {
        await ExecuteSubscriptionActionAsync(
            action: service => service.ResumePendingOperationAsync(CancellationToken.None),
            title: "Wznowienie subskrypcji");
    }

    private async Task ExecuteSubscriptionActionAsync(
        Func<ISubscriptionManagementService, Task<SubscriptionActionResult>> action,
        string title)
    {
        if (_isSubscriptionActionInProgress)
        {
            return;
        }

        var service = GetSubscriptionManagementService();
        if (service == null)
        {
            await DisplayAlert("Błąd", "Usługa subskrypcji jest niedostępna.", "OK");
            return;
        }

        _isSubscriptionActionInProgress = true;
        SetSubscriptionActionButtonsEnabled(false);
        ShowSpinner("Aktualizacja subskrypcji...");

        try
        {
            var result = await action(service);
            await HandleSubscriptionActionResultAsync(result, title, showAlert: true);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd subskrypcji", ex.Message, "OK");
        }
        finally
        {
            HideSpinner();
            _isSubscriptionActionInProgress = false;
            SetSubscriptionActionButtonsEnabled(true);
        }
    }

    private async Task HandleSubscriptionActionResultAsync(SubscriptionActionResult result, string title, bool showAlert)
    {
        var featureAccess = GetFeatureAccessService();
        if (featureAccess != null)
        {
            await featureAccess.RefreshAccessAsync();
        }

        await RefreshSubscriptionSectionAsync();

        if (!showAlert || string.IsNullOrWhiteSpace(result.UiMessage))
        {
            return;
        }

        await DisplayAlert(title, result.UiMessage, "OK");
    }

    private async Task TryAutoResumePendingOperationAsync()
    {
        if (_subscriptionAutoResumeAttempted || !_isLoggedIn)
        {
            return;
        }

        _subscriptionAutoResumeAttempted = true;
        var service = GetSubscriptionManagementService();
        if (service == null)
        {
            return;
        }

        try
        {
            var pending = await service.GetPendingOperationAsync(CancellationToken.None);
            if (pending == null)
            {
                return;
            }

            var result = await service.ResumePendingOperationAsync(CancellationToken.None);
            await HandleSubscriptionActionResultAsync(result, "Wznowienie subskrypcji", showAlert: false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] Auto resume subscription failed: {ex.Message}");
        }
    }

    private async Task LoadPendingSubscriptionUiAsync()
    {
        if (!_isLoggedIn)
        {
            ApplyPendingSubscriptionUi(null);
            return;
        }

        var service = GetSubscriptionManagementService();
        if (service == null)
        {
            ApplyPendingSubscriptionUi(null);
            return;
        }

        try
        {
            var pending = await service.GetPendingOperationAsync(CancellationToken.None);
            ApplyPendingSubscriptionUi(pending);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] LoadPendingSubscriptionUiAsync error: {ex.Message}");
            ApplyPendingSubscriptionUi(null);
        }
    }

    private void ApplyPendingSubscriptionUi(SubscriptionPendingOperation? pending)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (pending == null)
            {
                PendingSubscriptionCard.IsVisible = false;
                PendingSubscriptionErrorLabel.IsVisible = false;
                PendingSubscriptionErrorLabel.Text = string.Empty;
                return;
            }

            PendingSubscriptionCard.IsVisible = true;
            PendingSubscriptionStatusLabel.Text = pending.Status switch
            {
                SubscriptionOperationStatus.InProgress => ProfilePageResources.SubscriptionPendingStatusProcessing,
                SubscriptionOperationStatus.Failed => ProfilePageResources.SubscriptionPendingStatusFailed,
                SubscriptionOperationStatus.Abandoned => ProfilePageResources.SubscriptionPendingStatusCancelled,
                _ => ProfilePageResources.SubscriptionPendingStatusPending
            };

            PendingSubscriptionTargetLabel.Text = string.Format(
                ProfilePageResources.SubscriptionPendingPlanFormat,
                pending.TargetPlan switch
                {
                    SubscriptionPlan.PremiumYearly => ProfilePageResources.PlanNamePremiumYearly,
                    SubscriptionPlan.PremiumMonthly => ProfilePageResources.PlanNamePremiumMonthly,
                    _ => ProfilePageResources.PlanNameFree
                });

            PendingSubscriptionLastAttemptLabel.Text = pending.LastAttemptUtc.HasValue
                ? string.Format(ProfilePageResources.SubscriptionPendingLastAttemptFormat, pending.LastAttemptUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm"))
                : ProfilePageResources.SubscriptionPendingNoAttempt;

            var hasError = !string.IsNullOrWhiteSpace(pending.LastError);
            PendingSubscriptionErrorLabel.IsVisible = hasError;
            PendingSubscriptionErrorLabel.Text = hasError
                ? string.Format(ProfilePageResources.SubscriptionPendingErrorFormat, pending.LastError)
                : string.Empty;
        });
    }

    #endregion

    #region Login / Logout

    private async void OnProfileFetchJwtClicked(object sender, EventArgs e)
    {
        var auth = GetSupabaseAuth();
        if (auth == null) { await DisplayAlert("Błąd", "Usługa autoryzacji niedostępna.", "OK"); return; }

        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Logowanie", "Wypełnij e-mail i hasło.", "OK");
            return;
        }

        StatusLabel.Text = "Logowanie...";
        ShowSpinner("Logowanie...");

        try
        {
            var session = await auth.SignInAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                HideSpinner();
                StatusLabel.Text = string.Empty;
                await DisplayAlert("Błąd logowania", "Nieprawidłowe dane logowania.", "OK");
                return;
            }

            _isLoggedIn = true;
            _currentUserEmail = session.User?.Email ?? email;
            PasswordEntry.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            HideSpinner();
            UpdateUiState();

            // Single, simple question — no local/cloud priority dialog
            var enableSync = await DisplayAlert(
                "Synchronizacja z chmurą",
                "Czy chcesz włączyć synchronizację danych? Twoje dane będą automatycznie synchronizowane z chmurą.",
                "Tak, włącz",
                "Nie teraz");

            if (enableSync)
            {
                ShowSpinner("Synchronizacja danych...");
                var syncService = GetSyncService();
                if (syncService != null)
                {
                    await syncService.EnableCloudSyncAsync();
                    await SaveSyncChoiceAsync(true);
                }
                HideSpinner();
            }
            else
            {
                await SaveSyncChoiceAsync(false);
            }

            await LoadSyncStatusAsync();
        }
        catch (Exception ex)
        {
            HideSpinner();
            StatusLabel.Text = string.Empty;
            await DisplayAlert("Błąd", ex.InnerException?.Message ?? ex.Message, "OK");
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        try
        {
            var popup = FoodbookApp.MauiProgram.ServiceProvider?.GetService<RegisterPopup>() ?? new RegisterPopup();
            var hostPage = Application.Current?.Windows.FirstOrDefault()?.Page ?? this;
            await hostPage.ShowPopupAsync(popup);

            var registered = popup.ResultTask.IsCompleted && await popup.ResultTask;
            if (!registered) return;

            var session = GetSupabaseAuth()?.CurrentSession;
            if (session != null && !string.IsNullOrWhiteSpace(session.AccessToken))
            {
                _isLoggedIn = true;
                _currentUserEmail = session.User?.Email ?? string.Empty;
                UpdateUiState();
                await LoadSyncStatusAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", ex.InnerException?.Message ?? ex.Message, "OK");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var accountService = GetAccountService();
            if (accountService != null)
                await accountService.SignOutAsync(clearAutoLogin: true);
            else
                await (GetSupabaseAuth()?.SignOutAsync() ?? Task.CompletedTask);

            var syncService = GetSyncService();
            if (syncService != null)
                await syncService.DisableCloudSyncAsync();

            await ClearSyncChoiceAsync();

            _isLoggedIn = false;
            _subscriptionAutoResumeAttempted = false;
            _currentUserEmail = string.Empty;
            EmailEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            ApplyPendingSubscriptionUi(null);
            UpdateUiState();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd wylogowania", ex.Message, "OK");
        }
    }

    #endregion

    #region Force Sync

    private async void OnForceSyncClicked(object sender, EventArgs e)
    {
        var sync = GetSyncService();
        if (sync == null) { await DisplayAlert("Błąd", "Usługa synchronizacji niedostępna.", "OK"); return; }

        ShowSpinner("Synchronizacja...");
        try
        {
            var result = await sync.ForceSyncAllAsync();
            HideSpinner();

            var msg = $"Zsynchronizowano: {result.ItemsProcessed}";
            if (result.ItemsRemaining > 0) msg += $"\nOczekujące: {result.ItemsRemaining}";
            if (result.ItemsFailed > 0) msg += $"\nBłędy: {result.ItemsFailed}";
            msg += $"\nCzas: {result.Duration.TotalSeconds:F1}s";

            await DisplayAlert("Synchronizacja zakończona", msg, "OK");
        }
        catch (Exception ex)
        {
            HideSpinner();
            await DisplayAlert("Błąd synchronizacji", ex.Message, "OK");
        }
        finally
        {
            await LoadSyncStatusAsync();
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateUiState()
    {
        HeroBar.IsVisible = true;
        AccountContentSection.IsVisible = _isLoggedIn;
        LoggedInPanel.IsVisible = _isLoggedIn;
        LoginPanel.IsVisible = !_isLoggedIn;

        if (!_isLoggedIn) SyncStatusLabel.IsVisible = false;
        if (!_isLoggedIn) PendingSubscriptionCard.IsVisible = false;

        HeroUserEmailLabel.Text = _isLoggedIn ? _currentUserEmail : string.Empty;
        SetSubscriptionActionButtonsEnabled(_isLoggedIn && !_isSubscriptionActionInProgress);
    }

    private void SetSubscriptionActionButtonsEnabled(bool isEnabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SwitchToFreePlanButton.IsEnabled = isEnabled;
            SwitchToMonthlyPlanButton.IsEnabled = isEnabled;
            SwitchToYearlyPlanButton.IsEnabled = isEnabled;
            CancelSubscriptionButton.IsEnabled = isEnabled;
            ResumeSubscriptionButton.IsEnabled = isEnabled;
        });
    }

    private void ShowSpinner(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncOverlayLabel.Text = message;
            SyncActivityIndicator.IsRunning = true;
            SyncingOverlay.IsVisible = true;
            CloudSyncSwitch.IsEnabled = false;
            SetSubscriptionActionButtonsEnabled(false);
        });
    }

    private void HideSpinner()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncingOverlay.IsVisible = false;
            SyncActivityIndicator.IsRunning = false;
            CloudSyncSwitch.IsEnabled = true;
            SetSubscriptionActionButtonsEnabled(_isLoggedIn && !_isSubscriptionActionInProgress);
        });
    }

    #endregion

    #region SecureStorage Helpers

    private async Task<string?> GetSyncChoiceKeyAsync()
    {
        try
        {
            var tokenStore = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IAuthTokenStore>();
            if (tokenStore == null) return null;
            var id = await tokenStore.GetActiveAccountIdAsync();
            return id.HasValue ? $"{SyncChoiceKeyPrefix}{id.Value:N}" : null;
        }
        catch { return null; }
    }

    private async Task SaveSyncChoiceAsync(bool enabled)
    {
        var key = await GetSyncChoiceKeyAsync();
        if (key != null)
            await SecureStorage.SetAsync(key, enabled ? "true" : "false");
    }

    private async Task ClearSyncChoiceAsync()
    {
        var key = await GetSyncChoiceKeyAsync();
        if (key != null) SecureStorage.Remove(key);
    }

    #endregion

    #region Service Locators

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();

    private ISupabaseSyncService? GetSyncService()
        => _syncService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseSyncService>();

    private IAccountService? GetAccountService()
        => _accountService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<IAccountService>();

    private IFeatureAccessService? GetFeatureAccessService()
        => _featureAccessService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<IFeatureAccessService>();

    private ISubscriptionManagementService? GetSubscriptionManagementService()
        => _subscriptionManagementService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISubscriptionManagementService>();

    #endregion

    private void OnBackgroundSyncClicked(object sender, EventArgs e) => HideSpinner();

    private void ShowMainProfileView()
    {
        ProfileMainView.IsVisible = true;
        PlanDetailsView.IsVisible = false;
        SyncQueueDetailsView.IsVisible = false;
    }

    private void OnOpenPlanDetailsTapped(object? sender, TappedEventArgs e)
    {
        ProfileMainView.IsVisible = false;
        PlanDetailsView.IsVisible = true;
    }

    private void OnClosePlanDetailsTapped(object? sender, TappedEventArgs e)
    {
        PlanDetailsView.IsVisible = false;
        ProfileMainView.IsVisible = true;
    }

    private async void OnOpenSyncQueueTapped(object? sender, TappedEventArgs e)
    {
        ProfileMainView.IsVisible = false;
        SyncQueueDetailsView.IsVisible = true;
        await LoadSyncQueueAsync(reset: true);
    }

    private void OnCloseSyncQueueTapped(object? sender, TappedEventArgs e)
    {
        SyncQueueDetailsView.IsVisible = false;
        ProfileMainView.IsVisible = true;
    }

    private async void OnRefreshSyncQueueClicked(object sender, EventArgs e)
    {
        var utcNow = DateTime.UtcNow;
        var elapsed = utcNow - _lastManualSyncQueueRefreshUtc;
        if (elapsed < SyncQueueManualRefreshThrottle)
        {
            var waitTime = Math.Ceiling((SyncQueueManualRefreshThrottle - elapsed).TotalSeconds);
            await DisplayAlert("Odświeżanie", $"Odczekaj {waitTime:0} s przed kolejnym odświeżeniem.", "OK");
            return;
        }

        _lastManualSyncQueueRefreshUtc = utcNow;
        await LoadSyncQueueAsync(reset: true);
    }

    private async void OnLoadMoreSyncQueueClicked(object sender, EventArgs e)
    {
        if (!_syncQueueHasMoreItems)
            return;

        await LoadSyncQueueAsync();
    }

    private async Task LoadSyncQueueAsync(bool reset = false)
    {
        try
        {
            if (!_isLoggedIn)
            {
                SyncQueueItems.Clear();
                _syncQueueOffset = 0;
                _syncQueueHasMoreItems = false;
                SyncQueueLoadMoreButton.IsVisible = false;
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueLoginRequired;
                return;
            }

            var serviceProvider = FoodbookApp.MauiProgram.ServiceProvider;
            if (serviceProvider == null)
            {
                SyncQueueItems.Clear();
                _syncQueueOffset = 0;
                _syncQueueHasMoreItems = false;
                SyncQueueLoadMoreButton.IsVisible = false;
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueServiceUnavailable;
                return;
            }

            var tokenStore = serviceProvider.GetService<IAuthTokenStore>();
            if (tokenStore == null)
            {
                SyncQueueItems.Clear();
                _syncQueueOffset = 0;
                _syncQueueHasMoreItems = false;
                SyncQueueLoadMoreButton.IsVisible = false;
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueAccountUnavailable;
                return;
            }

            var accountId = await tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
            {
                SyncQueueItems.Clear();
                _syncQueueOffset = 0;
                _syncQueueHasMoreItems = false;
                SyncQueueLoadMoreButton.IsVisible = false;
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueNoActiveAccount;
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (reset)
                _syncQueueOffset = 0;

            var queuePage = await db.SyncQueue
                .AsNoTracking()
                .Where(e => e.AccountId == accountId.Value)
                .OrderByDescending(e => e.CreatedUtc)
                .Skip(_syncQueueOffset)
                .Take(SyncQueuePageSize)
                .Select(e => new SyncQueueListEntry
                {
                    EntityType = e.EntityType,
                    EntityId = e.EntityId,
                    OperationType = e.OperationType,
                    Payload = e.Payload,
                    CreatedUtc = e.CreatedUtc,
                    LastError = e.LastError,
                    Status = e.Status
                })
                .ToListAsync();

            var mapped = queuePage.Select(e =>
            {
                var isDownload = IsDownloadEntry(e.Payload);
                var directionEmoji = isDownload ? "⬇️" : "⬆️";
                var statusText = GetStatusText(e.Status);
                var statusColor = GetStatusColor(e.Status);
                var detail = BuildDetailText(e);

                return new SyncQueueDisplayItem
                {
                    Title = $"{directionEmoji} {e.EntityType} · {e.OperationType}",
                    StatusText = statusText,
                    StatusColor = statusColor,
                    Detail = detail,
                    DetailColor = e.Status is SyncEntryStatus.Failed or SyncEntryStatus.Abandoned
                        ? SyncErrorColor
                        : SyncNeutralColor,
                    CreatedText = e.CreatedUtc.ToLocalTime().ToString("dd.MM HH:mm"),
                    IsDownload = isDownload,
                    Status = e.Status
                };
            }).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (reset)
                {
                    SyncQueueItems.Clear();
                    _syncQueueSummary = SyncQueueSummaryState.Empty;
                }

                foreach (var item in mapped)
                {
                    SyncQueueItems.Add(item);
                    _syncQueueSummary = _syncQueueSummary.Add(item);
                }

                _syncQueueOffset += queuePage.Count;
                _syncQueueHasMoreItems = queuePage.Count == SyncQueuePageSize;
                SyncQueueLoadMoreButton.IsVisible = _syncQueueHasMoreItems;
                UpdateSyncQueueSummary(_syncQueueSummary);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] LoadSyncQueueAsync error: {ex.Message}");
            SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueLoadError;
            _syncQueueHasMoreItems = false;
            SyncQueueLoadMoreButton.IsVisible = false;
        }
    }

    private void UpdateSyncQueueSummary(SyncQueueSummaryState summary)
    {
        SyncQueueSummaryLabel.Text = string.Format(
            ProfilePageResources.SyncQueueSummaryFormat,
            summary.Total,
            summary.Uploaded,
            summary.Downloaded,
            summary.Success,
            summary.Failed,
            summary.Pending);
    }

    private static string BuildDetailText(SyncQueueListEntry entry)
    {
        if (entry.Status is SyncEntryStatus.Failed or SyncEntryStatus.Abandoned)
            return string.IsNullOrWhiteSpace(entry.LastError)
                ? ProfilePageResources.SyncQueueDetailErrorNoMessage
                : string.Format(ProfilePageResources.SyncQueueDetailErrorFormat, entry.LastError);

        if (entry.Status == SyncEntryStatus.Completed)
            return string.Format(ProfilePageResources.SyncQueueDetailCompletedFormat, entry.EntityId);

        if (entry.Status == SyncEntryStatus.InProgress)
            return ProfilePageResources.SyncQueueDetailInProgress;

        return ProfilePageResources.SyncQueueDetailPending;
    }

    private static string GetStatusText(SyncEntryStatus status) => status switch
    {
        SyncEntryStatus.Completed => ProfilePageResources.SyncQueueStatusSuccess,
        SyncEntryStatus.Failed => ProfilePageResources.SyncQueueStatusError,
        SyncEntryStatus.Abandoned => ProfilePageResources.SyncQueueStatusError,
        SyncEntryStatus.InProgress => ProfilePageResources.SyncQueueStatusInProgress,
        _ => ProfilePageResources.SyncQueueStatusPending
    };

    private static Color GetStatusColor(SyncEntryStatus status) => status switch
    {
        SyncEntryStatus.Completed => SyncSuccessColor,
        SyncEntryStatus.Failed => SyncErrorColor,
        SyncEntryStatus.Abandoned => SyncErrorColor,
        _ => SyncNeutralColor
    };

    private static bool IsDownloadEntry(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("syncDirection", out var directionElement))
                return false;

            var direction = directionElement.GetString();
            return string.Equals(direction, "download", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class SyncQueueListEntry
    {
        public string EntityType { get; init; } = string.Empty;
        public Guid EntityId { get; init; }
        public SyncOperationType OperationType { get; init; }
        public string? Payload { get; init; }
        public DateTime CreatedUtc { get; init; }
        public string? LastError { get; init; }
        public SyncEntryStatus Status { get; init; }
    }

    public sealed class SyncQueueDisplayItem
    {
        public string Title { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public Color StatusColor { get; set; } = SyncNeutralColor;
        public string Detail { get; set; } = string.Empty;
        public Color DetailColor { get; set; } = SyncNeutralColor;
        public string CreatedText { get; set; } = string.Empty;
        public bool IsDownload { get; set; }
        public SyncEntryStatus Status { get; set; }
    }

    private readonly record struct SyncQueueSummaryState(
        int Total,
        int Uploaded,
        int Downloaded,
        int Success,
        int Failed,
        int Pending)
    {
        public static readonly SyncQueueSummaryState Empty = new(0, 0, 0, 0, 0, 0);

        public SyncQueueSummaryState Add(SyncQueueDisplayItem item)
        {
            var uploaded = Uploaded + (item.IsDownload ? 0 : 1);
            var downloaded = Downloaded + (item.IsDownload ? 1 : 0);
            var success = Success;
            var failed = Failed;
            var pending = Pending;

            switch (item.Status)
            {
                case SyncEntryStatus.Completed:
                    success++;
                    break;
                case SyncEntryStatus.Failed:
                case SyncEntryStatus.Abandoned:
                    failed++;
                    break;
                case SyncEntryStatus.Pending:
                case SyncEntryStatus.InProgress:
                    pending++;
                    break;
            }

            return new SyncQueueSummaryState(Total + 1, uploaded, downloaded, success, failed, pending);
        }
    }
}
