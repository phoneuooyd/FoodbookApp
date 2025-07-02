using Foodbook.Localization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
        LanguagePicker.SelectedIndex = LocalizationResourceManager.Instance.CurrentCulture.TwoLetterISOLanguageName == "pl" ? 1 : 0;
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        var culture = LanguagePicker.SelectedIndex == 1 ? new CultureInfo("pl") : new CultureInfo("en");
        LocalizationResourceManager.Instance.CurrentCulture = culture;
        Preferences.Default.Set("AppLanguage", culture.TwoLetterISOLanguageName);
    }
}
