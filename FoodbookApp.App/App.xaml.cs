using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Models;
using Foodbook.Views;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;

namespace FoodbookApp
{
    public partial class App : Application
    {
        private readonly ILocalizationService _localizationService;
        private readonly IPreferencesService _preferencesService;
        private readonly IThemeService _themeService;
        private readonly IFontService _fontService;
        private bool _globalHandlersRegistered = false;

        public App(ILocalizationService localizationService, IPreferencesService preferencesService, IThemeService themeService, IFontService fontService)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App][Ctor] Starting constructor");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[App][Ctor] XAML components initialized");
                _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
                _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
                _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
                _fontService = fontService ?? throw new ArgumentNullException(nameof(fontService));

                RegisterGlobalExceptionHandlers();

                var savedCulture = LoadSavedCulture();
                _localizationService.SetCulture(savedCulture);
                System.Diagnostics.Debug.WriteLine($"[App][Ctor] Culture applied: {savedCulture}");

                var savedTheme = LoadSavedTheme();
                _themeService.SetTheme(savedTheme);
                System.Diagnostics.Debug.WriteLine($"[App][Ctor] Theme applied: {savedTheme}");

                var savedColorTheme = LoadSavedColorTheme();
                _themeService.SetColorTheme(savedColorTheme);
                System.Diagnostics.Debug.WriteLine($"[App][Ctor] Color theme applied: {savedColorTheme}");

                var isColorfulBackgroundEnabled = LoadSavedColorfulBackgroundSetting();
                _themeService.SetColorfulBackground(isColorfulBackgroundEnabled);
                System.Diagnostics.Debug.WriteLine($"[App][Ctor] Colorful background: {isColorfulBackgroundEnabled}");

                LoadSavedFontSettings();
                System.Diagnostics.Debug.WriteLine("[App] Application initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Critical error during initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void RegisterGlobalExceptionHandlers()
        {
            if (_globalHandlersRegistered) return;
            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    var ex = e.ExceptionObject as Exception;
                    System.Diagnostics.Debug.WriteLine($"[Global][AppDomain] Unhandled: {ex?.Message}\n{ex?.StackTrace}");
                };
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Global][TaskScheduler] Unobserved: {e.Exception.Message}\n{e.Exception.StackTrace}");
                    e.SetObserved();
                };
                _globalHandlersRegistered = true;
                System.Diagnostics.Debug.WriteLine("[App] Global exception handlers registered");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to register global handlers: {ex.Message}");
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
                var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                var supportedCultures = _preferencesService.GetSupportedCultures();
                var fallbackCulture = supportedCultures.Contains(systemCulture) ? systemCulture : supportedCultures[0];
                System.Diagnostics.Debug.WriteLine($"[App] No saved preference, using fallback: {fallbackCulture}");
                return fallbackCulture;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load culture preference: {ex.Message}");
                return "en";
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
                return Foodbook.Models.AppTheme.System;
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
                return AppColorTheme.Default;
            }
        }

        private void LoadSavedFontSettings()
        {
            try
            {
                _fontService.LoadSavedSettings();
                System.Diagnostics.Debug.WriteLine("[App] Font settings loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load font settings: {ex.Message}");
            }
        }

        private bool LoadSavedColorfulBackgroundSetting()
        {
            try
            {
                var isEnabled = _preferencesService.GetIsColorfulBackgroundEnabled();
                System.Diagnostics.Debug.WriteLine($"[App] Loaded colorful background preference: {isEnabled}");
                return isEnabled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to load colorful background preference: {ex.Message}");
                return false;
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[App] CreateWindow invoked");
                Window window;
                if (_preferencesService.IsFirstLaunch())
                {
                    System.Diagnostics.Debug.WriteLine("[App] First launch detected - resolving SetupWizardPage from DI");
                    var setupPage = MauiProgram.ServiceProvider?.GetService<SetupWizardPage>();
                    if (setupPage == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[App] DI failed to resolve SetupWizardPage - fallback manual creation");
                        var vm = MauiProgram.ServiceProvider?.GetRequiredService<SetupWizardViewModel>();
                        setupPage = new SetupWizardPage(vm!);
                    }
                    window = new Window(setupPage);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[App] Returning user - showing main shell");
                    window = new Window(new AppShell());
                }

                // Ensure system bars use the saved theme colors once the platform window exists
                window.Created += OnWindowCreated;

                return window;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] CreateWindow crash: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void OnWindowCreated(object? sender, EventArgs e)
        {
            try
            {
                // Re-apply current color theme to force system bars update when window is ready
                var currentColorTheme = _themeService.GetCurrentColorTheme();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _themeService.SetColorTheme(currentColorTheme);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] OnWindowCreated error: {ex.Message}");
            }
        }
    }
}