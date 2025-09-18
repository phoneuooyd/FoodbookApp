using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;

namespace Foodbook.Views.Base
{
    /// <summary>
    /// Helper class for managing theme, font, and localization changes in ContentPages
    /// </summary>
    public class PageThemeHelper : IDisposable
    {
        private IThemeService? _themeService;
        private IFontService? _fontService;
        private ILocalizationService? _localizationService;
        private bool _disposed = false;

        /// <summary>
        /// Event handlers for theme, font, and culture changes
        /// </summary>
        public event EventHandler? ThemeChanged;
        public event EventHandler? FontChanged;
        public event EventHandler? CultureChanged;

        /// <summary>
        /// Initialize the helper and subscribe to service events
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Get services from dependency injection
                var serviceProvider = FoodbookApp.MauiProgram.ServiceProvider;
                if (serviceProvider != null)
                {
                    _themeService = serviceProvider.GetService<IThemeService>();
                    _fontService = serviceProvider.GetService<IFontService>();
                    _localizationService = serviceProvider.GetService<ILocalizationService>();

                    // Subscribe to change events
                    if (_fontService != null)
                    {
                        _fontService.FontSettingsChanged += OnFontSettingsChanged;
                    }

                    if (_localizationService != null)
                    {
                        _localizationService.CultureChanged += OnLocalizationCultureChanged;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageThemeHelper] Error initializing: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup subscriptions and dispose resources
        /// </summary>
        public void Cleanup()
        {
            if (_disposed) return;

            try
            {
                // Unsubscribe from events to prevent memory leaks
                if (_fontService != null)
                {
                    _fontService.FontSettingsChanged -= OnFontSettingsChanged;
                }

                if (_localizationService != null)
                {
                    _localizationService.CultureChanged -= OnLocalizationCultureChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageThemeHelper] Error cleaning up: {ex.Message}");
            }

            _disposed = true;
        }

        private void OnFontSettingsChanged(object? sender, FontSettingsChangedEventArgs e)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FontChanged?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageThemeHelper] Error in OnFontSettingsChanged: {ex.Message}");
            }
        }

        private void OnLocalizationCultureChanged(object? sender, EventArgs e)
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CultureChanged?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PageThemeHelper] Error in OnLocalizationCultureChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current theme service instance
        /// </summary>
        public IThemeService? ThemeService => _themeService;

        /// <summary>
        /// Gets the current font service instance
        /// </summary>
        public IFontService? FontService => _fontService;

        /// <summary>
        /// Gets the current localization service instance
        /// </summary>
        public ILocalizationService? LocalizationService => _localizationService;

        public void Dispose()
        {
            Cleanup();
            GC.SuppressFinalize(this);
        }

        ~PageThemeHelper()
        {
            Cleanup();
        }
    }
}