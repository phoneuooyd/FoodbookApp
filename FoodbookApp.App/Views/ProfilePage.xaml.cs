using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Services.Auth;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;

namespace Foodbook.Views;

public partial class ProfilePage : ContentPage
{
    private ISupabaseAuthService? _supabaseAuth;
    private bool _isLoggedIn;

    public ProfilePage()
    {
        InitializeComponent();
        UpdateUiState();
    }

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();

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
                await DisplayAlert("Logowanie", "Email nie mo¿e byæ pusty.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Logowanie", "Has³o nie mo¿e byæ puste.", "OK");
                return;
            }

            StatusLabel.Text = "Trwa logowanie...";

            var session = await auth.SignInAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                StatusLabel.Text = string.Empty;
                await DisplayAlert("B³¹d logowania", "Nie uda³o siê zalogowaæ. SprawdŸ dane logowania.", "OK");
                return;
            }

            _isLoggedIn = true;
            LoggedInUserLabel.Text = session.User?.Email ?? email;
            StatusLabel.Text = string.Empty;

            // Clear sensitive input after success
            PasswordEntry.Text = string.Empty;

            UpdateUiState();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Exception: {ex}");
            StatusLabel.Text = string.Empty;

            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await DisplayAlert("B³¹d", msg, "OK");
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
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Register popup error: {ex}");
            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await DisplayAlert("B³¹d", msg, "OK");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Wyloguj", "Funkcja wylogowania bêdzie dodana póŸniej.", "OK");
    }
}
