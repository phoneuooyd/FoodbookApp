using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp;

namespace Foodbook.ViewModels;

/// <summary>
/// ViewModel for the initial setup wizard
/// </summary>
public class SetupWizardViewModel : INotifyPropertyChanged
{
    private readonly IPreferencesService _preferencesService;
    private readonly ILocalizationService _localizationService; // kept for persistence only
    private readonly LocalizationResourceManager _localizationManager; // used to broadcast dynamic changes
    private LanguageOption _selectedLanguage;
    private bool _installBasicIngredients = true;
    private bool _isCompleting = false;
    private string _statusMessage = string.Empty;

    public SetupWizardViewModel(IPreferencesService preferencesService,
                                ILocalizationService localizationService,
                                LocalizationResourceManager localizationManager)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizationManager = localizationManager ?? throw new ArgumentNullException(nameof(localizationManager));
        _localizationManager.PropertyChanged += OnLocalizationManagerPropertyChanged;
        InitializeLanguages();
        CompleteSetupCommand = new Command(async () => await CompleteSetupAsync(), () => !IsCompleting);
    }

    private void OnLocalizationManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Force refresh of labels whose values come purely from TranslateExtension but might cache
        OnPropertyChanged(string.Empty); // broadcast change
    }

    #region Properties

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value && value != null)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                // Change culture via manager (fires PropertyChanged(null) to all TranslateExtension bindings)
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
            if (_installBasicIngredients != value)
            {
                _installBasicIngredients = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleting
    {
        get => _isCompleting;
        set
        {
            if (_isCompleting != value)
            {
                _isCompleting = value;
                OnPropertyChanged();
                ((Command)CompleteSetupCommand).ChangeCanExecute();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Commands

    public ICommand CompleteSetupCommand { get; }

    #endregion

    #region Private Methods

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
                    "es-ES" => new LanguageOption { CultureCode = "es-ES", DisplayName = "Spanish", NativeName = "Espa?ol" },
                    "fr-FR" => new LanguageOption { CultureCode = "fr-FR", DisplayName = "French", NativeName = "Français" },
                    "ko-KR" => new LanguageOption { CultureCode = "ko-KR", DisplayName = "Korean", NativeName = "???" },
                    _ => new LanguageOption { CultureCode = culture, DisplayName = culture, NativeName = culture }
                };

                AvailableLanguages.Add(languageOption);
            }

            var saved = _preferencesService.GetSavedLanguage();
            LanguageOption? defaultLanguage = null;
            if (!string.IsNullOrWhiteSpace(saved))
                defaultLanguage = AvailableLanguages.FirstOrDefault(l => l.CultureCode == saved);

            if (defaultLanguage == null)
            {
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                defaultLanguage = AvailableLanguages.FirstOrDefault(l => l.CultureCode == systemCulture)
                                   ?? AvailableLanguages.FirstOrDefault();
            }

            if (defaultLanguage != null)
                SelectedLanguage = defaultLanguage;

            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Initialized {AvailableLanguages.Count} languages, selected: {SelectedLanguage?.CultureCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Error initializing languages: {ex.Message}");

            if (!AvailableLanguages.Any())
            {
                AvailableLanguages.Add(new LanguageOption
                {
                    CultureCode = "en",
                    DisplayName = "English",
                    NativeName = "English"
                });
                SelectedLanguage = AvailableLanguages.First();
            }
        }
    }

    private async Task CompleteSetupAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Starting CompleteSetupAsync");
            IsCompleting = true;
            StatusMessage = "Completing setup...";

            if (SelectedLanguage != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Saving language: {SelectedLanguage.CultureCode}");
                _preferencesService.SaveLanguage(SelectedLanguage.CultureCode);
                StatusMessage = "Language preference saved...";
                await Task.Delay(100);
            }

            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Saving install ingredients: {InstallBasicIngredients}");
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
                        System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Starting ingredient seeding with language override: " + lang);
                        var services = FoodbookApp.MauiProgram.ServiceProvider;
                        if (services != null)
                        {
                            using var scope = services.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            await SeedData.SeedIngredientsAsync(dbContext, lang);
                            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Ingredient seeding completed");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] ServiceProvider is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Error seeding ingredients: {ex.Message}");
                    }
                });
            }

            StatusMessage = "Finalizing setup...";
            await Task.Delay(100);

            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Marking setup as completed");
            _preferencesService.MarkInitialSetupCompleted();

            StatusMessage = "Setup completed!";
            await Task.Delay(500);

            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Navigating to main shell");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app?.MainPage != null)
                    {
                        app.MainPage = new AppShell();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Error replacing MainPage: {ex.Message}");
                }
            });

            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Setup wizard completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Error completing setup: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Stack trace: {ex.StackTrace}");
            StatusMessage = "Setup completed with some issues, but you can continue using the app.";
            await Task.Delay(1000);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var app = Application.Current;
                    if (app != null)
                    {
                        app.MainPage = new AppShell();
                    }
                }
                catch (Exception navEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Fallback navigation failed: {navEx.Message}");
                }
            });
        }
        finally
        {
            IsCompleting = false;
        }
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Represents a language option for the setup wizard
/// </summary>
public class LanguageOption
{
    public string CultureCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NativeName { get; set; } = string.Empty;

    public override string ToString()
    {
        return NativeName;
    }
}