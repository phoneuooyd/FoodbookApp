using System.Linq;
using FoodbookApp.Interfaces;

namespace FoodbookApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            
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
    }
}
