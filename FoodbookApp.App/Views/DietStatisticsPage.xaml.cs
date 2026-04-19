using Foodbook.ViewModels;
using Foodbook.Views.Base;
using FoodbookApp.Interfaces;

namespace Foodbook.Views;

public partial class DietStatisticsPage : ContentPage, ITabLoadable
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
            ViewModel.StartListening();
            await ViewModel.LoadAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel?.StopListening();
        _themeHelper.Cleanup();
    }

    public async Task OnTabActivatedAsync()
    {
        try
        {
            if (ViewModel == null)
            {
                return;
            }

            ViewModel.StartListening();
            await ViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DietStatisticsPage] OnTabActivatedAsync error: {ex.Message}");
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
