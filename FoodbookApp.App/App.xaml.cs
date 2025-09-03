using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Services;
using Foodbook.Models;

namespace FoodbookApp
{
    public partial class App : Application
    {
        private readonly ILocalizationService _localizationService;
        private readonly IPreferencesService _preferencesService;
        private readonly IThemeService _themeService;
        private readonly IFontService _fontService;

        public App(ILocalizationService localizationService, IPreferencesService preferencesService, IThemeService themeService, IFontService fontService)
        {
            try
            {
                // Initialize XAML components first
                InitializeComponent();
                
                _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
                _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
                _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
                _fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));
                
                // Load saved language preference or use system default
                var savedCulture = LoadSavedCulture();
                _localizationService.SetCulture(savedCulture);
                
                // Load and apply saved theme
                var savedTheme = LoadSavedTheme();
                _themeService.SetTheme(savedTheme);
                
                // Load and apply saved color theme
                var savedColorTheme = LoadSavedColorTheme();
                _themeService.SetColorTheme(savedColorTheme);
                
                // Load and apply saved font settings
                LoadSavedFontSettings();
                
                System.Diagnostics.Debug.WriteLine("[App] Application initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Critical error during initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] Stack trace: {ex.StackTrace}");
                throw; // Re-throw to let the framework handle it
            }
        }

        private string LoadSavedCulture()
        {
            try
            {
                var savedCulture = _preferencesService.GetSavedLanguage();
                
                if (!string.IsNullOrEmpty(savedCulture))
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Loaded saved culture preference: {savedCulture}");
                    return savedCulture;
                }
                
                // Fall back to system culture if no preference saved
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                var supportedCultures = _preferencesService.GetSupportedCultures();
                var fallbackCulture = supportedCultures.Contains(systemCulture) ? systemCulture : supportedCultures[0];
                
                System.Diagnostics.Debug.WriteLine($"[App] No saved preference, using fallback: {fallbackCulture}");
                return fallbackCulture;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load culture preference: {ex.Message}");
                return "en"; // Default to English if everything fails
            }
        }

        private Foodbook.Models.AppTheme LoadSavedTheme()
        {
            try
            {
                var savedTheme = _preferencesService.GetSavedTheme();
                System.Diagnostics.Debug.WriteLine($"[App] Loaded saved theme preference: {savedTheme}");
                return savedTheme;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load theme preference: {ex.Message}");
                return Foodbook.Models.AppTheme.System; // Default to system theme if everything fails
            }
        }

        private AppColorTheme LoadSavedColorTheme()
        {
            try
            {
                var savedColorTheme = _preferencesService.GetSavedColorTheme();
                System.Diagnostics.Debug.WriteLine($"[App] Loaded saved color theme preference: {savedColorTheme}");
                return savedColorTheme;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load color theme preference: {ex.Message}");
                return AppColorTheme.Default; // Default to default color theme if everything fails
            }
        }

        private void LoadSavedFontSettings()
        {
            try
            {
                // Load saved font settings from preferences
                _fontService.LoadSavedSettings();
                
                System.Diagnostics.Debug.WriteLine($"[App] Font settings loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load font settings: {ex.Message}");
                // FontService will use defaults if loading fails
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}