using System.Windows.Input;
using CommunityToolkit.Maui.Views;
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
                await Shell.Current.DisplayAlert("Rejestracja", "Brak ISupabaseAuthService w DI.", "OK");
                return;
            }

            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;
            var confirm = ConfirmPasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email))
            {
                await Shell.Current.DisplayAlert("Rejestracja", "Email nie mo¿e byæ pusty.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                await Shell.Current.DisplayAlert("Rejestracja", "Has³o nie mo¿e byæ puste.", "OK");
                return;
            }

            if (password != confirm)
            {
                await Shell.Current.DisplayAlert("Rejestracja", "Has³a nie s¹ takie same.", "OK");
                return;
            }

            StatusLabel.Text = "Trwa rejestracja...";

            var session = await auth.SignUpAsync(email, password);

            if (session == null || string.IsNullOrWhiteSpace(session.AccessToken))
            {
                StatusLabel.Text = string.Empty;
                await Shell.Current.DisplayAlert("Rejestracja", "Nie uda³o siê utworzyæ konta (mo¿liwa wymagana weryfikacja email).", "OK");
                return;
            }

            StatusLabel.Text = string.Empty;

            PasswordEntry.Text = string.Empty;
            ConfirmPasswordEntry.Text = string.Empty;

            await Shell.Current.DisplayAlert("Rejestracja", "Konto zosta³o utworzone.", "OK");
            await CloseWithResultAsync(true);
        }
        catch (Exception ex)
        {
            StatusLabel.Text = string.Empty;
            var msg = ex.InnerException?.Message is { Length: > 0 } inner ? inner : ex.Message;
            await Shell.Current.DisplayAlert("B³¹d", msg, "OK");
        }
    }

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
