using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Data;
using Foodbook.Models;
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
    private CancellationTokenSource? _syncRefreshCts;
    private bool _isLoggedIn;
    private bool _suppressToggle;
    private string _currentUserEmail = string.Empty;

    private const string SyncChoiceKeyPrefix = "cloudsync.enabled:";
    private static readonly Color SyncSuccessColor = Color.FromArgb("#2E7D32");
    private static readonly Color SyncErrorColor = Color.FromArgb("#C62828");
    private static readonly Color SyncNeutralColor = Color.FromArgb("#6B7280");

    public ObservableCollection<SyncQueueDisplayItem> SyncQueueItems { get; } = new();

    public ProfilePage()
    {
        InitializeComponent();
        UpdateUiState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartSyncStatusRefresh();
        await CheckAndRestoreSessionAsync();
        if (_isLoggedIn)
            await LoadSyncStatusAsync();
    }

    protected override void OnDisappearing()
    {
        StopSyncStatusRefresh();
        base.OnDisappearing();
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
            _currentUserEmail = string.Empty;
            EmailEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;
            StatusLabel.Text = string.Empty;
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

        HeroUserEmailLabel.Text = _isLoggedIn ? _currentUserEmail : string.Empty;
    }

    private void ShowSpinner(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncOverlayLabel.Text = message;
            SyncActivityIndicator.IsRunning = true;
            SyncingOverlay.IsVisible = true;
            CloudSyncSwitch.IsEnabled = false;
        });
    }

    private void HideSpinner()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncingOverlay.IsVisible = false;
            SyncActivityIndicator.IsRunning = false;
            CloudSyncSwitch.IsEnabled = true;
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

    #endregion

    private void OnBackgroundSyncClicked(object sender, EventArgs e) => HideSpinner();

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
        await LoadSyncQueueAsync();
    }

    private void OnCloseSyncQueueTapped(object? sender, TappedEventArgs e)
    {
        SyncQueueDetailsView.IsVisible = false;
        ProfileMainView.IsVisible = true;
    }

    private async void OnRefreshSyncQueueClicked(object sender, EventArgs e)
    {
        await LoadSyncQueueAsync();
    }

    private async Task LoadSyncQueueAsync()
    {
        try
        {
            if (!_isLoggedIn)
            {
                SyncQueueItems.Clear();
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueLoginRequired;
                return;
            }

            var serviceProvider = FoodbookApp.MauiProgram.ServiceProvider;
            if (serviceProvider == null)
            {
                SyncQueueItems.Clear();
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueServiceUnavailable;
                return;
            }

            var tokenStore = serviceProvider.GetService<IAuthTokenStore>();
            if (tokenStore == null)
            {
                SyncQueueItems.Clear();
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueAccountUnavailable;
                return;
            }

            var accountId = await tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
            {
                SyncQueueItems.Clear();
                SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueNoActiveAccount;
                return;
            }

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var queue = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value)
                .OrderByDescending(e => e.CreatedUtc)
                .Take(300)
                .ToListAsync();

            var mapped = queue.Select(e =>
            {
                var isDownload = IsDownloadEntry(e);
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
                    CreatedText = e.CreatedUtc.ToLocalTime().ToString("dd.MM HH:mm")
                };
            }).ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SyncQueueItems.Clear();
                foreach (var item in mapped)
                    SyncQueueItems.Add(item);

                var success = queue.Count(e => e.Status == SyncEntryStatus.Completed);
                var failed = queue.Count(e => e.Status is SyncEntryStatus.Failed or SyncEntryStatus.Abandoned);
                var pending = queue.Count(e => e.Status is SyncEntryStatus.Pending or SyncEntryStatus.InProgress);
                var downloaded = queue.Count(IsDownloadEntry);
                var uploaded = queue.Count - downloaded;

                SyncQueueSummaryLabel.Text = string.Format(
                    ProfilePageResources.SyncQueueSummaryFormat,
                    queue.Count,
                    uploaded,
                    downloaded,
                    success,
                    failed,
                    pending);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProfilePage] LoadSyncQueueAsync error: {ex.Message}");
            SyncQueueSummaryLabel.Text = ProfilePageResources.SyncQueueLoadError;
        }
    }

    private static string BuildDetailText(SyncQueueEntry entry)
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

    private static bool IsDownloadEntry(SyncQueueEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Payload))
            return false;

        return entry.Payload.Contains("\"syncDirection\":\"download\"", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class SyncQueueDisplayItem
    {
        public string Title { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
        public Color StatusColor { get; set; } = SyncNeutralColor;
        public string Detail { get; set; } = string.Empty;
        public Color DetailColor { get; set; } = SyncNeutralColor;
        public string CreatedText { get; set; } = string.Empty;
    }
}
