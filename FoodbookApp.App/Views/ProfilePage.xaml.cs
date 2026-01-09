using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Services.Auth;

namespace Foodbook.Views;

public partial class ProfilePage : ContentPage
{
    private ISupabaseAuthService? _supabaseAuth;

    public ProfilePage()
    {
        InitializeComponent();
    }

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();
    private async void OnProfileFetchJwtClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ProfilePage] Button clicked");
        try
        {
            var auth = GetSupabaseAuth();
            if (auth == null)
            {
                await DisplayAlert("Profil", "Brak ISupabaseAuthService w DI.", "OK");
                return;
            }

            // 1) Zbierz dane logowania (najpierw email, potem has³o)
            var email = await DisplayPromptAsync(
                title: "Logowanie",
                message: "Email",
                accept: "OK",
                cancel: "Anuluj",
                placeholder: "name@example.com",
                keyboard: Keyboard.Email);

            if (email is null)
            {
                // U¿ytkownik anulowa³ – nic dalej nie rób
                System.Diagnostics.Debug.WriteLine("[ProfilePage] Email prompt cancelled");
                return;
            }
            email = email.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                await DisplayAlert("Logowanie", "Email nie mo¿e byæ pusty.", "OK");
                return;
            }

            var password = await DisplayPromptAsync(
                title: "Logowanie",
                message: "Has³o",
                accept: "OK",
                cancel: "Anuluj",
                placeholder: string.Empty,
                keyboard: Keyboard.Text);

            if (password is null)
            {
                System.Diagnostics.Debug.WriteLine("[ProfilePage] Password prompt cancelled");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Logowanie", "Has³o nie mo¿e byæ puste.", "OK");
                return;
            }

            // 2) Wykonaj logowanie dopiero po zebraniu danych
            StatusLabel.Text = "Trwa logowanie...";
            var session = await auth.SignInAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                StatusLabel.Text = string.Empty;
                await DisplayAlert("B³¹d logowania", "Nie uda³o siê zalogowaæ. SprawdŸ dane logowania.", "OK");
                return;
            }

            var tokenPreview = session.AccessToken.Length > 40
                ? session.AccessToken.Substring(0, 40) + "..."
                : session.AccessToken;

            StatusLabel.Text = string.Empty;
            await DisplayAlert("Sukces", $"Zalogowano: {session.User?.Email}\nToken: {tokenPreview}", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] Exception: {ex.Message}");
            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await DisplayAlert("B³¹d", msg, "OK");
            StatusLabel.Text = string.Empty;
        }
    }
}
