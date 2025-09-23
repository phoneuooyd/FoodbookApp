using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly LocalizationResourceManager _locManager;
    private readonly IPreferencesService _preferencesService;
    private readonly IThemeService _themeService;
    private readonly IFontService _fontService;
    private readonly IDatabaseService _databaseService;

    // Guard to prevent re-entrancy and UI loops when changing culture
    private bool _isChangingCulture;

    // Expose if current build is DEBUG so UI can hide dev-only actions in Release
#if DEBUG
    public bool IsDebugBuild => true;
#else
    public bool IsDebugBuild => false;
#endif

    public ObservableCollection<string> SupportedCultures { get; }
    public ObservableCollection<Foodbook.Models.AppTheme> SupportedThemes { get; }
    public ObservableCollection<AppColorTheme> SupportedColorThemes { get; }
    public ObservableCollection<AppFontFamily> SupportedFontFamilies { get; }
    public ObservableCollection<AppFontSize> SupportedFontSizes { get; }

    private string _selectedCulture;
    public string SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (_selectedCulture == value || _isChangingCulture) return;

            _isChangingCulture = true;
            try
            {
                // Validate culture value before applying
                var safeValue = value;
                if (string.IsNullOrWhiteSpace(safeValue))
                {
                    safeValue = _preferencesService.GetSavedLanguage();
                    if (string.IsNullOrWhiteSpace(safeValue))
                    {
                        var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                        safeValue = _preferencesService.GetSupportedCultures().Contains(systemCulture)
                            ? systemCulture
                            : _preferencesService.GetSupportedCultures().First();
                    }
                }
                else if (!_preferencesService.GetSupportedCultures().Contains(safeValue))
                {
                    // If not directly supported, try neutral (e.g., "pl" from "pl-PL")
                    var neutral = new System.Globalization.CultureInfo(safeValue).TwoLetterISOLanguageName;
                    if (_preferencesService.GetSupportedCultures().Contains(neutral))
                    {
                        safeValue = neutral;
                    }
                    else
                    {
                        safeValue = _preferencesService.GetSupportedCultures().First();
                    }
                }

                _selectedCulture = safeValue;
                OnPropertyChanged(nameof(SelectedCulture));

                // Change app culture via resource manager (on UI thread)
                MainThread.BeginInvokeOnMainThread(() => _locManager.SetCulture(safeValue));

                // Save preference
                _preferencesService.SaveLanguage(safeValue);

                // Do NOT repopulate ItemsSource collections here – it causes the picker to rebind
                // and can retrigger SelectedItem changes, leading to re-entrancy and freezes.
                // If display text depends on resources, bindings will refresh via CultureChanged.

                // Notify dependent properties so their display can update if necessary
                OnPropertyChanged(nameof(SelectedTheme));
                OnPropertyChanged(nameof(SelectedColorTheme));
                OnPropertyChanged(nameof(SelectedFontFamily));
                OnPropertyChanged(nameof(SelectedFontSize));
            }
            finally
            {
                _isChangingCulture = false;
            }
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

    private AppColorTheme _selectedColorTheme;
    public AppColorTheme SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (_selectedColorTheme == value) return;
            _selectedColorTheme = value;
            OnPropertyChanged(nameof(SelectedColorTheme));
            
            // Apply the color theme immediately
            _themeService.SetColorTheme(value);
            
            // Save the selected color theme to preferences
            _preferencesService.SaveColorTheme(value);
        }
    }

    // NEW: Colorful background property
    private bool _isColorfulBackgroundEnabled;
    public bool IsColorfulBackgroundEnabled
    {
        get => _isColorfulBackgroundEnabled;
        set
        {
            if (_isColorfulBackgroundEnabled == value) return;
            _isColorfulBackgroundEnabled = value;
            OnPropertyChanged(nameof(IsColorfulBackgroundEnabled));
            
            // Apply the colorful background setting immediately
            _themeService.SetColorfulBackground(value);
            
            // Save the colorful background preference
            _preferencesService.SaveColorfulBackground(value);
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Colorful background changed to: {value}");
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

    public SettingsViewModel(LocalizationResourceManager locManager, IPreferencesService preferencesService, IThemeService themeService, IFontService fontService, IDatabaseService databaseService)
    {
        _locManager = locManager;
        _preferencesService = preferencesService;
        _themeService = themeService;
        _fontService = fontService;
        _databaseService = databaseService;
        
        // Initialize supported cultures from preferences service
        SupportedCultures = new ObservableCollection<string>(_preferencesService.GetSupportedCultures());
        
        // Initialize supported themes
        SupportedThemes = new ObservableCollection<Foodbook.Models.AppTheme> 
        { 
            Foodbook.Models.AppTheme.System, 
            Foodbook.Models.AppTheme.Light, 
            Foodbook.Models.AppTheme.Dark 
        };
        
        // Picker order requested by user
        SupportedColorThemes = new ObservableCollection<AppColorTheme>
        {
            AppColorTheme.Default,
            AppColorTheme.Nature,
            AppColorTheme.Forest,
            AppColorTheme.Autumn,
            AppColorTheme.Warm,
            AppColorTheme.Sunset,
            AppColorTheme.Vibrant,
            AppColorTheme.Monochrome,
            AppColorTheme.Navy,
            AppColorTheme.Mint,
            AppColorTheme.Sky,
            AppColorTheme.Bubblegum
        };
        
        // Initialize supported font families
        SupportedFontFamilies = new ObservableCollection<AppFontFamily>(_fontService.GetAvailableFontFamilies());
        
        // Initialize supported font sizes
        SupportedFontSizes = new ObservableCollection<AppFontSize>(_fontService.GetAvailableFontSizes());
        
        // Load the saved preferences
        _selectedCulture = LoadSelectedCulture();
        _selectedTheme = LoadSelectedTheme();
        _selectedColorTheme = LoadSelectedColorTheme();
        _isColorfulBackgroundEnabled = LoadColorfulBackgroundSetting(); // NEW
        LoadSelectedFontSettings();
        
        // Set the culture without triggering the setter to avoid recursive calls
        _locManager.SetCulture(_selectedCulture);
        
        // Apply saved theme and color theme
        _themeService.SetTheme(_selectedTheme);
        _themeService.SetColorTheme(_selectedColorTheme);
        _themeService.SetColorfulBackground(_isColorfulBackgroundEnabled); // NEW: Apply saved colorful background setting
        
        // Apply saved font settings
        _fontService.LoadSavedSettings();

        // Initialize commands
        MigrateDatabaseCommand = new Command(async () => await MigrateDatabaseAsync(), () => CanExecuteMigration);
        ResetDatabaseCommand = new Command(async () => await ResetDatabaseAsync(), () => CanExecuteMigration);
        
        System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Initialized with color theme and colorful background support");
    }

    private void RefreshCollectionsForLocalization()
    {
        // NOTE: This method is intentionally left intact, but it is no longer invoked from SelectedCulture setter
        // to avoid UI re-entrancy/freeze scenarios. Keep for future use if needed during app startup only.

        // Cultures
        SupportedCultures.Clear();
        foreach (var c in _preferencesService.GetSupportedCultures())
            SupportedCultures.Add(c);

        // Themes
        var themes = new[] { Foodbook.Models.AppTheme.System, Foodbook.Models.AppTheme.Light, Foodbook.Models.AppTheme.Dark };
        SupportedThemes.Clear();
        foreach (var t in themes)
            SupportedThemes.Add(t);

        // Keep the same explicit order as the enum requested by the user
        var colorThemes = new[]
        {
            AppColorTheme.Default,
            AppColorTheme.Nature,
            AppColorTheme.Forest,
            AppColorTheme.Autumn,
            AppColorTheme.Warm,
            AppColorTheme.Sunset,
            AppColorTheme.Vibrant,
            AppColorTheme.Monochrome,
            AppColorTheme.Navy,
            AppColorTheme.Mint,
            AppColorTheme.Sky,
            AppColorTheme.Bubblegum
        };
        SupportedColorThemes.Clear();
        foreach (var ct in colorThemes)
            SupportedColorThemes.Add(ct);

        // Fonts
        SupportedFontFamilies.Clear();
        foreach (var ff in _fontService.GetAvailableFontFamilies())
            SupportedFontFamilies.Add(ff);

        SupportedFontSizes.Clear();
        foreach (var fs in _fontService.GetAvailableFontSizes())
            SupportedFontSizes.Add(fs);
    }

    private async Task MigrateDatabaseAsync()
    {
        try
        {
            IsMigrationInProgress = true;
            MigrationStatus = "Wykonywanie migracji bazy danych...";
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database migration");
            
            var success = await _databaseService.MigrateDatabaseAsync();

            var page = Application.Current?.MainPage;
            if (success)
            {
                MigrationStatus = "Migracja zakoñczona pomyœlnie!";
                if (page != null)
                {
                    await page.DisplayAlert(
                        "Sukces", 
                        "Migracja bazy danych zosta³a wykonana pomyœlnie.", 
                        "OK");
                }
            }
            else
            {
                MigrationStatus = "Migracja nieudana.";
                if (page != null)
                {
                    await page.DisplayAlert(
                        "B³¹d", 
                        "Nie uda³o siê wykonaæ migracji bazy danych. SprawdŸ logi aplikacji.", 
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Migration error: {ex.Message}");
            MigrationStatus = $"B³¹d migracji: {ex.Message}";
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert(
                    "B³¹d", 
                    $"Wyst¹pi³ b³¹d podczas migracji: {ex.Message}", 
                    "OK");
            }
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
            var page = Application.Current?.MainPage;
            if (page == null) return;

            bool confirm = await page.DisplayAlert(
                "Resetuj bazê danych", 
                "Czy na pewno chcesz usun¹æ wszystkie dane? Ta operacja jest nieodwracalna.\n\nWszystkie przepisy, plany i listy zakupów zostan¹ utracone.", 
                "Tak, resetuj", "Anuluj");
                
            if (!confirm) return;

            IsMigrationInProgress = true;
            MigrationStatus = "Resetowanie bazy danych...";
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database reset");
            
            var success = await _databaseService.ResetDatabaseAsync();
            
            if (success)
            {
                MigrationStatus = "Baza danych zosta³a zresetowana!";
                await page.DisplayAlert(
                    "Sukces", 
                    "Baza danych zosta³a zresetowana. Aplikacja zostanie zamkniêta - uruchom j¹ ponownie.", 
                    "OK");
                
                // Close application after reset
                Application.Current?.Quit();
            }
            else
            {
                MigrationStatus = "Reset nieudany.";
                await page.DisplayAlert(
                    "B³¹d", 
                    "Nie uda³o siê zresetowaæ bazy danych. SprawdŸ logi aplikacji.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Reset error: {ex.Message}");
            MigrationStatus = $"B³¹d resetu: {ex.Message}";
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert(
                    "B³¹d", 
                    $"Wyst¹pi³ b³¹d podczas resetowania: {ex.Message}", 
                    "OK");
            }
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

    private AppColorTheme LoadSelectedColorTheme()
    {
        try
        {
            var savedColorTheme = _preferencesService.GetSavedColorTheme();
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded saved color theme preference: {savedColorTheme}");
            return savedColorTheme;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load color theme preference: {ex.Message}");
            return AppColorTheme.Default; // Default to default color theme
        }
    }

    // NEW: Load colorful background setting
    private bool LoadColorfulBackgroundSetting()
    {
        try
        {
            var isEnabled = _preferencesService.GetIsColorfulBackgroundEnabled();
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded colorful background preference: {isEnabled}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load colorful background preference: {ex.Message}");
            return false; // Default to false (gray backgrounds)
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
