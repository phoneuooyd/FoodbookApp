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
using AppTheme = Foodbook.Models.AppTheme;

namespace Foodbook.ViewModels;

public class SetupWizardViewModel : INotifyPropertyChanged
{
    private readonly IPreferencesService _preferencesService;
    private readonly ILocalizationService _localizationService;
    private readonly LocalizationResourceManager _localizationManager;
    private readonly IThemeService _themeService;
    private readonly IFontService _fontService;

    private LanguageOption _selectedLanguage;
    private bool _installBasicIngredients = true;
    private bool _isCompleting;
    private string _statusMessage = string.Empty;
    private SetupWizardStep _currentStep = SetupWizardStep.Language;
    private bool _isGuestMode;
    private string _selectedPlanChoice = string.Empty;
    private AppTheme _selectedTheme;
    private AppFontFamily _selectedFontFamily;

    public SetupWizardViewModel(
        IPreferencesService preferencesService,
        ILocalizationService localizationService,
        LocalizationResourceManager localizationManager,
        IThemeService themeService,
        IFontService fontService,
        SetupLoginViewModel setupLoginViewModel)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));

        LoginViewModel = setupLoginViewModel ?? throw new ArgumentNullException(nameof(setupLoginViewModel));
        LoginViewModel.LoginStepCompleted += (_, args) =>
        {
            IsGuestMode = !args.IsAuthenticated;
            if (IsGuestMode)
            {
                SelectedPlanChoice = "Free";
            }

            MoveToNextStep();
        };

        _localizationManager.PropertyChanged += OnLocalizationManagerPropertyChanged;

        InitializeLanguages();
        InitializeAppearanceSettings();
        InstallBasicIngredients = _preferencesService.GetInstallBasicIngredients();

        SelectInstallBasicIngredientsCommand = new Command(() => InstallBasicIngredients = true);
        SelectSkipBasicIngredientsCommand = new Command(() => InstallBasicIngredients = false);
        SelectLanguageCommand = new Command<LanguageOption>(SelectLanguage);
        SelectDarkThemeCommand = new Command(() => SelectedTheme = AppTheme.Dark);
        SelectLightThemeCommand = new Command(() => SelectedTheme = AppTheme.Light);

        NextStepCommand = new Command(MoveToNextStep, () => !IsCompleting && CanMoveToNextStep);
        PreviousStepCommand = new Command(MoveToPreviousStep, () => CanGoBack);
        SelectFreePlanCommand = new Command(() => SelectPlan("Free"), () => !IsCompleting);
        SelectPremiumPlanCommand = new Command(() => SelectPlan("Premium"), () => !IsCompleting && !IsGuestMode);
        CompleteSetupCommand = new Command(async () => await CompleteSetupAsync(), () => CanCompleteSetup);
    }

    public SetupLoginViewModel LoginViewModel { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();
    public ObservableCollection<AppTheme> AvailableThemes { get; } = new();
    public ObservableCollection<AppFontFamily> AvailableFontFamilies { get; } = new();

    public SetupWizardStep CurrentStep
    {
        get => _currentStep;
        set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLanguageStep));
            OnPropertyChanged(nameof(IsIngredientsStep));
            OnPropertyChanged(nameof(IsAppearanceStep));
            OnPropertyChanged(nameof(IsLoginStep));
            OnPropertyChanged(nameof(IsPlanSelectionStep));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanMoveToNextStep));
            OnPropertyChanged(nameof(CurrentStepIndex));
            OnPropertyChanged(nameof(CurrentStepLabel));
            OnPropertyChanged(nameof(CurrentStepTitle));
            OnPropertyChanged(nameof(CurrentStepSubtitle));
            OnPropertyChanged(nameof(IsStep1Active));
            OnPropertyChanged(nameof(IsStep2Active));
            OnPropertyChanged(nameof(IsStep3Active));
            OnPropertyChanged(nameof(IsStep4Active));
            OnPropertyChanged(nameof(IsStep5Active));
            OnPropertyChanged(nameof(IsStep1Completed));
            OnPropertyChanged(nameof(IsStep2Completed));
            OnPropertyChanged(nameof(IsStep3Completed));
            OnPropertyChanged(nameof(IsStep4Completed));
            OnPropertyChanged(nameof(IsStep5Completed));
            ((Command)PreviousStepCommand).ChangeCanExecute();
            ((Command)NextStepCommand).ChangeCanExecute();
        }
    }

    public int CurrentStepIndex => CurrentStep switch
    {
        SetupWizardStep.Language => 0,
        SetupWizardStep.Ingredients => 1,
        SetupWizardStep.Appearance => 2,
        SetupWizardStep.Login => 3,
        SetupWizardStep.PlanSelection => 4,
        _ => 0
    };

    public string CurrentStepLabel => CurrentStep switch
    {
        SetupWizardStep.Language => _localizationService.GetString("SetupWizardPageResources", "Step1Label"),
        SetupWizardStep.Ingredients => _localizationService.GetString("SetupWizardPageResources", "Step2Label"),
        SetupWizardStep.Appearance => _localizationService.GetString("SetupWizardPageResources", "Step3Label"),
        SetupWizardStep.Login => _localizationService.GetString("SetupWizardPageResources", "Step4Label"),
        SetupWizardStep.PlanSelection => _localizationService.GetString("SetupWizardPageResources", "Step5Label"),
        _ => string.Empty
    };

    public string CurrentStepTitle => CurrentStep switch
    {
        SetupWizardStep.Language => _localizationService.GetString("SetupWizardPageResources", "Step1Title"),
        SetupWizardStep.Ingredients => _localizationService.GetString("SetupWizardPageResources", "Step2Title"),
        SetupWizardStep.Appearance => _localizationService.GetString("SetupWizardPageResources", "Step3Title"),
        SetupWizardStep.Login => _localizationService.GetString("SetupWizardPageResources", "Step4Title"),
        SetupWizardStep.PlanSelection => _localizationService.GetString("SetupWizardPageResources", "Step5Title"),
        _ => string.Empty
    };

    public string CurrentStepSubtitle => CurrentStep switch
    {
        SetupWizardStep.Language => _localizationService.GetString("SetupWizardPageResources", "Step1Subtitle"),
        SetupWizardStep.Ingredients => _localizationService.GetString("SetupWizardPageResources", "Step2Subtitle"),
        SetupWizardStep.Appearance => _localizationService.GetString("SetupWizardPageResources", "Step3Subtitle"),
        SetupWizardStep.Login => _localizationService.GetString("SetupWizardPageResources", "Step4Subtitle"),
        SetupWizardStep.PlanSelection => _localizationService.GetString("SetupWizardPageResources", "Step5Subtitle"),
        _ => string.Empty
    };

    public bool IsStep1Active => CurrentStepIndex == 0;
    public bool IsStep2Active => CurrentStepIndex == 1;
    public bool IsStep3Active => CurrentStepIndex == 2;
    public bool IsStep4Active => CurrentStepIndex == 3;
    public bool IsStep5Active => CurrentStepIndex == 4;

    public bool IsStep1Completed => CurrentStepIndex > 0;
    public bool IsStep2Completed => CurrentStepIndex > 1;
    public bool IsStep3Completed => CurrentStepIndex > 2;
    public bool IsStep4Completed => CurrentStepIndex > 3;
    public bool IsStep5Completed => CurrentStepIndex > 4;

    public bool IsLanguageStep => CurrentStep == SetupWizardStep.Language;
    public bool IsIngredientsStep => CurrentStep == SetupWizardStep.Ingredients;
    public bool IsAppearanceStep => CurrentStep == SetupWizardStep.Appearance;
    public bool IsLoginStep => CurrentStep == SetupWizardStep.Login;
    public bool IsPlanSelectionStep => CurrentStep == SetupWizardStep.PlanSelection;
    public bool CanGoBack => !IsCompleting && CurrentStep != SetupWizardStep.Language;
    public bool CanMoveToNextStep => CurrentStep is SetupWizardStep.Language or SetupWizardStep.Ingredients or SetupWizardStep.Appearance;
    public bool CanCompleteSetup => !IsCompleting && !string.IsNullOrWhiteSpace(SelectedPlanChoice);

    public bool IsDarkThemeSelected => SelectedTheme == AppTheme.Dark;
    public bool IsLightThemeSelected => SelectedTheme == AppTheme.Light;

    public bool IsGuestMode
    {
        get => _isGuestMode;
        set
        {
            if (_isGuestMode == value) return;
            _isGuestMode = value;

            if (_isGuestMode && SelectedPlanChoice == "Premium")
            {
                SelectedPlanChoice = "Free";
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CanSelectPremiumPlan));
            ((Command)SelectPremiumPlanCommand).ChangeCanExecute();
        }
    }

    public bool CanSelectPremiumPlan => !IsGuestMode;
    public bool IsFreePlanSelected => SelectedPlanChoice == "Free";
    public bool IsPremiumPlanSelected => SelectedPlanChoice == "Premium";

    public string SelectedPlanChoice
    {
        get => _selectedPlanChoice;
        set
        {
            if (_selectedPlanChoice == value) return;
            _selectedPlanChoice = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFreePlanSelected));
            OnPropertyChanged(nameof(IsPremiumPlanSelected));
            OnPropertyChanged(nameof(CanCompleteSetup));
            ((Command)CompleteSetupCommand).ChangeCanExecute();
        }
    }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value && value != null)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                ApplyLanguageSelection(_selectedLanguage);
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

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;
            _selectedTheme = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDarkThemeSelected));
            OnPropertyChanged(nameof(IsLightThemeSelected));
            _themeService.SetTheme(_selectedTheme);
        }
    }

    public AppFontFamily SelectedFontFamily
    {
        get => _selectedFontFamily;
        set
        {
            if (_selectedFontFamily == value) return;
            _selectedFontFamily = value;
            OnPropertyChanged();
            _fontService.SetFontFamily(_selectedFontFamily);
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
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanCompleteSetup));
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

    public ICommand SelectInstallBasicIngredientsCommand { get; }
    public ICommand SelectSkipBasicIngredientsCommand { get; }
    public ICommand SelectLanguageCommand { get; }
    public ICommand SelectDarkThemeCommand { get; }
    public ICommand SelectLightThemeCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand SelectFreePlanCommand { get; }
    public ICommand SelectPremiumPlanCommand { get; }
    public ICommand CompleteSetupCommand { get; }

    private void OnLocalizationManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(string.Empty);
        OnPropertyChanged(nameof(CurrentStepLabel));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CurrentStepSubtitle));
    }

    private void MoveToNextStep()
    {
        CurrentStep = CurrentStep switch
        {
            SetupWizardStep.Language => SetupWizardStep.Ingredients,
            SetupWizardStep.Ingredients => SetupWizardStep.Appearance,
            SetupWizardStep.Appearance => SetupWizardStep.Login,
            SetupWizardStep.Login => SetupWizardStep.PlanSelection,
            _ => SetupWizardStep.PlanSelection
        };
    }

    private void SelectLanguage(LanguageOption? option)
    {
        if (option == null)
            return;

        SelectedLanguage = option;
    }

    private void ApplyLanguageSelection(LanguageOption selected)
    {
        foreach (var language in AvailableLanguages)
        {
            language.IsSelected = ReferenceEquals(language, selected);
        }
    }

    public void MoveToPreviousStep()
    {
        if (!CanGoBack)
            return;

        CurrentStep = CurrentStep switch
        {
            SetupWizardStep.Ingredients => SetupWizardStep.Language,
            SetupWizardStep.Appearance => SetupWizardStep.Ingredients,
            SetupWizardStep.Login => SetupWizardStep.Appearance,
            SetupWizardStep.PlanSelection => SetupWizardStep.Login,
            _ => SetupWizardStep.Language
        };
    }

    private void SelectPlan(string planChoice)
    {
        if (planChoice == "Premium" && IsGuestMode)
            return;

        SelectedPlanChoice = planChoice;
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
                    "en" => new LanguageOption { CultureCode = "en", DisplayName = "English", NativeName = "English", CountryName = "United Kingdom", FlagEmoji = "🇬🇧" },
                    "pl-PL" => new LanguageOption { CultureCode = "pl-PL", DisplayName = "Polish", NativeName = "Polski", CountryName = "Polska", FlagEmoji = "🇵🇱" },
                    "de-DE" => new LanguageOption { CultureCode = "de-DE", DisplayName = "German", NativeName = "Deutsch", CountryName = "Deutschland", FlagEmoji = "🇩🇪" },
                    "es-ES" => new LanguageOption { CultureCode = "es-ES", DisplayName = "Spanish", NativeName = "Español", CountryName = "España", FlagEmoji = "🇪🇸" },
                    "fr-FR" => new LanguageOption { CultureCode = "fr-FR", DisplayName = "French", NativeName = "Français", CountryName = "France", FlagEmoji = "🇫🇷" },
                    "ko-KR" => new LanguageOption { CultureCode = "ko-KR", DisplayName = "Korean", NativeName = "한국어", CountryName = "대한민국", FlagEmoji = "🇰🇷" },
                    _ => new LanguageOption { CultureCode = culture, DisplayName = culture, NativeName = culture, CountryName = culture, FlagEmoji = "🌐" }
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
                AvailableLanguages.Add(new LanguageOption { CultureCode = "en", DisplayName = "English", NativeName = "English", CountryName = "United Kingdom", FlagEmoji = "🇬🇧", IsSelected = true });
                SelectedLanguage = AvailableLanguages.First();
            }
        }
    }

    private void InitializeAppearanceSettings()
    {
        AvailableThemes.Add(AppTheme.Dark);
        AvailableThemes.Add(AppTheme.Light);

        foreach (var fontFamily in _fontService.GetAvailableFontFamilies())
        {
            AvailableFontFamilies.Add(fontFamily);
        }

        var savedTheme = _preferencesService.GetSavedTheme();
        SelectedTheme = savedTheme is AppTheme.Dark or AppTheme.Light
            ? savedTheme
            : AppTheme.Dark;

        SelectedFontFamily = _preferencesService.GetSavedFontFamily();
    }

    private async Task CompleteSetupAsync()
    {
        try
        {
            if (!CanCompleteSetup)
                return;

            IsCompleting = true;
            StatusMessage = "Completing setup...";

            if (SelectedLanguage != null)
            {
                _preferencesService.SaveLanguage(SelectedLanguage.CultureCode);
                await Task.Delay(100);
            }

            _preferencesService.SavePlanChoice(SelectedPlanChoice);
            _preferencesService.SaveTheme(SelectedTheme);
            _preferencesService.SaveFontFamily(SelectedFontFamily);
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
    Language,
    Ingredients,
    Appearance,
    Login,
    PlanSelection
}

public class LanguageOption : INotifyPropertyChanged
{
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = "🌐";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public override string ToString() => NativeName;

    public event PropertyChangedEventHandler? PropertyChanged;
}
