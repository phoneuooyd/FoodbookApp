using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views;

public partial class DietStatisticsPage : ContentPage
{
    private readonly PageThemeHelper _themeHelper;

    private DietStatisticsViewModel? ViewModel => BindingContext as DietStatisticsViewModel;

    public DietStatisticsPage(DietStatisticsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _themeHelper = new PageThemeHelper();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _themeHelper.Initialize();

        if (ViewModel != null)
        {
            await ViewModel.LoadAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _themeHelper.Cleanup();
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
