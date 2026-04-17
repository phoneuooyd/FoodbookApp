using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using FoodbookApp.Localization;
using FoodbookApp.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.Views.Components;

public partial class RegisterPopup : Popup
{
    private readonly TaskCompletionSource<bool> _tcs = new();
    private ISupabaseAuthService? _supabaseAuth;

    public Task<bool> ResultTask => _tcs.Task;

    public ICommand CloseCommand { get; }
    public ICommand RegisterCommand { get; }

    public RegisterPopup()
    {
        CloseCommand = new Command(async () => await CloseWithResultAsync(false));
        RegisterCommand = new Command(async () => await OnRegisterAsync());

        InitializeComponent();
    }

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();

    private async Task OnRegisterAsync()
    {
        try
        {
            var auth = GetSupabaseAuth();
            if (auth is null)
            {
                await Shell.Current.DisplayAlert(
                    P("RegisterPopupTitle", "Register"),
                    P("RegisterServiceMissingMessage", "Authentication service unavailable."),
                    P("OK", "OK"));
                return;
            }

            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;
            var confirm = ConfirmPasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email))
            {
                await Shell.Current.DisplayAlert(P("RegisterPopupTitle", "Register"), P("EmailRequired", "Email cannot be empty."), P("OK", "OK"));
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                await Shell.Current.DisplayAlert(P("RegisterPopupTitle", "Register"), P("PasswordRequired", "Password cannot be empty."), P("OK", "OK"));
                return;
            }

            if (password != confirm)
            {
                await Shell.Current.DisplayAlert(P("RegisterPopupTitle", "Register"), P("PasswordsDoNotMatch", "Passwords do not match."), P("OK", "OK"));
                return;
            }

            StatusLabel.Text = P("RegisterInProgress", "Registering...");

            var session = await auth.SignUpAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                StatusLabel.Text = string.Empty;
                await Shell.Current.DisplayAlert(
                    P("RegisterPopupTitle", "Register"),
                    P("RegisterConfirmEmailMessage", "Check your email inbox to confirm registration."),
                    P("OK", "OK"));
                await CloseWithResultAsync(true);
            }

            StatusLabel.Text = string.Empty;

            PasswordEntry.Text = string.Empty;
            ConfirmPasswordEntry.Text = string.Empty;

            await Shell.Current.DisplayAlert(
                P("RegisterPopupTitle", "Register"),
                P("RegisterSuccessMessage", "Account has been created."),
                P("OK", "OK"));
            await CloseWithResultAsync(true);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = string.Empty;
            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await Shell.Current.DisplayAlert(P("GenericErrorTitle", "Error"), msg, P("OK", "OK"));
        }
    }

    private static string P(string key, string fallback)
        => ProfilePageResources.ResourceManager.GetString(key, ProfilePageResources.Culture) ?? fallback;

    private async Task CloseWithResultAsync(bool result)
    {
        try
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.SetResult(result);

            await CloseAsync();
        }
        catch
        {
        }
    }
}
