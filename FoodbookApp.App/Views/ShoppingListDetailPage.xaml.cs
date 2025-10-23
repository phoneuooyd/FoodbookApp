using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Foodbook.Models;

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
        
        // Save all states when leaving the page, but only if not editing
        if (!_viewModel.IsEditing)
        {
            await _viewModel.SaveAllStatesAsync();
        }
    }

    protected override bool OnBackButtonPressed()
    {
        Shell.Current.GoToAsync("..");
        return true;
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        _viewModel.IsEditing = true;
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        _viewModel.IsEditing = false;
        
        // Save the item state when editing is completed
        var entry = sender as Entry;
        var ingredient = entry?.BindingContext as Ingredient;
        if (ingredient != null)
        {
            _viewModel.OnItemEditingCompleted(ingredient);
        }
    }
}
