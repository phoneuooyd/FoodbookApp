using System.Linq;
using Foodbook.Utils;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;
using Foodbook.Views;

namespace FoodbookApp
{
    public partial class AppShell : Shell
    {
        private ILocalizationService? _localizationService;
        private bool _hasSeenFirstNavigation;
        private string _lastAnimatedLocation = string.Empty;

        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(DietStatisticsPage), typeof(DietStatisticsPage));
            
            // Get localization service
            _localizationService = MauiProgram.ServiceProvider?.GetService<ILocalizationService>();

            Navigated += OnShellNavigatedAnimate;
            
            TryUpdateSystemBars();
            
            System.Diagnostics.Debug.WriteLine("[AppShell] Initialized with custom TabBarComponent in MainPage");
        }

        private void TryUpdateSystemBars()
        {
            try
            {
                var themeService = MauiProgram.ServiceProvider?.GetService<IThemeService>();
                themeService?.UpdateSystemBars();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] TryUpdateSystemBars error: {ex.Message}");
            }
        }

        private async void OnShellNavigatedAnimate(object? sender, ShellNavigatedEventArgs e)
        {
            try
            {
                var location = e.Current?.Location?.ToString() ?? string.Empty;

                if (!_hasSeenFirstNavigation)
                {
                    _hasSeenFirstNavigation = true;
                    _lastAnimatedLocation = location;
                    return;
                }

                if (string.Equals(location, _lastAnimatedLocation, StringComparison.Ordinal))
                    return;

                _lastAnimatedLocation = location;

                // Do not animate the Main route because tab transitions are handled inside TabBarComponent.
                if (IsMainLocation(location) || CurrentPage is MainPage)
                    return;

                var useVerticalLift = e.Source is not ShellNavigationSource.Pop and not ShellNavigationSource.PopToRoot;
                await PageTransitionAnimator.AnimatePageEnterAsync(CurrentPage, useVerticalLift);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Navigation animation error: {ex.Message}");
            }
        }

        private static bool IsMainLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                return false;

            return location.Contains("//Main", StringComparison.OrdinalIgnoreCase)
                || location.EndsWith("/Main", StringComparison.OrdinalIgnoreCase)
                || string.Equals(location, "Main", StringComparison.OrdinalIgnoreCase);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            TryUpdateSystemBars();
        }

        protected override bool OnBackButtonPressed()
        {
            // Prefer centralized handling in MainPage's TabBarComponent, but do not block the UI thread
            var mainPage = GetMainPage();
            if (mainPage != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var handled = await mainPage.TabBar.HandleBackButtonAsync();
                        System.Diagnostics.Debug.WriteLine($"[AppShell] Delegated back handled: {handled}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AppShell] Delegated back handling error: {ex.Message}");
                    }
                });
                return true; // consume back press immediately to avoid deadlock
            }
            
            // Fallback behavior if MainPage/TabBar not available
            var currentLocation = Shell.Current?.CurrentState?.Location?.ToString() ?? string.Empty;
            if (currentLocation == "//Main" || currentLocation.EndsWith("/Main"))
            {
                // Show exit confirmation (localized if possible) without blocking
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var title = _localizationService?.GetString("HomePageResources", "ExitAppTitle") ?? "Wyjście";
                    var message = _localizationService?.GetString("HomePageResources", "ExitAppConfirmation") ?? "Czy chcesz wyjść z aplikacji?";
                    bool result = await Application.Current!.MainPage!.DisplayAlert(title, message, ButtonResources.Yes, ButtonResources.No);
                    if (result)
                    {
                        Application.Current?.Quit();
                    }
                });
                return true;
            }
            else
            {
                // Navigate back to Main
                Shell.Current?.GoToAsync("//Main");
                return true;
            }
        }

        /// <summary>
        /// Get the MainPage instance if it's currently displayed
        /// </summary>
        private MainPage? GetMainPage()
        {
            try
            {
                return Shell.Current?.CurrentPage as MainPage;
            }
            catch
            {
                return null;
            }
        }
    }
}
