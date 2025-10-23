using System.Linq;
using FoodbookApp.Interfaces;

namespace FoodbookApp
{
    public partial class AppShell : Shell
    {
        private ILocalizationService? _localizationService;

        public AppShell()
        {
            InitializeComponent();
            
            // Get localization service
            _localizationService = MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
            
            // Ustawienie HomePage jako domyślnej zakładki przy starcie
            CurrentItem = Items.OfType<TabBar>().FirstOrDefault()?.Items[2]; // HomePage jest na pozycji 2 (indeks zaczyna się od 0)
            TryUpdateSystemBars();
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

        protected override void OnAppearing()
        {
            base.OnAppearing();
            TryUpdateSystemBars();
        }

        private void OnNavigating(object sender, ShellNavigatingEventArgs e)
        {
        }

        protected override bool OnBackButtonPressed()
        {
            var currentLocation = Shell.Current.CurrentState.Location.ToString();
            if (currentLocation == "//HomeTab" || currentLocation.EndsWith("/HomeTab"))
            {
                // Show exit confirmation
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var title = _localizationService?.GetString("HomePageResources", "ExitAppTitle");
                    var message = _localizationService?.GetString("HomePageResources", "ExitAppConfirmation");
                    bool result = await Application.Current.MainPage.DisplayAlert(title, message, "Yes", "No");
                    if (result)
                    {
                        Application.Current.Quit();
                    }
                });
                return true;
            }
            else
            {
                // Navigate to HomeTab from any other location
                Shell.Current.GoToAsync("//HomeTab");
                return true;
            }
        }
    }
}
