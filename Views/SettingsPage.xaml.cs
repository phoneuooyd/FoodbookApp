using Microsoft.Maui.Controls;
using Foodbook.Services;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILocalizationService _localizationService;
    private readonly Dictionary<string, string> _cultures = new()
    {
        { "English", "en" },
        { "Polski", "pl-PL" }
    };

    public SettingsPage(ILocalizationService localizationService)
    {
        InitializeComponent();
        _localizationService = localizationService;

        LanguagePicker.ItemsSource = _cultures.Keys.ToList();
        var current = _cultures.FirstOrDefault(c => c.Value == _localizationService.CurrentCulture.Name).Key;
        LanguagePicker.SelectedItem = current;
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedItem is string key && _cultures.TryGetValue(key, out var culture))
        {
            _localizationService.SetCulture(culture);
            Application.Current.MainPage = new AppShell();
        }
    }
}