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
    private ISupabaseAuthService? _supabaseAuth;
    private ISupabaseSyncService? _syncService;
    private IAccountService? _accountService;
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
        
        // Always check session state on appearing (handles auto-login and app restarts)
        await CheckAndRestoreSessionAsync();
        
        if (_isLoggedIn)
        {
            await LoadSyncStatusAsync();
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
            try
            {
                _suppressCloudSyncToggled = true;
                CloudSyncCheckBox.IsChecked = isEnabled;
            }
            finally
            {
                _suppressCloudSyncToggled = false;
            }

            if (isEnabled)
            {
                var state = await syncService.GetSyncStateAsync();
                if (state != null)
                {
                    var pendingCount = await syncService.GetPendingCountAsync();
                    SyncStatusLabel.Text = $"Status: {state.Status}, Pending: {pendingCount}";
                    SyncStatusLabel.IsVisible = true;
                }
            }
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

    private async Task<bool> PromptAndEnableSyncAfterLoginAsync()
    {
        var syncService = GetSyncService();
        if (syncService == null)
            return false;

        var storageKey = await GetCloudSyncStorageKeyAsync();

        // If we have a saved choice for this account, apply it WITHOUT prompting
        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            try
            {
                var saved = await SecureStorage.GetAsync(storageKey);
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] Retrieved saved cloud sync value: '{saved}' for key {storageKey}");
                if (!string.IsNullOrWhiteSpace(saved))
                {
                    if (saved == "cloud")
                    {
                        SyncStatusLabel.IsVisible = true;
                        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");
                        await syncService.EnableCloudSyncAsync(Foodbook.Models.SyncPriority.Cloud);
                        try { _suppressCloudSyncToggled = true; CloudSyncCheckBox.IsChecked = true; } finally { _suppressCloudSyncToggled = false; }
                        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");
                        return true;
                    }
                    else if (saved == "local")
                    {
                        SyncStatusLabel.IsVisible = true;
                        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");
                        await syncService.EnableCloudSyncAsync(Foodbook.Models.SyncPriority.Local);
                        try { _suppressCloudSyncToggled = true; CloudSyncCheckBox.IsChecked = true; } finally { _suppressCloudSyncToggled = false; }
                        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");
                        return true;
                    }
                    else if (saved == "disabled")
                    {
                        await syncService.DisableCloudSyncAsync();
                        try { _suppressCloudSyncToggled = true; CloudSyncCheckBox.IsChecked = false; } finally { _suppressCloudSyncToggled = false; }
                        SyncStatusLabel.IsVisible = false;
                        System.Diagnostics.Debug.WriteLine("[ProfilePage] Cloud sync explicitly disabled for this account (saved)");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] SecureStorage read failed in PromptAndEnableSyncAfterLoginAsync: {ex.Message}");
                // fall through to prompt
            }
        }

        // No saved choice -> show the prompt on UI thread
        var title = GetLocalizedTextSafe("ProfilePageResources", "CloudSyncHeader", "Cloud sync");
        var cancel = GetLocalizedTextSafe("ButtonResources", "Cancel", "Cancel");
        var cloud = GetLocalizedTextSafe("ProfilePageResources", "CloudFirstOption", "Cloud first (download from cloud first)");
        var local = GetLocalizedTextSafe("ProfilePageResources", "LocalFirstOption", "Local first (upload local data first)");

        var action = await MainThread.InvokeOnMainThreadAsync(() => DisplayActionSheet(title, cancel, null, cloud, local));

        if (action == cancel || action == null)
            return false;

        var priority = action == cloud ? Foodbook.Models.SyncPriority.Cloud : Foodbook.Models.SyncPriority.Local;

        SyncStatusLabel.IsVisible = true;
        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");

        await syncService.EnableCloudSyncAsync(priority);

        // Persist user choice so we won't prompt next time
        if (!string.IsNullOrWhiteSpace(storageKey))
        {
            try
            {
                var val = priority == Foodbook.Models.SyncPriority.Cloud ? "cloud" : "local";
                await SecureStorage.SetAsync(storageKey, val);
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] Saved cloud sync choice to SecureStorage: {storageKey}={val}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to save cloud sync choice: {ex.Message}");
            }
        }

        // Only AFTER enabling sync, handle cloud preferences/theme depending on priority
        var userId = await GetCurrentSupabaseUserIdAsync();
        if (userId.HasValue)
        {
            try
            {
                if (priority == Foodbook.Models.SyncPriority.Cloud)
                {
                    // Cloud-first: pull prefs/theme from cloud and apply locally
                    await syncService.LoadUserPreferencesFromCloudAsync(userId.Value);
                }
                else
                {
                    // Local-first: push current local prefs/theme to cloud
                    await syncService.SaveUserPreferencesToCloudAsync(userId.Value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] Post-login preferences sync failed (non-fatal): {ex.Message}");
            }
        }

        try { _suppressCloudSyncToggled = true; CloudSyncCheckBox.IsChecked = true; } finally { _suppressCloudSyncToggled = false; }
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

            // Build storage key scoped to active account
            string storageKey = "cloudsync.choice";
            try
            {
                var tokenStore = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IAuthTokenStore>();
                var activeAccountId = tokenStore == null ? (Guid?)null : await tokenStore.GetActiveAccountIdAsync();
                if (activeAccountId.HasValue)
                    storageKey = $"cloudsync.choice:{activeAccountId.Value:N}";
            }
            catch { }

            if (e.Value)
            {
                var title = GetLocalizedTextSafe("ProfilePageResources", "CloudSyncHeader", "Cloud sync");
                var cancel = GetLocalizedTextSafe("ButtonResources", "Cancel", "Cancel");
                var cloud = GetLocalizedTextSafe("ProfilePageResources", "CloudFirstOption", "Cloud first (download from cloud first)");
                var local = GetLocalizedTextSafe("ProfilePageResources", "LocalFirstOption", "Local first (upload local data first)");

                var action = await DisplayActionSheet(title, cancel, null, cloud, local);

                if (action == cancel || action == null)
                {
                    CloudSyncCheckBox.IsChecked = false;
                    SyncStatusLabel.IsVisible = false;
                    return;
                }

                var priority = action == cloud ? Foodbook.Models.SyncPriority.Cloud : Foodbook.Models.SyncPriority.Local;

                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");
                await syncService.EnableCloudSyncAsync(priority);
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncEnabled", "Cloud sync enabled. Syncing data...");

                // Persist user choice so we won't prompt next time
                try
                {
                    var val = priority == Foodbook.Models.SyncPriority.Cloud ? "cloud" : "local";
                    await SecureStorage.SetAsync(storageKey, val);
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Saved cloud sync choice to SecureStorage: {storageKey}={val}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to save cloud sync choice: {ex.Message}");
                }
            }
            else
            {
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "DisablingSync", "Disabling sync...");
                await syncService.DisableCloudSyncAsync();
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncDisabled", "Cloud sync disabled");

                // Persist disabled state so prompt can be shown again if desired
                try
                {
                    await SecureStorage.SetAsync(storageKey, "disabled");
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Saved cloud sync disabled flag to SecureStorage: {storageKey}=disabled");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProfilePage] Failed to save cloud sync disabled flag: {ex.Message}");
                }
            }

            await Task.Delay(2000);
            await LoadSyncStatusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Sync toggle error: {ex.Message}");
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

        try
        {
            // Use ForceSyncAllAsync to sync all items without delay
            var result = await sync.ForceSyncAllAsync();
            var sb = new StringBuilder();
            sb.AppendLine($"Success: {result.Success}");
            sb.AppendLine($"Processed: {result.ItemsProcessed}");
            sb.AppendLine($"Remaining: {result.ItemsRemaining}");
            sb.AppendLine($"Duration: {result.Duration.TotalSeconds:F1}s");
            if (!string.IsNullOrEmpty(result.ErrorMessage)) 
                sb.AppendLine($"Error: {result.ErrorMessage}");

            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "ForceSyncCompletedTitle", "Force sync completed"),
                sb.ToString(),
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] ForceSync error: {ex}");
            await DisplayAlert(
                GetLocalizedText("ProfilePageResources", "ForceSyncErrorTitle", "Force sync error"),
                ex.Message,
                GetLocalizedText("ProfilePageResources", "OK", "OK")
            );
        }
        finally
        {
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
}
