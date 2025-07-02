using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    private SettingsViewModel ViewModel => BindingContext as SettingsViewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Force refresh of the picker selection when the page appears
        if (ViewModel != null && ViewModel.AvailableLanguages.Count > 0)
        {
            var selected = ViewModel.AvailableLanguages.FirstOrDefault(l => l.Code == ViewModel.SelectedLanguage.Code);
            if (!selected.Equals(default((string, string))))
            {
                LanguagePicker.SelectedItem = selected;
            }
        }
    }
}