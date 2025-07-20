using Microsoft.Maui.Controls;
using Foodbook.Services;
using System.Collections.Generic;
using System.Linq;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    private readonly ILocalizationService _localizationService;
    private readonly List<(string Culture, string DisplayName)> _languages = new()
    {
        ("en-US", "English"),
        ("pl-PL", "Polski")
    };

    public SettingsPage(ILocalizationService localizationService)
    {
        InitializeComponent();
        _localizationService = localizationService;

        LanguagePicker.ItemsSource = _languages.Select(l => l.DisplayName).ToList();
        var index = _languages.FindIndex(l => l.Culture == _localizationService.CurrentCulture.Name);
        if (index >= 0)
            LanguagePicker.SelectedIndex = index;
    }

    private void OnLanguageSelected(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedIndex < 0 || LanguagePicker.SelectedIndex >= _languages.Count)
            return;

        var culture = _languages[LanguagePicker.SelectedIndex].Culture;
        if (_localizationService.CurrentCulture.Name == culture)
            return;

        _localizationService.SetCulture(culture);
        Application.Current.MainPage = new AppShell();
    }
}