using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Services.Auth;
using System.Text;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;
using Microsoft.Maui.Storage;

namespace Foodbook.Views;

public partial class ProfilePage : ContentPage
{
    private const string CloudSyncChoiceDisabled = "disabled";
    private const string CloudSyncChoiceCloud = "cloud";
    private const string CloudSyncChoiceLocal = "local";

    private ISupabaseAuthService? _supabaseAuth;
    private ISupabaseSyncService? _syncService;
    private IAccountService? _accountService;
    private CancellationTokenSource? _syncStatusRefreshCts;
    private bool _isLoggedIn;
    private bool _suppressCloudSyncToggled = false;

    public ProfilePage()
    {
        InitializeComponent();
        UpdateUiState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartSyncStatusRefresh();
        
        // Always check session state on appearing (handles auto-login and app restarts)
        await CheckAndRestoreSessionAsync();
        
        if (_isLoggedIn)
        {
            await LoadSyncStatusAsync();
        }
    }

    protected override void OnDisappearing()
    {
        StopSyncStatusRefresh();
        base.OnDisappearing();
    }

    private void StartSyncStatusRefresh()
    {
        StopSyncStatusRefresh();
        _syncStatusRefreshCts = new CancellationTokenSource();
        _ = RefreshSyncStatusLoopAsync(_syncStatusRefreshCts.Token);
    }

    private void StopSyncStatusRefresh()
    {
        _syncStatusRefreshCts?.Cancel();
        _syncStatusRefreshCts?.Dispose();
        _syncStatusRefreshCts = null;
    }

