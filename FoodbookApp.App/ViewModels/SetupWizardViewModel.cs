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
    private readonly ILocalizationService _localizationService;
    private LanguageOption _selectedLanguage;
    private bool _installBasicIngredients = true;
    private bool _isCompleting = false;
    private string _statusMessage = string.Empty;

    public SetupWizardViewModel(IPreferencesService preferencesService, ILocalizationService localizationService)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        
        InitializeLanguages();
        CompleteSetupCommand = new Command(async () => await CompleteSetupAsync(), () => !IsCompleting);
    }

    #region Properties

    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                
                // Natychmiast zmieñ jêzyk w aplikacji
                if (value != null)
                {
                    _localizationService.SetCulture(value.CultureCode);
                }
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
                    "en" => new LanguageOption 
                    { 
                        CultureCode = "en", 
                        DisplayName = "English", 
                        NativeName = "English" 
                    },
                    "pl-PL" => new LanguageOption 
                    { 
                        CultureCode = "pl-PL", 
                        DisplayName = "Polish", 
                        NativeName = "Polski" 
                    },
                    _ => new LanguageOption 
                    { 
                        CultureCode = culture, 
                        DisplayName = culture, 
                        NativeName = culture 
                    }
                };
                
                AvailableLanguages.Add(languageOption);
            }

            // Ustaw domyœlny jêzyk na systemowy lub pierwszy dostêpny
            var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var defaultLanguage = AvailableLanguages.FirstOrDefault(l => l.CultureCode == systemCulture) 
                                 ?? AvailableLanguages.FirstOrDefault();
            
            if (defaultLanguage != null)
            {
                SelectedLanguage = defaultLanguage;
            }

            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Initialized {AvailableLanguages.Count} languages, selected: {SelectedLanguage?.CultureCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Error initializing languages: {ex.Message}");
            
            // Fallback - dodaj przynajmniej angielski
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

            // Zapisz wybrane ustawienia
            if (SelectedLanguage != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Saving language: {SelectedLanguage.CultureCode}");
                _preferencesService.SaveLanguage(SelectedLanguage.CultureCode);
                StatusMessage = "Language preference saved...";
                await Task.Delay(100);
            }

            System.Diagnostics.Debug.WriteLine($"[SetupWizardViewModel] Saving install ingredients: {InstallBasicIngredients}");
            _preferencesService.SaveInstallBasicIngredients(InstallBasicIngredients);

            // Jeœli u¿ytkownik wybra³ instalacjê sk³adników, uruchom seedowanie
            if (InstallBasicIngredients)
            {
                StatusMessage = "Installing basic ingredients...";
                await Task.Delay(100);
                
                // Uruchom seedowanie sk³adników w tle
                _ = Task.Run(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Starting ingredient seeding");
                        var services = FoodbookApp.MauiProgram.ServiceProvider;
                        if (services != null)
                        {
                            using var scope = services.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                            await SeedData.SeedIngredientsAsync(dbContext);
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

            // Oznacz setup jako zakoñczony
            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Marking setup as completed");
            _preferencesService.MarkInitialSetupCompleted();

            StatusMessage = "Setup completed!";
            await Task.Delay(500);

            // Nawiguj do g³ównej strony aplikacji - zast¹p ca³e okno Shell'em
            System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Navigating to main shell");
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    // Zast¹p aktualne okno nowym Shell'em
                    var app = Application.Current;
                    if (app?.MainPage != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Replacing MainPage with AppShell");
                        app.MainPage = new AppShell();
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[SetupWizardViewModel] Application.Current.MainPage is null");
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
            
            // Fallback navigation - try to replace MainPage
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