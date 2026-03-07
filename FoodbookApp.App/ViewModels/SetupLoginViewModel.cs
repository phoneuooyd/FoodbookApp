using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using FoodbookApp.Localization;

namespace Foodbook.ViewModels;

public class SetupLoginViewModel : INotifyPropertyChanged
{
    private readonly IAccountService _accountService;
    private readonly ISupabaseAuthService _supabaseAuthService;

    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isRegisterMode;
    private bool _isBusy;
    private string _errorMessage = string.Empty;

    public SetupLoginViewModel(IAccountService accountService, ISupabaseAuthService supabaseAuthService)
    {
        _accountService = accountService;
        _supabaseAuthService = supabaseAuthService;

        ToggleModeCommand = new Command(ToggleMode, () => !IsBusy);
        SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsBusy);
        ContinueWithoutAccountCommand = new Command(async () => await ContinueWithoutAccountAsync(), () => !IsBusy);
    }

    public event EventHandler<SetupLoginCompletedEventArgs>? LoginStepCompleted;

    public string Email
    {
        get => _email;
        set
        {
            if (_email == value) return;
            _email = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password == value) return;
            _password = value;
            OnPropertyChanged();
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (_confirmPassword == value) return;
            _confirmPassword = value;
            OnPropertyChanged();
        }
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (_isRegisterMode == value) return;
            _isRegisterMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLoginMode));
        }
    }

    public bool IsLoginMode => !IsRegisterMode;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            ((Command)ToggleModeCommand).ChangeCanExecute();
            ((Command)SubmitCommand).ChangeCanExecute();
            ((Command)ContinueWithoutAccountCommand).ChangeCanExecute();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public ICommand ToggleModeCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand ContinueWithoutAccountCommand { get; }

    private void ToggleMode()
    {
        ErrorMessage = string.Empty;
        IsRegisterMode = !IsRegisterMode;
    }

    private async Task SubmitAsync()
    {
        ErrorMessage = string.Empty;

        var validation = Validate();
        if (!string.IsNullOrWhiteSpace(validation))
        {
            ErrorMessage = validation;
            return;
        }

        try
        {
            IsBusy = true;

            if (IsRegisterMode)
            {
                await _accountService.SignUpAsync(Email.Trim(), Password, enableAutoLogin: true);
            }
            else
            {
                await _accountService.SignInAsync(Email.Trim(), Password, enableAutoLogin: true);
            }

            LoginStepCompleted?.Invoke(this, new SetupLoginCompletedEventArgs(isGuestFlow: false));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string Validate()
    {
        if (string.IsNullOrWhiteSpace(Email))
            return Localize("EmailRequired");

        if (!Email.Contains('@'))
            return Localize("EmailInvalid");

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
            return Localize("PasswordTooShort");

        if (IsRegisterMode && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            return Localize("PasswordsDoNotMatch");

        return string.Empty;
    }

    private static string Localize(string key)
    {
        return SetupWizardPageResources.ResourceManager.GetString(key, SetupWizardPageResources.Culture)
               ?? key;
    }

    private async Task ContinueWithoutAccountAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            IsBusy = true;

            if (_supabaseAuthService.CurrentSession != null)
            {
                await _supabaseAuthService.SignOutAsync();
            }

            LoginStepCompleted?.Invoke(this, new SetupLoginCompletedEventArgs(isGuestFlow: true));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class SetupLoginCompletedEventArgs : EventArgs
{
    public SetupLoginCompletedEventArgs(bool isGuestFlow)
    {
        IsGuestFlow = isGuestFlow;
    }

    public bool IsGuestFlow { get; }
}
