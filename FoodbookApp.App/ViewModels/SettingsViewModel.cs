using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Localization;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly LocalizationResourceManager _locManager;
    private readonly IPreferencesService _preferencesService;
    private readonly IThemeService _themeService;
    private readonly IFontService _fontService;

    public ObservableCollection<string> SupportedCultures { get; }
    public ObservableCollection<Foodbook.Models.AppTheme> SupportedThemes { get; }
    public ObservableCollection<AppFontFamily> SupportedFontFamilies { get; }
    public ObservableCollection<AppFontSize> SupportedFontSizes { get; }

    private string _selectedCulture;
    public string SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (_selectedCulture == value) return;
            _selectedCulture = value;
            OnPropertyChanged(nameof(SelectedCulture));
            _locManager.SetCulture(value);
            
            // Save the selected culture to preferences
            _preferencesService.SaveLanguage(value);
        }
    }

    private Foodbook.Models.AppTheme _selectedTheme;
    public Foodbook.Models.AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (_selectedTheme == value) return;
            _selectedTheme = value;
            OnPropertyChanged(nameof(SelectedTheme));
            
            // Apply the theme immediately
            _themeService.SetTheme(value);
            
            // Save the selected theme to preferences
            _preferencesService.SaveTheme(value);
        }
    }

    private AppFontFamily _selectedFontFamily;
    public AppFontFamily SelectedFontFamily
    {
        get => _selectedFontFamily;
        set
        {
            if (_selectedFontFamily == value) return;
            _selectedFontFamily = value;
            OnPropertyChanged(nameof(SelectedFontFamily));
            
            // Apply the font family immediately
            _fontService.SetFontFamily(value);
        }
    }

    private AppFontSize _selectedFontSize;
    public AppFontSize SelectedFontSize
    {
        get => _selectedFontSize;
        set
        {
            if (_selectedFontSize == value) return;
            _selectedFontSize = value;
            OnPropertyChanged(nameof(SelectedFontSize));
            
            // Apply the font size immediately
            _fontService.SetFontSize(value);
        }
    }

    private bool _isMigrationInProgress;
    public bool IsMigrationInProgress
    {
        get => _isMigrationInProgress;
        set
        {
            if (_isMigrationInProgress == value) return;
            _isMigrationInProgress = value;
            OnPropertyChanged(nameof(IsMigrationInProgress));
            OnPropertyChanged(nameof(CanExecuteMigration));
        }
    }

    private string _migrationStatus = string.Empty;
    public string MigrationStatus
    {
        get => _migrationStatus;
        set
        {
            if (_migrationStatus == value) return;
            _migrationStatus = value;
            OnPropertyChanged(nameof(MigrationStatus));
        }
    }

    public bool CanExecuteMigration => !IsMigrationInProgress;

    // Commands for database operations
    public ICommand MigrateDatabaseCommand { get; }
    public ICommand ResetDatabaseCommand { get; }

    public SettingsViewModel(LocalizationResourceManager locManager, IPreferencesService preferencesService, IThemeService themeService, IFontService fontService)
    {
        _locManager = locManager;
        _preferencesService = preferencesService;
        _themeService = themeService;
        _fontService = fontService;
        
        // Initialize supported cultures from preferences service
        SupportedCultures = new ObservableCollection<string>(_preferencesService.GetSupportedCultures());
        
        // Initialize supported themes
        SupportedThemes = new ObservableCollection<Foodbook.Models.AppTheme> 
        { 
            Foodbook.Models.AppTheme.System, 
            Foodbook.Models.AppTheme.Light, 
            Foodbook.Models.AppTheme.Dark 
        };
        
        // Initialize supported font families
        SupportedFontFamilies = new ObservableCollection<AppFontFamily>(_fontService.GetAvailableFontFamilies());
        
        // Initialize supported font sizes
        SupportedFontSizes = new ObservableCollection<AppFontSize>(_fontService.GetAvailableFontSizes());
        
        // Load the saved preferences
        _selectedCulture = LoadSelectedCulture();
        _selectedTheme = LoadSelectedTheme();
        LoadSelectedFontSettings();
        
        // Set the culture without triggering the setter to avoid recursive calls
        _locManager.SetCulture(_selectedCulture);
        
        // Apply saved theme
        _themeService.SetTheme(_selectedTheme);
        
        // Apply saved font settings
        _fontService.LoadSavedSettings();

        // Initialize commands
        MigrateDatabaseCommand = new Command(async () => await MigrateDatabaseAsync(), () => CanExecuteMigration);
        ResetDatabaseCommand = new Command(async () => await ResetDatabaseAsync(), () => CanExecuteMigration);
        
        System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Initialized with font support");
    }

    private async Task MigrateDatabaseAsync()
    {
        try
        {
            IsMigrationInProgress = true;
            MigrationStatus = "Wykonywanie migracji bazy danych...";
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database migration");
            
            var success = await FoodbookApp.MauiProgram.MigrateDatabaseAsync();
            
            if (success)
            {
                MigrationStatus = "Migracja zakoñczona pomyœlnie!";
                await Application.Current?.MainPage?.DisplayAlert(
                    "Sukces", 
                    "Migracja bazy danych zosta³a wykonana pomyœlnie.", 
                    "OK");
            }
            else
            {
                MigrationStatus = "Migracja nieudana.";
                await Application.Current?.MainPage?.DisplayAlert(
                    "B³¹d", 
                    "Nie uda³o siê wykonaæ migracji bazy danych. SprawdŸ logi aplikacji.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Migration error: {ex.Message}");
            MigrationStatus = $"B³¹d migracji: {ex.Message}";
            await Application.Current?.MainPage?.DisplayAlert(
                "B³¹d", 
                $"Wyst¹pi³ b³¹d podczas migracji: {ex.Message}", 
                "OK");
        }
        finally
        {
            IsMigrationInProgress = false;
            
            // Clear status after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() => MigrationStatus = string.Empty);
            });
        }
    }

    private async Task ResetDatabaseAsync()
    {
        try
        {
            bool confirm = await Application.Current?.MainPage?.DisplayAlert(
                "Resetuj bazê danych", 
                "Czy na pewno chcesz usun¹æ wszystkie dane? Ta operacja jest nieodwracalna.\n\nWszystkie przepisy, plany i listy zakupów zostan¹ utracone.", 
                "Tak, resetuj", "Anuluj") == true;
                
            if (!confirm) return;

            IsMigrationInProgress = true;
            MigrationStatus = "Resetowanie bazy danych...";
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database reset");
            
            var success = await FoodbookApp.MauiProgram.ResetDatabaseAsync();
            
            if (success)
            {
                MigrationStatus = "Baza danych zosta³a zresetowana!";
                await Application.Current?.MainPage?.DisplayAlert(
                    "Sukces", 
                    "Baza danych zosta³a zresetowana. Aplikacja zostanie zamkniêta - uruchom j¹ ponownie.", 
                    "OK");
                
                // Close application after reset
                Application.Current?.Quit();
            }
            else
            {
                MigrationStatus = "Reset nieudany.";
                await Application.Current?.MainPage?.DisplayAlert(
                    "B³¹d", 
                    "Nie uda³o siê zresetowaæ bazy danych. SprawdŸ logi aplikacji.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Reset error: {ex.Message}");
            MigrationStatus = $"B³¹d resetu: {ex.Message}";
            await Application.Current?.MainPage?.DisplayAlert(
                "B³¹d", 
                $"Wyst¹pi³ b³¹d podczas resetowania: {ex.Message}", 
                "OK");
        }
        finally
        {
            IsMigrationInProgress = false;
            
            // Clear status after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() => MigrationStatus = string.Empty);
            });
        }
    }

    private string LoadSelectedCulture()
    {
        try
        {
            var savedCulture = _preferencesService.GetSavedLanguage();
            
            if (!string.IsNullOrEmpty(savedCulture))
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded saved culture preference: {savedCulture}");
                return savedCulture;
            }
            
            // Fall back to system culture if no preference saved
            var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var supportedCultures = _preferencesService.GetSupportedCultures();
            var fallbackCulture = supportedCultures.Contains(systemCulture) ? systemCulture : supportedCultures[0];
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] No saved preference, using fallback: {fallbackCulture}");
            return fallbackCulture;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load culture preference: {ex.Message}");
            return _preferencesService.GetSupportedCultures()[0]; // Default to first supported culture
        }
    }

    private Foodbook.Models.AppTheme LoadSelectedTheme()
    {
        try
        {
            var savedTheme = _preferencesService.GetSavedTheme();
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded saved theme preference: {savedTheme}");
            return savedTheme;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load theme preference: {ex.Message}");
            return Foodbook.Models.AppTheme.System; // Default to system theme
        }
    }

    private void LoadSelectedFontSettings()
    {
        try
        {
            var savedFontSettings = _preferencesService.GetFontSettings();
            _selectedFontFamily = savedFontSettings.FontFamily;
            _selectedFontSize = savedFontSettings.FontSize;
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded saved font settings: Family={savedFontSettings.FontFamily}, Size={savedFontSettings.FontSize}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load font settings: {ex.Message}");
            _selectedFontFamily = AppFontFamily.Default;
            _selectedFontSize = AppFontSize.Default;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
