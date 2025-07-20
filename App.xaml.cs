using Microsoft.Maui;
using Microsoft.Extensions.DependencyInjection;

namespace FoodbookApp
{
    public partial class App : Application
    {
        private readonly Foodbook.Services.ILocalizationService _localizationService;

        public App(Foodbook.Services.ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            _localizationService.SetCulture(System.Globalization.CultureInfo.CurrentUICulture.Name);
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}