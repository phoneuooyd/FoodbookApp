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

    private async void OnProfileFetchJwtClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ProfilePage] Login button clicked");

        try
        {
            var auth = GetSupabaseAuth();
            if (auth == null)
            {
                await DisplayAlert("Profil", "Brak ISupabaseAuthService w DI.", "OK");
                return;
            }

            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email))
            {
                await DisplayAlert("Logowanie", "Email nie moze byc pusty.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Logowanie", "Haslo nie moze byc puste.", "OK");
                return;
            }

            StatusLabel.Text = "Trwa logowanie...";

            var session = await auth.SignInAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                StatusLabel.Text = string.Empty;
                await DisplayAlert("Blad logowania", "Nie udalo sie zalogowac. Sprawdz dane logowania.", "OK");
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
            await DisplayAlert("Blad", msg, "OK");
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
            await DisplayAlert("Blad", msg, "OK");
        }
    }

    private async void OnCloudSyncToggled(object sender, CheckedChangedEventArgs e)
    {
        try
        {
            var syncService = GetSyncService();
            if (syncService == null)
            {
                await DisplayAlert("Sync Error", "Sync service not available", "OK");
                CloudSyncCheckBox.IsChecked = !e.Value;
                return;
            }

            SyncStatusLabel.IsVisible = true;
            SyncStatusLabel.Text = e.Value ? "Enabling sync..." : "Disabling sync...";

            if (e.Value)
            {
                await syncService.EnableCloudSyncAsync();
                SyncStatusLabel.Text = "Cloud sync enabled. Syncing data...";
            }
            else
            {
                await syncService.DisableCloudSyncAsync();
                SyncStatusLabel.Text = "Cloud sync disabled";
            }

            await Task.Delay(2000);
            await LoadSyncStatusAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Sync toggle error: {ex.Message}");
            await DisplayAlert("Sync Error", ex.Message, "OK");
            CloudSyncCheckBox.IsChecked = !e.Value;
            SyncStatusLabel.IsVisible = false;
        }
    }

    private async void OnForceSyncClicked(object sender, EventArgs e)
    {
        var sync = GetSyncService();
        if (sync == null)
        {
            await DisplayAlert("Sync Error", "Sync service not available", "OK");
            return;
        }

        SyncStatusLabel.IsVisible = true;
        SyncStatusLabel.Text = "Forcing sync of ALL items...";

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

            await DisplayAlert("Force sync completed", sb.ToString(), "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] ForceSync error: {ex}");
            await DisplayAlert("Force sync error", ex.Message, "OK");
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
            await DisplayAlert("Logout Error", ex.Message, "OK");
        }
    }
}
