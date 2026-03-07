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
    private readonly LocalizationResourceManager _localizationManager;
    private readonly IThemeService _themeService;
    private readonly IFontService _fontService;

    private LanguageOption _selectedLanguage;
    private ThemeOption _selectedTheme;
    private FontOption _selectedFont;
    private bool _installBasicIngredients = true;
    private bool _isCompleting;
    private string _statusMessage = string.Empty;
    private SetupWizardStep _currentStep = SetupWizardStep.Preferences;
    private bool _isGuestFlow;

    public SetupWizardViewModel(
        IPreferencesService preferencesService,
        LocalizationResourceManager localizationManager,
        IThemeService themeService,
        IFontService fontService,
        SetupLoginViewModel setupLoginViewModel)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));

        LoginViewModel = setupLoginViewModel ?? throw new ArgumentNullException(nameof(setupLoginViewModel));
        LoginViewModel.LoginStepCompleted += OnLoginStepCompleted;

        _localizationManager.PropertyChanged += OnLocalizationManagerPropertyChanged;

        InitializeLanguages();
        InitializeAppearanceOptions();

        InstallBasicIngredients = _preferencesService.GetInstallBasicIngredients();

        NextStepCommand = new Command(MoveToNextStep, () => !IsCompleting && CurrentStep != SetupWizardStep.PlanSelection);
        PreviousStepCommand = new Command(MoveToPreviousStep, () => CanGoBack && !IsCompleting);
        SelectFreePlanCommand = new Command(() => SelectPlan("Free"), () => !IsCompleting);
        SelectPremiumPlanCommand = new Command(() => SelectPlan("Premium"), () => !IsCompleting && !IsGuestFlow);
        CompleteSetupCommand = new Command(async () => await CompleteSetupAsync(), () => !IsCompleting && CanCompleteCurrentStep);
    }

    public SetupLoginViewModel LoginViewModel { get; }
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();
    public ObservableCollection<ThemeOption> AvailableThemes { get; } = new();
    public ObservableCollection<FontOption> AvailableFonts { get; } = new();

    public SetupWizardStep CurrentStep
    {
        get => _currentStep;
        set
        {
            if (_currentStep == value) return;
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsLoginStep));
            OnPropertyChanged(nameof(IsPlanSelectionStep));
            OnPropertyChanged(nameof(IsPreferencesStep));
            OnPropertyChanged(nameof(IsAppearanceStep));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanCompleteCurrentStep));
            OnPropertyChanged(nameof(ShowNextButton));
            ((Command)NextStepCommand).ChangeCanExecute();
            ((Command)PreviousStepCommand).ChangeCanExecute();
            ((Command)CompleteSetupCommand).ChangeCanExecute();
        }
    }

    public bool IsLoginStep => CurrentStep == SetupWizardStep.Login;
    public bool IsPlanSelectionStep => CurrentStep == SetupWizardStep.PlanSelection;
    public bool IsPreferencesStep => CurrentStep == SetupWizardStep.Preferences;
    public bool IsAppearanceStep => CurrentStep == SetupWizardStep.Appearance;
    public bool CanGoBack => CurrentStep != SetupWizardStep.Preferences;
    public bool ShowNextButton => CurrentStep != SetupWizardStep.Login && CurrentStep != SetupWizardStep.PlanSelection;

    public bool CanCompleteCurrentStep =>
        CurrentStep == SetupWizardStep.PlanSelection ||
        (CurrentStep == SetupWizardStep.Login && IsGuestFlow);

    public bool IsGuestFlow
    {
        get => _isGuestFlow;
        set
        {
            if (_isGuestFlow == value) return;
            _isGuestFlow = value;
            OnPropertyChanged();
            ((Command)SelectPremiumPlanCommand).ChangeCanExecute();
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

    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value || value == null) return;
            _selectedTheme = value;
            OnPropertyChanged();
            _themeService.SetTheme(value.Theme);
        }
    }

    public FontOption SelectedFont
    {
        get => _selectedFont;
        set
        {
            if (_selectedFont == value || value == null) return;
            _selectedFont = value;
            OnPropertyChanged();
            _fontService.SetFontFamily(value.FontFamily);
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

    private void OnLoginStepCompleted(object? sender, SetupLoginCompletedEventArgs e)
    {
        IsGuestFlow = e.IsGuestFlow;

        if (IsGuestFlow)
        {
            _preferencesService.SavePlanChoice("Free");
        }

        MoveToNextStep();
    }

    public bool HandleBackNavigation()
    {
        if (!CanGoBack || IsCompleting)
            return false;

        MoveToPreviousStep();
        return true;
    }

    private void MoveToNextStep()
    {
        CurrentStep = CurrentStep switch
        {
            SetupWizardStep.Preferences => SetupWizardStep.Appearance,
            SetupWizardStep.Appearance => SetupWizardStep.Login,
            SetupWizardStep.Login => SetupWizardStep.PlanSelection,
            _ => SetupWizardStep.PlanSelection
        };
    }

    private void MoveToPreviousStep()
    {
        CurrentStep = CurrentStep switch
        {
            SetupWizardStep.Appearance => SetupWizardStep.Preferences,
            SetupWizardStep.Login => SetupWizardStep.Appearance,
            SetupWizardStep.PlanSelection => SetupWizardStep.Login,
            _ => SetupWizardStep.Preferences
        };
    }

    private async void SelectPlan(string planChoice)
    {
        _preferencesService.SavePlanChoice(planChoice);
        await CompleteSetupAsync();
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

    private void InitializeAppearanceOptions()
    {
        AvailableThemes.Clear();
        AvailableThemes.Add(new ThemeOption(AppTheme.System, "System"));
        AvailableThemes.Add(new ThemeOption(AppTheme.Light, "Light"));
        AvailableThemes.Add(new ThemeOption(AppTheme.Dark, "Dark"));

        var savedTheme = _preferencesService.GetSavedTheme();
        SelectedTheme = AvailableThemes.FirstOrDefault(x => x.Theme == savedTheme) ?? AvailableThemes.First();

        AvailableFonts.Clear();
        foreach (var font in _fontService.GetAvailableFontFamilies())
        {
            AvailableFonts.Add(new FontOption(font, font.ToString()));
        }

        var savedFont = _preferencesService.GetSavedFontFamily();
        SelectedFont = AvailableFonts.FirstOrDefault(x => x.FontFamily == savedFont) ?? AvailableFonts.First();
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

            if (SelectedTheme != null)
            {
                _preferencesService.SaveTheme(SelectedTheme.Theme);
                _themeService.SetTheme(SelectedTheme.Theme);
            }

            if (SelectedFont != null)
            {
                _preferencesService.SaveFontFamily(SelectedFont.FontFamily);
                _fontService.SetFontFamily(SelectedFont.FontFamily);
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
    Preferences,
    Appearance,
    Login,
    PlanSelection
}

public class LanguageOption
{
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;

    public override string ToString() => NativeName;
}

public class ThemeOption
{
    public ThemeOption(AppTheme theme, string displayName)
    {
        Theme = theme;
        DisplayName = displayName;
    }

    public AppTheme Theme { get; }
    public string DisplayName { get; }
}

public class FontOption
{
    public FontOption(AppFontFamily fontFamily, string displayName)
    {
        FontFamily = fontFamily;
        DisplayName = displayName;
    }

    public AppFontFamily FontFamily { get; }
    public string DisplayName { get; }
}
