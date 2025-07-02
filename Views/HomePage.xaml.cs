using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class HomePage : ContentPage
{
    private HomeViewModel ViewModel => BindingContext as HomeViewModel;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ViewModel != null)
            await ViewModel.LoadAsync();
    }

    private async void OnArchiveClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ArchivePage));
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}

