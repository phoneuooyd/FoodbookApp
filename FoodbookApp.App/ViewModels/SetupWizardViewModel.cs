using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp;
using FoodbookApp.Interfaces;

namespace Foodbook.ViewModels;

public class SetupWizardViewModel : INotifyPropertyChanged
{
    private readonly IPreferencesService _preferencesService;
    private readonly ILocalizationService _localizationService;
    private readonly LocalizationResourceManager _localizationManager;

    private LanguageOption _selectedLanguage;
    private bool _installBasicIngredients = true;
    private bool _isCompleting;
    private string _statusMessage = string.Empty;
    private SetupWizardStep _currentStep = SetupWizardStep.Welcome;

    public SetupWizardViewModel(
        IPreferencesService preferencesService,
        ILocalizationService localizationService,
        LocalizationResourceManager localizationManager,
        SetupLoginViewModel setupLoginViewModel)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));

        LoginViewModel = setupLoginViewModel ?? throw new ArgumentNullException(nameof(setupLoginViewModel));
        LoginViewModel.LoginStepCompleted += (_, _) => MoveToNextStep();

        _localizationManager.PropertyChanged += OnLocalizationManagerPropertyChanged;

        InitializeLanguages();
        InstallBasicIngredients = _preferencesService.GetInstallBasicIngredients();

        NextStepCommand = new Command(MoveToNextStep, () => !IsCompleting);
        PreviousStepCommand = new Command(MoveToPreviousStep, () => !IsCompleting && CurrentStep != SetupWizardStep.Welcome);
        SelectFreePlanCommand = new Command(() => SelectPlan("Free"), () => !IsCompleting);
        SelectPremiumPlanCommand = new Command(() => SelectPlan("Premium"), () => !IsCompleting);
        CompleteSetupCommand = new Command(async () => await CompleteSetupAsync(), () => !IsCompleting);
    }

    public SetupLoginViewModel LoginViewModel { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

    public SetupWizardStep CurrentStep
    {
        get => _currentStep;
        set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWelcomeStep));
            OnPropertyChanged(nameof(IsLoginStep));
            OnPropertyChanged(nameof(IsPlanSelectionStep));
            OnPropertyChanged(nameof(IsPreferencesStep));
            ((Command)PreviousStepCommand).ChangeCanExecute();
        }
    }

    public bool IsWelcomeStep => CurrentStep == SetupWizardStep.Welcome;
    public bool IsLoginStep => CurrentStep == SetupWizardStep.Login;
    public bool IsPlanSelectionStep => CurrentStep == SetupWizardStep.PlanSelection;
    public bool IsPreferencesStep => CurrentStep == SetupWizardStep.Preferences;

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value && value != null)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        _localizationManager.SetCulture(value.CultureCode);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Failed to set culture: {ex.Message}");
                    }
                });
            }
        }
    }

    public bool InstallBasicIngredients
    {
        get => _installBasicIngredients;
        set
        {
            if (_installBasicIngredients == value) return;
            _installBasicIngredients = value;
            OnPropertyChanged();
        }
    }

    public bool IsCompleting
    {
        get => _isCompleting;
        set
        {
            if (_isCompleting == value) return;
            _isCompleting = value;
            OnPropertyChanged();
            ((Command)NextStepCommand).ChangeCanExecute();
            ((Command)PreviousStepCommand).ChangeCanExecute();
            ((Command)SelectFreePlanCommand).ChangeCanExecute();
            ((Command)SelectPremiumPlanCommand).ChangeCanExecute();
            ((Command)CompleteSetupCommand).ChangeCanExecute();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand SelectFreePlanCommand { get; }
    public ICommand SelectPremiumPlanCommand { get; }
    public ICommand CompleteSetupCommand { get; }

    private void OnLocalizationManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

    private void MoveToNextStep()
    {
        CurrentStep = CurrentStep switch
        {
            SetupWizardStep.Welcome => SetupWizardStep.Login,
            SetupWizardStep.Login => SetupWizardStep.PlanSelection,
            SetupWizardStep.PlanSelection => SetupWizardStep.Preferences,
            _ => SetupWizardStep.Preferences
        };
    }

    private void MoveToPreviousStep()
    {
        CurrentStep = CurrentStep switch
        {
            SetupWizardStep.Login => SetupWizardStep.Welcome,
            SetupWizardStep.PlanSelection => SetupWizardStep.Login,
            SetupWizardStep.Preferences => SetupWizardStep.PlanSelection,
            _ => SetupWizardStep.Welcome
        };
    }

    private void SelectPlan(string planChoice)
    {
        _preferencesService.SavePlanChoice(planChoice);
        MoveToNextStep();
    }

    private void InitializeLanguages()
    {
        try
        {
            var supportedCultures = _preferencesService.GetSupportedCultures();
            foreach (var culture in supportedCultures)
            {
                var languageOption = culture switch
                {
                    "en" => new LanguageOption { CultureCode = "en", DisplayName = "English", NativeName = "English" },
                    "pl-PL" => new LanguageOption { CultureCode = "pl-PL", DisplayName = "Polish", NativeName = "Polski" },
                    "de-DE" => new LanguageOption { CultureCode = "de-DE", DisplayName = "German", NativeName = "Deutsch" },
                    "es-ES" => new LanguageOption { CultureCode = "es-ES", DisplayName = "Spanish", NativeName = "Español" },
                    "fr-FR" => new LanguageOption { CultureCode = "fr-FR", DisplayName = "French", NativeName = "Français" },
                    "ko-KR" => new LanguageOption { CultureCode = "ko-KR", DisplayName = "Korean", NativeName = "한국어" },
                    _ => new LanguageOption { CultureCode = culture, DisplayName = culture, NativeName = culture }
                };

                AvailableLanguages.Add(languageOption);
            }

            var saved = _preferencesService.GetSavedLanguage();
            var defaultLanguage = !string.IsNullOrWhiteSpace(saved)
                ? AvailableLanguages.FirstOrDefault(l => l.CultureCode == saved)
                : null;

            defaultLanguage ??= AvailableLanguages.FirstOrDefault(l => l.CultureCode == System.Globalization.CultureInfo.CurrentUICulture.Name)
                               ?? AvailableLanguages.FirstOrDefault();

            if (defaultLanguage != null)
                SelectedLanguage = defaultLanguage;
        }
        catch
        {
            if (!AvailableLanguages.Any())
            {
                AvailableLanguages.Add(new LanguageOption { CultureCode = "en", DisplayName = "English", NativeName = "English" });
                SelectedLanguage = AvailableLanguages.First();
            }
        }
    }

    private async Task CompleteSetupAsync()
    {
        try
        {
            IsCompleting = true;
            StatusMessage = "Completing setup...";

            if (SelectedLanguage != null)
            {
                _preferencesService.SaveLanguage(SelectedLanguage.CultureCode);
                await Task.Delay(100);
            }

            _preferencesService.SaveInstallBasicIngredients(InstallBasicIngredients);

            if (InstallBasicIngredients)
            {
                StatusMessage = "Installing basic ingredients...";
                await Task.Delay(100);

                var lang = SelectedLanguage?.CultureCode;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var services = FoodbookApp.MauiProgram.ServiceProvider;
                        if (services != null)
                        {
                            using var scope = services.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            await SeedData.SeedIngredientsAsync(dbContext, lang);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Error seeding ingredients: {ex.Message}");
                    }
                });
            }

            _preferencesService.MarkInitialSetupCompleted();
            StatusMessage = "Setup completed!";
            await Task.Delay(300);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var app = Application.Current;
                if (app?.MainPage != null)
                    app.MainPage = new AppShell();
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var app = Application.Current;
                if (app != null)
                    app.MainPage = new AppShell();
            });
        }
        finally
        {
            IsCompleting = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum SetupWizardStep
{
    Welcome,
    Login,
    PlanSelection,
    Preferences
}

public class LanguageOption
{
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;

    public override string ToString() => NativeName;
}
