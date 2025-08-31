using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Services;

namespace FoodbookApp
{
    public partial class App : Application
    {
        private readonly ILocalizationService _localizationService;
        private readonly IPreferencesService _preferencesService;
        private readonly IThemeService _themeService;

        public App(ILocalizationService localizationService, IPreferencesService preferencesService, IThemeService themeService)
        {
            _localizationService = localizationService;
            _preferencesService = preferencesService;
            _themeService = themeService;
            
            // Load saved language preference or use system default
            var savedCulture = LoadSavedCulture();
            _localizationService.SetCulture(savedCulture);
            
            // Load and apply saved theme
            var savedTheme = LoadSavedTheme();
            _themeService.SetTheme(savedTheme);
            
            InitializeComponent();
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

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}