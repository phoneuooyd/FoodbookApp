using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Services;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;

namespace Foodbook.ViewModels;

public partial class SettingsViewModel : INotifyPropertyChanged
{
    private readonly LocalizationResourceManager _locManager;
    private readonly IPreferencesService _preferencesService;
    private readonly IThemeService _themeService;
    private readonly IFontService _fontService;
    private readonly IDatabaseService _databaseService;
    private readonly IDeduplicationService _deduplicationService;
    private readonly IFeatureAccessService _featureAccessService;

    // Tabs management
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex == value) return;
            _selectedTabIndex = value;
            OnPropertyChanged(nameof(SelectedTabIndex));
            OnPropertyChanged(nameof(IsLanguageTabSelected));
            OnPropertyChanged(nameof(IsAppearanceTabSelected));
            OnPropertyChanged(nameof(IsDataTabSelected));
        }
    }

    public bool IsLanguageTabSelected => SelectedTabIndex == 0;
    public bool IsAppearanceTabSelected => SelectedTabIndex == 1;
    public bool IsDataTabSelected => SelectedTabIndex == 2;

    public ICommand SelectTabCommand { get; }

    // Guard to prevent re-entrancy and UI loops when changing culture
    private bool _isChangingCulture;
    // Guard to avoid recursion when toggling mutually exclusive background options
    private bool _isUpdatingBackgroundOptions;

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
            OnPropertyChanged(nameof(IsSystemThemeSelected));
            OnPropertyChanged(nameof(IsLightThemeSelected));
            OnPropertyChanged(nameof(IsDarkThemeSelected));
            
            // Apply the theme immediately
            _themeService.SetTheme(value);
            
            // Save the selected theme to preferences
            _preferencesService.SaveTheme(value);
        }
    }

    public bool IsSystemThemeSelected => SelectedTheme == Foodbook.Models.AppTheme.System;
    public bool IsLightThemeSelected => SelectedTheme == Foodbook.Models.AppTheme.Light;
    public bool IsDarkThemeSelected => SelectedTheme == Foodbook.Models.AppTheme.Dark;

    public ICommand SelectSystemThemeCommand { get; }
    public ICommand SelectLightThemeCommand { get; }
    public ICommand SelectDarkThemeCommand { get; }

    private AppColorTheme _selectedColorTheme;
    public AppColorTheme SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (_selectedColorTheme == value) return;
            _selectedColorTheme = value;
            OnPropertyChanged(nameof(SelectedColorTheme));
            
            // Update wallpaper availability for this color theme
            IsWallpaperAvailable = _themeService.IsWallpaperAvailableFor(_selectedColorTheme);
            
            // If wallpaper not available, ensure it's turned off in UI and service
            if (!IsWallpaperAvailable && IsWallpaperBackgroundEnabled)
            {
                if (!_isUpdatingBackgroundOptions)
                {
                    try
                    {
                        _isUpdatingBackgroundOptions = true;
                        IsWallpaperBackgroundEnabled = false; // will call service and save pref
                    }
                    finally
                    {
                        _isUpdatingBackgroundOptions = false;
                    }
                }
            }
            
            // Apply the color theme immediately
            _themeService.SetColorTheme(value);
            
            // Save the selected color theme to preferences
            _preferencesService.SaveColorTheme(value);
        }
    }

    // NEW: Availability of wallpaper option for selected color theme
    private bool _isWallpaperAvailable;
    public bool IsWallpaperAvailable
    {
        get => _isWallpaperAvailable;
        private set
        {
            if (_isWallpaperAvailable == value) return;
            _isWallpaperAvailable = value;
            OnPropertyChanged(nameof(IsWallpaperAvailable));
        }
    }

    private bool _isPremiumUser;

    private bool _canUseWallpaperBackground;
    public bool CanUseWallpaperBackground
    {
        get => _canUseWallpaperBackground;
        private set
        {
            if (_canUseWallpaperBackground == value) return;
            _canUseWallpaperBackground = value;
            OnPropertyChanged(nameof(CanUseWallpaperBackground));
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

            // Ensure mutual exclusion with wallpaper option
            if (value && IsWallpaperBackgroundEnabled)
            {
                if (_isUpdatingBackgroundOptions) return;
                try
                {
                    _isUpdatingBackgroundOptions = true;
                    IsWallpaperBackgroundEnabled = false;
                }
                finally
                {
                    _isUpdatingBackgroundOptions = false;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Colorful background changed to: {value}");
        }
    }

    // NEW: Wallpaper background property
    private bool _isWallpaperBackgroundEnabled;
    public bool IsWallpaperBackgroundEnabled
    {
        get => _isWallpaperBackgroundEnabled;
        set
        {
            if (_isWallpaperBackgroundEnabled == value) return;

            if (value && !CanUseWallpaperBackground)
            {
                _isWallpaperBackgroundEnabled = false;
                OnPropertyChanged(nameof(IsWallpaperBackgroundEnabled));
                _themeService.EnableWallpaperBackground(false);
                _preferencesService.SaveWallpaperEnabled(false);
                ShowPremiumRequiredWallpaperMessage();
                return;
            }

            // Block enabling when not available for current color theme
            if (value && !_themeService.IsWallpaperAvailableFor(_selectedColorTheme))
            {
                // keep disabled
                _isWallpaperBackgroundEnabled = false;
                OnPropertyChanged(nameof(IsWallpaperBackgroundEnabled));
                return;
            }

            _isWallpaperBackgroundEnabled = value;
            OnPropertyChanged(nameof(IsWallpaperBackgroundEnabled));

            // Apply wallpaper background immediately
            _themeService.EnableWallpaperBackground(value);
            
            // Save preference
            _preferencesService.SaveWallpaperEnabled(value);

            // Ensure mutual exclusion with colorful background option
            if (value && IsColorfulBackgroundEnabled)
            {
                if (_isUpdatingBackgroundOptions) return;
                try
                {
                    _isUpdatingBackgroundOptions = true;
                    IsColorfulBackgroundEnabled = false;
                }
                finally
                {
                    _isUpdatingBackgroundOptions = false;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Wallpaper background changed to: {value}");
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
    public ICommand FactoryResetCommand { get; }
    public ICommand DeduplicateIngredientsCommand { get; }

    public SettingsViewModel(LocalizationResourceManager locManager, IPreferencesService preferencesService, IThemeService themeService, IFontService fontService, IDatabaseService databaseService, IDeduplicationService deduplicationService, IFeatureAccessService featureAccessService)
    {
        _locManager = locManager;
        _preferencesService = preferencesService;
        _themeService = themeService;
        _fontService = fontService;
        _databaseService = databaseService;
        _deduplicationService = deduplicationService;
        _featureAccessService = featureAccessService;
        _isPremiumUser = string.Equals(_preferencesService.GetPlanChoice(), "Premium", StringComparison.OrdinalIgnoreCase);
        _canUseWallpaperBackground = _isPremiumUser;
        
        // Tabs
        SelectTabCommand = new Command<object>(p =>
        {
            try
            {
                int index = 0;
                if (p is int i) index = i;
                else if (p is string s && int.TryParse(s, out var parsed)) index = parsed;
                if (index < 0 || index > 2) index = 0;
                SelectedTabIndex = index;
            }
            catch { SelectedTabIndex = 0; }
        });
        SelectedTabIndex = 0; // default: Language
        
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
        _isColorfulBackgroundEnabled = LoadColorfulBackgroundSetting();
        _isWallpaperBackgroundEnabled = LoadWallpaperBackgroundSetting();
        
        // Initialize wallpaper availability for initial color theme
        _isWallpaperAvailable = _themeService.IsWallpaperAvailableFor(_selectedColorTheme);
        
        LoadSelectedFontSettings();
        
        // Set the culture without triggering the setter to avoid recursive calls
        _locManager.SetCulture(_selectedCulture);
        
        // Apply saved theme and color theme
        _themeService.SetTheme(_selectedTheme);
        _themeService.SetColorTheme(_selectedColorTheme);
        _themeService.SetColorfulBackground(_isColorfulBackgroundEnabled);
        // If initial theme does not support wallpapers, ensure it's off
        if ((!_isWallpaperAvailable || !CanUseWallpaperBackground) && _isWallpaperBackgroundEnabled)
        {
            _isWallpaperBackgroundEnabled = false;
            _preferencesService.SaveWallpaperEnabled(false);
        }
        _themeService.EnableWallpaperBackground(_isWallpaperBackgroundEnabled);
        
        // Apply saved font settings
        _fontService.LoadSavedSettings();

        // Initialize commands
        MigrateDatabaseCommand = new Command(async () => await MigrateDatabaseAsync(), () => CanExecuteMigration);
        ResetDatabaseCommand = new Command(async () => await ResetDatabaseAsync(), () => CanExecuteMigration);
        FactoryResetCommand = new Command(async () => await FactoryResetAsync(), () => CanExecuteMigration);
        DeduplicateIngredientsCommand = new Command(async () => await DeduplicateIngredientsAsync(), () => CanExecuteMigration);

        SelectSystemThemeCommand = new Command(() => SelectedTheme = Foodbook.Models.AppTheme.System);
        SelectLightThemeCommand = new Command(() => SelectedTheme = Foodbook.Models.AppTheme.Light);
        SelectDarkThemeCommand = new Command(() => SelectedTheme = Foodbook.Models.AppTheme.Dark);
        
        System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Initialized with color theme and colorful/wallpaper background support");

        // Initialize labels feature (in partial)
        InitializeLabelsFeature();

        _ = RefreshWallpaperPremiumAccessAsync();
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
            MigrationStatus = S("DatabaseMigrationInProgress", "Running database migration...");
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database migration");
            
            var success = await _databaseService.MigrateDatabaseAsync();

            var page = Application.Current?.MainPage;
            if (success)
            {
                MigrationStatus = S("DatabaseMigrationSuccessStatus", "Database migration completed successfully!");
                if (page != null)
                {
                    await page.DisplayAlert(
                        S("DatabaseMigrationSuccessTitle", "Success"),
                        S("DatabaseMigrationSuccessMessage", "Database migration completed successfully."),
                        ButtonResources.OK);
                }
            }
            else
            {
                MigrationStatus = S("DatabaseMigrationFailedStatus", "Migration failed.");
                if (page != null)
                {
                    await page.DisplayAlert(
                        S("DatabaseMigrationFailedTitle", "Error"),
                        S("DatabaseMigrationFailedMessage", "Could not execute database migration. Check application logs."),
                        ButtonResources.OK);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Migration error: {ex.Message}");
            MigrationStatus = string.Format(S("DatabaseMigrationErrorStatusFormat", "Migration error: {0}"), ex.Message);
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert(
                    S("DatabaseMigrationFailedTitle", "Error"),
                    string.Format(S("DatabaseMigrationErrorMessageFormat", "An error occurred during migration: {0}"), ex.Message),
                    ButtonResources.OK);
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
                S("DatabaseResetConfirmTitle", "Reset database"),
                string.Format(
                    S("DatabaseResetConfirmMessage", "Are you sure you want to delete all data? This operation is irreversible.{0}{0}All recipes, plans and shopping lists will be lost."),
                    Environment.NewLine),
                S("DatabaseResetConfirmAccept", "Yes, reset"),
                ButtonResources.Cancel);
                
            if (!confirm) return;

            IsMigrationInProgress = true;
            MigrationStatus = S("DatabaseResetInProgress", "Resetting database...");
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database reset");
            
            var success = await _databaseService.ResetDatabaseAsync();
            
            if (success)
            {
                MigrationStatus = S("DatabaseResetSuccessStatus", "Database has been reset!");
                await page.DisplayAlert(
                    S("DatabaseResetSuccessTitle", "Success"),
                    S("DatabaseResetSuccessMessage", "Database has been reset. The app will close - restart it."),
                    ButtonResources.OK);
                
                // Close application after reset
                Application.Current?.Quit();
            }
            else
            {
                MigrationStatus = S("DatabaseResetFailedStatus", "Reset failed.");
                await page.DisplayAlert(
                    S("DatabaseResetFailedTitle", "Error"),
                    S("DatabaseResetFailedMessage", "Could not reset database. Check application logs."),
                    ButtonResources.OK);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Reset error: {ex.Message}");
            MigrationStatus = string.Format(S("DatabaseResetErrorStatusFormat", "Reset error: {0}"), ex.Message);
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert(
                    S("DatabaseResetFailedTitle", "Error"),
                    string.Format(S("DatabaseResetErrorMessageFormat", "An error occurred while resetting: {0}"), ex.Message),
                    ButtonResources.OK);
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

    private async Task FactoryResetAsync()
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null) return;

            // Localized strings via ResourceManager to avoid relying on generated properties
            var rm = SettingsPageResources.ResourceManager;
            string L(string key, string fallback) => rm.GetString(key) ?? fallback;

            bool confirm = await page.DisplayAlert(
                L("FactoryResetConfirmTitle", "Factory reset"),
                L("FactoryResetConfirmMessage", "This operation will reset the app to its initial state. Continue?"),
                L("FactoryResetConfirmOk", "Yes, reset"),
                L("FactoryResetConfirmCancel", "Cancel"));

            if (!confirm) return;

            IsMigrationInProgress = true;
            MigrationStatus = L("FactoryResetInProgress", "Restoring factory settings...");

            _preferencesService.ResetAllToDefaults();
            var dbOk = await _databaseService.ResetDatabaseAsync();

            if (dbOk)
            {
                MigrationStatus = L("FactoryResetDone", "Factory settings restored");
                await page.DisplayAlert(
                    L("FactoryResetSuccessTitle", "Success"),
                    L("FactoryResetSuccessMessage", "Factory settings were restored. The app will close. Launch it again to start onboarding."),
                    ButtonResources.OK);

                Application.Current?.Quit();
            }
            else
            {
                MigrationStatus = L("FactoryResetFailedShort", "Factory reset failed");
                await page.DisplayAlert(
                    L("FactoryResetFailedTitle", "Error"),
                    L("FactoryResetFailedMessage", "Could not restore factory settings. Check app logs."),
                    ButtonResources.OK);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Factory reset error: {ex.Message}");
            var rm = SettingsPageResources.ResourceManager;
            string L(string key, string fallback) => rm.GetString(key) ?? fallback;

                MigrationStatus = L("FactoryResetErrorShort", "Factory reset error");
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert(
                    L("FactoryResetErrorTitle", "Error"),
                    string.Format(L("FactoryResetErrorMessage", "An error occurred while restoring factory settings: {0}"), ex.Message),
                    ButtonResources.OK);
            }
        }
        finally
        {
            IsMigrationInProgress = false;
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() => MigrationStatus = string.Empty);
            });
        }
    }

    private async Task DeduplicateIngredientsAsync()
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null) return;

            bool confirm = await page.DisplayAlert(
                S("DeduplicateConfirmTitle", "Remove duplicate ingredients"),
                string.Format(
                    S("DeduplicateConfirmMessage", "Are you sure you want to remove duplicated base ingredients? (comparison: name + macros).{0}{0}This operation is irreversible."),
                    Environment.NewLine),
                S("DeduplicateConfirmAccept", "Yes, remove"),
                ButtonResources.Cancel);

            if (!confirm) return;

            IsMigrationInProgress = true;
            MigrationStatus = S("DeduplicateInProgress", "Removing duplicate ingredients...");

            using var scope = FoodbookApp.MauiProgram.ServiceProvider?.CreateScope();
            var db = scope?.ServiceProvider.GetService<Foodbook.Data.AppDbContext>();
            if (db == null)
            {
                MigrationStatus = S("DeduplicateDbUnavailableStatus", "Error: no access to database");
                return;
            }

            var removed = await _deduplicationService.DeduplicateLocalIngredientsAndQueueDeletesAsync(db);

            // Notify UI/pages that ingredient catalog changed
            try
            {
                await Foodbook.Services.AppEvents.RaiseIngredientsChangedAsync();
            }
            catch { }

            MigrationStatus = removed > 0
                ? string.Format(S("DeduplicateRemovedStatusFormat", "Duplicates removed: {0}"), removed)
                : S("DeduplicateNoDuplicatesStatus", "No duplicates to remove");

            await page.DisplayAlert(S("DeduplicateDoneTitle", "Done"), MigrationStatus, ButtonResources.OK);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] DeduplicateIngredientsAsync error: {ex.Message}");
            MigrationStatus = string.Format(S("DeduplicateErrorStatusFormat", "Error: {0}"), ex.Message);

            var page = Application.Current?.MainPage;
            if (page != null)
                await page.DisplayAlert(S("DeduplicateErrorTitle", "Error"), MigrationStatus, ButtonResources.OK);
        }
        finally
        {
            IsMigrationInProgress = false;
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

    // NEW: Load wallpaper background setting
    private bool LoadWallpaperBackgroundSetting()
    {
        try
        {
            var isEnabled = _preferencesService.GetIsWallpaperEnabled();
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded wallpaper background preference: {isEnabled}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load wallpaper background preference: {ex.Message}");
            return false;
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

    private async Task RefreshWallpaperPremiumAccessAsync()
    {
        try
        {
            var canUsePremiumFeature = await _featureAccessService.CanUsePremiumFeatureAsync(PremiumFeature.AutoPlanner);
            var isPremiumFromPreferences = string.Equals(_preferencesService.GetPlanChoice(), "Premium", StringComparison.OrdinalIgnoreCase);
            _isPremiumUser = isPremiumFromPreferences || canUsePremiumFeature;
            CanUseWallpaperBackground = canUsePremiumFeature || _isPremiumUser;

            if (!CanUseWallpaperBackground && IsWallpaperBackgroundEnabled)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _isWallpaperBackgroundEnabled = false;
                    OnPropertyChanged(nameof(IsWallpaperBackgroundEnabled));
                    _themeService.EnableWallpaperBackground(false);
                    _preferencesService.SaveWallpaperEnabled(false);
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] RefreshWallpaperPremiumAccessAsync error: {ex.Message}");
            CanUseWallpaperBackground = _isPremiumUser;
        }
    }

    private static void ShowPremiumRequiredWallpaperMessage()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page != null)
            {
                await page.DisplayAlert(
                    S("PremiumRequiredTitle", "Premium"),
                    S("PremiumRequiredMessage", "This option is available only for Premium users."),
                    ButtonResources.OK);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private static string S(string key, string fallback)
        => SettingsPageResources.ResourceManager.GetString(key, SettingsPageResources.Culture) ?? fallback;

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Partial hook to initialize labels feature
    partial void InitializeLabelsFeature();
}
