using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Services;

namespace FoodbookApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            
            // Initialize language settings at app startup
            InitializeLanguage();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        private void InitializeLanguage()
        {
            try
            {
                if (MauiProgram.ServiceProvider != null)
                {
                    using var scope = MauiProgram.ServiceProvider.CreateScope();
                    var localizationService = scope.ServiceProvider.GetRequiredService<ILocalizationService>();
                    localizationService.InitializeLanguage();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing language: {ex.Message}");
            }
        }
    }
}