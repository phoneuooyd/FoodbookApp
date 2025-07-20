using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    private readonly Foodbook.Services.ILocalizationService _localizationService;

    public SettingsPage(Foodbook.Services.ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        InitializeComponent();
    }

    private void OnLanguageSelected(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is string culture)
        {
            _localizationService.SetCulture(culture);
            Application.Current.MainPage = MauiProgram.ServiceProvider!.GetRequiredService<AppShell>();
        }
    }
}