using Microsoft.Maui.Controls;
using Foodbook.Services.Localization;
using System.Globalization;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    private readonly LocalizationResourceManager _locManager;

    public SettingsPage(LocalizationResourceManager locManager)
    {
        _locManager = locManager;
        InitializeComponent();

        LanguagePicker.SelectedItem = _locManager.CurrentCulture.TwoLetterISOLanguageName;
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (LanguagePicker.SelectedItem is string code)
        {
            string culture = code == "pl" ? "pl-PL" : "en-US";
            _locManager.SetCulture(culture);
        }
    }
}
