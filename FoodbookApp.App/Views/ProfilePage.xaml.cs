using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Services.Auth;
using System.Text;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;

namespace Foodbook.Views;

public partial class ProfilePage : ContentPage
{
    private ISupabaseAuthService? _supabaseAuth;
    private ISupabaseSyncService? _syncService;
    private bool _isLoggedIn;

    public ProfilePage()
    {
        InitializeComponent();
        UpdateUiState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (_isLoggedIn)
        {
            await LoadSyncStatusAsync();
        }
    }

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();

    private ISupabaseSyncService? GetSyncService()
        => _syncService ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseSyncService>();

    private async Task LoadSyncStatusAsync()
    {
        try
        {
            var syncService = GetSyncService();
            if (syncService == null)
                return;

            var isEnabled = await syncService.IsCloudSyncEnabledAsync();
            CloudSyncCheckBox.IsChecked = isEnabled;

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

    private async Task<bool> PromptAndEnableSyncAfterLoginAsync()
    {
        var syncService = GetSyncService();
        if (syncService == null)
            return false;

        var title = GetLocalizedTextSafe("ProfilePageResources", "CloudSyncHeader", "Cloud sync");
        var cancel = GetLocalizedTextSafe("ButtonResources", "Cancel", "Cancel");
        var cloud = GetLocalizedTextSafe("ProfilePageResources", "CloudFirstOption", "Cloud first (download from cloud first)");
        var local = GetLocalizedTextSafe("ProfilePageResources", "LocalFirstOption", "Local first (upload local data first)");

        var action = await DisplayActionSheet(title, cancel, null, cloud, local);

        if (action == cancel || action == null)
            return false;

        var priority = action == cloud ? Foodbook.Models.SyncPriority.Cloud : Foodbook.Models.SyncPriority.Local;

        SyncStatusLabel.IsVisible = true;
        SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "EnablingSync", "Enabling sync...");

        await syncService.EnableCloudSyncAsync(priority);

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

        CloudSyncCheckBox.IsChecked = true;
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
            }
            else
            {
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "DisablingSync", "Disabling sync...");
                await syncService.DisableCloudSyncAsync();
                SyncStatusLabel.Text = GetLocalizedText("ProfilePageResources", "SyncDisabled", "Cloud sync disabled");
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
            var auth = GetSupabaseAuth();
            if (auth != null)
            {
                await auth.SignOutAsync();
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
