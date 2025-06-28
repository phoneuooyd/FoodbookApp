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
}