    private async Task RefreshSyncStatusLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!_isLoggedIn)
                    continue;

                try
                {
                    await LoadSyncStatusAsync();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Sync status refresh error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ProfilePage] Sync status refresh stopped");
        }
    }

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();

    private ISupabaseSyncService? GetSyncService()
        => _syncService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseSyncService>();

    private IAccountService? GetAccountService()
        => _accountService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<IAccountService>();

    /// <summary>
    /// Checks for existing session and restores UI state accordingly.
    /// This handles both auto-login on app restart and returning to the page.
    /// </summary>
    private async Task CheckAndRestoreSessionAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ProfilePage] === CheckAndRestoreSessionAsync START ===");
            
            var auth = GetSupabaseAuth();
            var accountService = GetAccountService();
            
            // First, check if there's an active session in the auth service (from Supabase client)
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Step 1: Checking auth.CurrentSession...");
            if (auth?.CurrentSession != null && !string.IsNullOrWhiteSpace(auth.CurrentSession.AccessToken))
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] ? Active session found in auth service for user: {auth.CurrentSession.User?.Email}");
                _isLoggedIn = true;
                LoggedInUserLabel.Text = auth.CurrentSession.User?.Email ?? string.Empty;
                UpdateUiState();

                // Ensure we apply saved cloud sync choice (if any) after a restored session
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ProfilePage] Applying saved cloud sync choice after in-memory session restore...");
                    await PromptAndEnableSyncAfterLoginAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Error applying saved cloud sync after session restore: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine("[ProfilePage] === CheckAndRestoreSessionAsync COMPLETE (session found in memory) ===");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Step 2: No active session in memory - checking for stored account...");
            
            // No active session in memory - check if we can restore from stored tokens
            if (accountService != null)
            {
                var activeAccount = await accountService.GetActiveAccountAsync();
                if (activeAccount != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Found active account: {activeAccount.Email}");
                    
                    // Try to restore the session using TryAutoLoginAsync
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Step 3: Calling TryAutoLoginAsync...");
                    var restored = await accountService.TryAutoLoginAsync();
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] TryAutoLoginAsync returned: {restored}");
                    
                    if (restored)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProfilePage] ? Session restored successfully!");
                        _isLoggedIn = true;
                        
                        // Get updated session info
                        var currentAuth = GetSupabaseAuth();
                        var currentSession = currentAuth?.CurrentSession;
                        LoggedInUserLabel.Text = currentSession?.User?.Email ?? activeAccount.Email ?? string.Empty;
                        
                        UpdateUiState();

                        // Apply saved cloud sync choice (if present) after auto-login
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("[ProfilePage] Applying saved cloud sync choice after auto-login...");
                            await PromptAndEnableSyncAfterLoginAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Error applying saved cloud sync after auto-login: {ex.Message}");
                        }

                        System.Diagnostics.Debug.WriteLine("[ProfilePage] === CheckAndRestoreSessionAsync COMPLETE (session restored) ===");
                        return;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProfilePage] ? Session restore failed - tokens may be expired or invalid");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] No active account found in database");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] AccountService is not available");
            }
            
            // No valid session - show login UI
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Step 4: No valid session found - showing login UI");
            if (_isLoggedIn)
            {
                System.Diagnostics.Debug.WriteLine("[ProfilePage] Resetting to logged out state");
                _isLoggedIn = false;
                UpdateUiState();
            }
            System.Diagnostics.Debug.WriteLine("[ProfilePage] === CheckAndRestoreSessionAsync COMPLETE (no session) ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] ? Error checking session: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Exception details: {ex}");
            // On error, assume logged out
            _isLoggedIn = false;
            UpdateUiState();
        }
    }

    private async Task LoadSyncStatusAsync()
    {
        try
        {
            var syncService = GetSyncService();
            if (syncService == null)
                return;

            var isEnabled = await syncService.IsCloudSyncEnabledAsync();
            Foodbook.Models.SyncState? state = null;
            var pendingCount = 0;

            if (isEnabled)
            {
                state = await syncService.GetSyncStateAsync();
                if (state != null)
                {
                    pendingCount = await syncService.GetPendingCountAsync();
                }
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    _suppressCloudSyncToggled = true;
                    CloudSyncCheckBox.IsChecked = isEnabled;
                }
                finally
                {
                    _suppressCloudSyncToggled = false;
                }

                if (isEnabled && state != null)
                {
                    SyncStatusLabel.Text = $"Status: {state.Status}, Pending: {pendingCount}";
                    SyncStatusLabel.IsVisible = true;
                }
                else
                {
                    SyncStatusLabel.IsVisible = false;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to load sync status: {ex.Message}");
        }
    }

    private void UpdateUiState()
    {
        LoginPanel.IsVisible = !_isLoggedIn;
        LoggedInPanel.IsVisible = _isLoggedIn;

        if (!_isLoggedIn)
        {
            LoggedInUserLabel.Text = string.Empty;
            SyncStatusLabel.IsVisible = false;
        }
    }

    private string GetLocalizedText(string resource, string key, string defaultText)
    {
        try
        {
            var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService(typeof(ILocalizationService)) as ILocalizationService;
            return loc?.GetString(resource, key) ?? defaultText;
        }
        catch { return defaultText; }
    }

    private string GetLocalizedTextSafe(string resource, string key, string defaultText)
    {
        var text = GetLocalizedText(resource, key, defaultText);
        return string.IsNullOrWhiteSpace(text) ? defaultText : text;
    }

    private async Task<Guid?> GetCurrentSupabaseUserIdAsync()
    {
        try
        {
            var serviceProvider = FoodbookApp.MauiProgram.ServiceProvider;
            if (serviceProvider == null) return null;

            var tokenStore = serviceProvider.GetService<IAuthTokenStore>();
            if (tokenStore == null) return null;

            var accountId = await tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue) return null;

            var db = serviceProvider.GetService<Foodbook.Data.AppDbContext>();
            if (db == null) return null;

            var acc = await db.AuthAccounts.FindAsync(accountId.Value);
            if (acc == null || string.IsNullOrWhiteSpace(acc.SupabaseUserId)) return null;

            return Guid.TryParse(acc.SupabaseUserId, out var userGuid) ? userGuid : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetCloudSyncStorageKeyAsync()
    {
        try
        {
            var tokenStore = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IAuthTokenStore>();
            if (tokenStore == null) return null;
            var activeAccountId = await tokenStore.GetActiveAccountIdAsync();
            if (!activeAccountId.HasValue) return null;
            return $"cloudsync.choice:{activeAccountId.Value:N}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] GetCloudSyncStorageKeyAsync failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> GetSavedCloudSyncChoiceAsync()
    {
        var storageKey = await GetCloudSyncStorageKeyAsync();
        if (string.IsNullOrWhiteSpace(storageKey))
            return null;

        try
        {
            var saved = await SecureStorage.GetAsync(storageKey);
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Retrieved saved cloud sync value: '{saved}' for key {storageKey}");
            return string.IsNullOrWhiteSpace(saved) ? null : saved;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to read cloud sync choice: {ex.Message}");
            return null;
        }
    }

    private async Task SaveCloudSyncChoiceAsync(Foodbook.Models.SyncPriority priority)
    {
        var storageKey = await GetCloudSyncStorageKeyAsync();
        if (string.IsNullOrWhiteSpace(storageKey))
            return;

        try
        {
            var value = priority == Foodbook.Models.SyncPriority.Cloud ? CloudSyncChoiceCloud : CloudSyncChoiceLocal;
            await SecureStorage.SetAsync(storageKey, value);
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Saved cloud sync choice to SecureStorage: {storageKey}={value}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to save cloud sync choice: {ex.Message}");
        }
    }

    private async Task SaveCloudSyncDisabledChoiceAsync()
    {
        var storageKey = await GetCloudSyncStorageKeyAsync();
        if (string.IsNullOrWhiteSpace(storageKey))
            return;

        try
        {
            await SecureStorage.SetAsync(storageKey, CloudSyncChoiceDisabled);
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Saved cloud sync disabled flag to SecureStorage: {storageKey}={CloudSyncChoiceDisabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to save cloud sync disabled flag: {ex.Message}");
        }
    }

    private void SetCloudSyncCheckBox(bool isChecked)
    {
        try
        {
            _suppressCloudSyncToggled = true;
            CloudSyncCheckBox.IsChecked = isChecked;
        }
        finally
        {
            _suppressCloudSyncToggled = false;
        }
    }

    private async Task ApplySyncChoiceAsync(ISupabaseSyncService syncService, string savedChoice)
    {
        switch (savedChoice)
        {
            case CloudSyncChoiceCloud:
                SyncStatusLabel.IsVisible = true;
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");
                await syncService.EnableCloudSyncAsync(Foodbook.Models.SyncPriority.Cloud);
                SetCloudSyncCheckBox(true);
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");
                break;

            case CloudSyncChoiceLocal:
                SyncStatusLabel.IsVisible = true;
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");
                await syncService.EnableCloudSyncAsync(Foodbook.Models.SyncPriority.Local);
                SetCloudSyncCheckBox(true);
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");
                break;

            case CloudSyncChoiceDisabled:
                await syncService.DisableCloudSyncAsync();
                SetCloudSyncCheckBox(false);
                SyncStatusLabel.IsVisible = false;
                System.Diagnostics.Debug.WriteLine("[ProfilePage] Cloud sync explicitly disabled for this account (saved)");
                break;
        }
    }

    private async Task<Foodbook.Models.SyncPriority?> PromptForSyncPriorityAsync()
    {
        var title = GetLocalizedTextSafe("ProfilePageResources", "CloudSyncHeader", "Cloud sync");
        var cancel = GetLocalizedTextSafe("ButtonResources", "Cancel", "Cancel");
        var cloud = GetLocalizedTextSafe("ProfilePageResources", "CloudFirstOption", "Cloud first (download from cloud first)");
        var local = GetLocalizedTextSafe("ProfilePageResources", "LocalFirstOption", "Local first (upload local data first)");

        var action = await MainThread.InvokeOnMainThreadAsync(() => DisplayActionSheet(title, cancel, null, cloud, local));

        if (action == cancel || action == null)
            return null;

        return action == cloud ? Foodbook.Models.SyncPriority.Cloud : Foodbook.Models.SyncPriority.Local;
    }

    private async Task RunPostEnableSyncPreferencesAsync(ISupabaseSyncService syncService, Foodbook.Models.SyncPriority priority)
    {
        var userId = await GetCurrentSupabaseUserIdAsync();
        if (!userId.HasValue)
            return;

        try
        {
            if (priority == Foodbook.Models.SyncPriority.Cloud)
            {
                await syncService.LoadUserPreferencesFromCloudAsync(userId.Value);
            }
            else
            {
                await syncService.SaveUserPreferencesToCloudAsync(userId.Value);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Post-login preferences sync failed (non-fatal): {ex.Message}");
        }
    }

    private async Task<bool> PromptAndEnableSyncAfterLoginAsync()
    {
        var syncService = GetSyncService();
        if (syncService == null)
            return false;

        var savedChoice = await GetSavedCloudSyncChoiceAsync();
        if (!string.IsNullOrWhiteSpace(savedChoice))
        {
            await ApplySyncChoiceAsync(syncService, savedChoice);
            return true;
        }

        var isEnabled = await syncService.IsCloudSyncEnabledAsync();
        if (isEnabled)
        {
            System.Diagnostics.Debug.WriteLine("[ProfilePage] Cloud sync is already enabled for current account - skipping prompt");
            SetCloudSyncCheckBox(true);
            return true;
        }

        var priority = await PromptForSyncPriorityAsync();
        if (!priority.HasValue)
            return false;

        SyncStatusLabel.IsVisible = true;
        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");

        await syncService.EnableCloudSyncAsync(priority.Value);
        await SaveCloudSyncChoiceAsync(priority.Value);
        await RunPostEnableSyncPreferencesAsync(syncService, priority.Value);

        SetCloudSyncCheckBox(true);
        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");
        return true;
    }

    private async void OnProfileFetchJwtClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ProfilePage] Login button clicked");

        try
        {
            var auth = GetSupabaseAuth();
            if (auth == null)
            {
                await DisplayAlert(
                    GetLocalizedText("ProfilePageResources", "ServiceMissingTitle", "Service unavailable"),
                    GetLocalizedText("ProfilePageResources", "ServiceMissingMessage", "Authentication service not found."),
                    GetLocalizedText("ProfilePageResources", "OK", "OK")
                );
                return;
            }

            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email))
            {
                await DisplayAlert(
                    GetLocalizedText("ProfilePageResources", "LoginTitle", "Login"),
                    GetLocalizedText("ProfilePageResources", "EmailRequired", "Email cannot be empty."),
                    GetLocalizedText("ProfilePageResources", "OK", "OK")
                );
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert(
                    GetLocalizedText("ProfilePageResources", "LoginTitle", "Login"),
                    GetLocalizedText("ProfilePageResources", "PasswordRequired", "Password cannot be empty."),
                    GetLocalizedText("ProfilePageResources", "OK", "OK")
                );
                return;
            }

            StatusLabel.Text = GetLocalizedText("ProfilePageResources", "LoggingIn", "Signing in...");

            var session = await auth.SignInAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                StatusLabel.Text = string.Empty;
                await DisplayAlert(
                    GetLocalizedText("ProfilePageResources", "LoginErrorTitle", "Login Error"),
                    GetLocalizedText("ProfilePageResources", "LoginErrorMessage", "Login failed. Please check your credentials."),
                    GetLocalizedText("ProfilePageResources", "OK", "OK")
                );
                return;
            }

            _isLoggedIn = true;
            LoggedInUserLabel.Text = session.User?.Email ?? email;
            StatusLabel.Text = string.Empty;

            // Clear sensitive input after success
            PasswordEntry.Text = string.Empty;

            UpdateUiState();

            await LoadSyncStatusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Exception: {ex}");
            StatusLabel.Text = string.Empty;

            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "GenericErrorTitle", "Error"),
                msg,
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        try
        {
            var popup = FoodbookApp.MauiProgram.ServiceProvider?.GetService<RegisterPopup>() ?? new RegisterPopup();

            var hostPage = Application.Current?.Windows.FirstOrDefault()?.Page
                           ?? Application.Current?.MainPage
                           ?? this;

            var showTask = hostPage.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;

            await Task.WhenAny(showTask, resultTask);

            var registered = resultTask.IsCompleted && await resultTask;
            if (!registered)
                return;

            var auth = GetSupabaseAuth();
            var session = auth?.CurrentSession;
            if (session != null && !string.IsNullOrWhiteSpace(session.AccessToken))
            {
                _isLoggedIn = true;
                LoggedInUserLabel.Text = session.User?.Email ?? string.Empty;
                UpdateUiState();
                await LoadSyncStatusAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Register popup error: {ex}");
            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "GenericErrorTitle", "Error"),
                msg,
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
    }

    private async void OnCloudSyncToggled(object sender, CheckedChangedEventArgs e)
    {
        if (_suppressCloudSyncToggled)
        {
            System.Diagnostics.Debug.WriteLine("[ProfilePage] OnCloudSyncToggled suppressed (programmatic change)");
            return;
        }
        try
        {
            var syncService = GetSyncService();
            if (syncService == null)
            {
                await DisplayAlert(
                    GetLocalizedText("ProfilePageResources", "SyncErrorTitle", "Sync Error"),
                    GetLocalizedText("ProfilePageResources", "SyncServiceUnavailable", "Sync service not available"),
                    GetLocalizedText("ProfilePageResources", "OK", "OK")
                );
                CloudSyncCheckBox.IsChecked = !e.Value;
                return;
            }

            SyncStatusLabel.IsVisible = true;

            if (e.Value)
            {
                var priority = await PromptForSyncPriorityAsync();

                if (!priority.HasValue)
                {
                    CloudSyncCheckBox.IsChecked = false;
                    SyncStatusLabel.IsVisible = false;
                    return;
                }

                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");
                ShowSpinner(GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync..."));

                await syncService.EnableCloudSyncAsync(priority.Value);
                await SaveCloudSyncChoiceAsync(priority.Value);

                HideSpinner();
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");
            }
            else
            {
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "DisablingSync", "Disabling sync...");
                ShowSpinner(GetLocalizedText("ProfilePageResources", "DisablingSync", "Disabling sync..."));

                await syncService.DisableCloudSyncAsync();

                HideSpinner();
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncDisabled", "Cloud sync disabled");
                await SaveCloudSyncDisabledChoiceAsync();
            }

            await Task.Delay(2000);
            await LoadSyncStatusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Sync toggle error: {ex.Message}");
            HideSpinner();
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "SyncErrorTitle", "Sync Error"),
                ex.Message,
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
            CloudSyncCheckBox.IsChecked = !e.Value;
            SyncStatusLabel.IsVisible = false;
        }
    }

    private async void OnForceSyncClicked(object sender, EventArgs e)
    {
        var sync = GetSyncService();
        if (sync == null)
        {
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "SyncErrorTitle", "Sync Error"),
                GetLocalizedText("ProfilePageResources", "SyncServiceUnavailable", "Sync service not available"),
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
            return;
        }

        SyncStatusLabel.IsVisible = true;
        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "ForcingSync", "Forcing sync of ALL items...");
        ShowSpinner(GetLocalizedText("ProfilePageResources", "SyncOverlaySyncing", "Syncing data..."));

        try
        {
            // Run deduplication first so we can report stats
            int dedupRemoved = 0;
            int dedupSkipped = 0;

            var deduplicationService = FoodbookApp.MauiProgram.ServiceProvider?
                .GetService(typeof(IDeduplicationService)) as IDeduplicationService;

            if (deduplicationService != null)
            {
                try
                {
                    ShowSpinner(GetLocalizedText("ProfilePageResources", "SyncOverlayDeduplicating", "Removing duplicates..."));

                    if (!deduplicationService.IsCachePopulated)
                    {
                        ShowSpinner(GetLocalizedText("ProfilePageResources", "SyncOverlayFetching", "Fetching cloud data..."));
                        await deduplicationService.FetchCloudDataAsync();
                    }

                    using var scope = FoodbookApp.MauiProgram.ServiceProvider!.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<Foodbook.Data.AppDbContext>();

                    // Get pending count BEFORE dedup so we can calculate skipped
                    var pendingBefore = await sync.GetPendingCountAsync();
                    dedupRemoved = await deduplicationService.DeduplicateSyncQueueAsync();
                    var pendingAfter = await sync.GetPendingCountAsync();
                    dedupSkipped = Math.Max(0, pendingBefore - pendingAfter - dedupRemoved);
                }
                catch (Exception dedupEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] ForceSync dedup error (non-fatal): {dedupEx.Message}");
                }
            }

            ShowSpinner(GetLocalizedText("ProfilePageResources", "SyncOverlaySyncing", "Syncing data..."));
            var result = await sync.ForceSyncAllAsync();

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(
                GetLocalizedText("ProfilePageResources", "ForceSyncSynced", "Synced: {0}"),
                result.ItemsProcessed));

            if (result.ItemsRemaining > 0)
                sb.AppendLine(string.Format(
                    GetLocalizedText("ProfilePageResources", "ForceSyncRemaining", "Remaining: {0}"),
                    result.ItemsRemaining));

            if (result.ItemsFailed > 0)
                sb.AppendLine(string.Format(
                    GetLocalizedText("ProfilePageResources", "ForceSyncFailed", "Failed: {0}"),
                    result.ItemsFailed));

            if (dedupRemoved > 0)
                sb.AppendLine(string.Format(
                    GetLocalizedText("ProfilePageResources", "ForceSyncDuplicatesRemoved", "Cloud duplicates removed: {0}"),
                    dedupRemoved));

            if (dedupSkipped > 0)
                sb.AppendLine(string.Format(
                    GetLocalizedText("ProfilePageResources", "ForceSyncSkipped", "Skipped (already in cloud): {0}"),
                    dedupSkipped));

            sb.AppendLine(string.Format(
                GetLocalizedText("ProfilePageResources", "ForceSyncDuration", "Duration: {0}s"),
                result.Duration.TotalSeconds.ToString("F1")));

            if (!string.IsNullOrEmpty(result.ErrorMessage))
                sb.AppendLine(string.Format(
                    GetLocalizedText("ProfilePageResources", "ForceSyncError", "Error: {0}"),
                    result.ErrorMessage));

            HideSpinner();
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "ForceSyncCompletedTitle", "Force sync completed"),
                sb.ToString().TrimEnd(),
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] ForceSync error: {ex}");
            HideSpinner();
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "ForceSyncErrorTitle", "Force sync error"),
                ex.Message,
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
        finally
        {
            HideSpinner();
            await LoadSyncStatusAsync();
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ProfilePage] OnLogoutClicked: Starting logout process");
            
            var accountService = GetAccountService();
            if (accountService != null)
            {
                // ? CRITICAL: Clear auto-login flag when user explicitly logs out
                System.Diagnostics.Debug.WriteLine("[ProfilePage] Calling SignOutAsync with clearAutoLogin=true");
                await accountService.SignOutAsync(clearAutoLogin: true);
            }
            else
            {
                // Fallback to direct auth service if account service unavailable
                var auth = GetSupabaseAuth();
                if (auth != null)
                {
                    await auth.SignOutAsync();
                }
            }

            var syncService = GetSyncService();
            if (syncService != null)
            {
                await syncService.DisableCloudSyncAsync();
            }

            _isLoggedIn = false;
            UpdateUiState();
            
            EmailEntry.Text = string.Empty;
            PasswordEntry.Text = string.Empty;
            StatusLabel.Text = string.Empty;
            SyncStatusLabel.IsVisible = false;
            
            System.Diagnostics.Debug.WriteLine("[ProfilePage] OnLogoutClicked: Logout completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Logout error: {ex.Message}");
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "LogoutErrorTitle", "Logout Error"),
                ex.Message,
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
    }

    private void OnBackgroundSyncClicked(object sender, EventArgs e)
    {
        HideSpinner();
    }

    #region Spinner helpers

    private void ShowSpinner(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncOverlayLabel.Text = message;
            SyncActivityIndicator.IsRunning = true;
            SyncingOverlay.IsVisible = true;
            ForceSyncButton.IsEnabled = false;
            CloudSyncCheckBox.IsEnabled = false;
        });
    }

    private void HideSpinner()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SyncingOverlay.IsVisible = false;
            SyncActivityIndicator.IsRunning = false;
            ForceSyncButton.IsEnabled = true;
            CloudSyncCheckBox.IsEnabled = true;
        });
    }

    #endregion
}
