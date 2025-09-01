using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "id")]
public partial class ShoppingListDetailPage : ContentPage
{
    private readonly ShoppingListDetailViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;

    public int PlanId { get; set; }

    public ShoppingListDetailPage(ShoppingListDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        
        await _viewModel.LoadAsync(PlanId);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
        
        // Save all states when leaving the page
        await _viewModel.SaveAllStatesAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        Shell.Current.GoToAsync("..");
        return true;
    }
}
