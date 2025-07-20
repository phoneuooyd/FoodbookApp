using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Data;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;
using FoodbookApp.Localization;

namespace Foodbook.Views;

public partial class IngredientsPage : ContentPage
{
    private readonly IngredientsViewModel _viewModel;
    private bool _isInitialized;

    public IngredientsPage(IngredientsViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Only load once or if explicitly needed
        if (!_isInitialized)
        {
            await _viewModel.LoadAsync();
            _isInitialized = true;

            // Check for seeding only after initial load is complete
            if (_viewModel.Ingredients.Count == 0 && !_viewModel.IsLoading)
            {
                await HandleEmptyIngredientsAsync();
            }
        }
        else
        {
            // If we're returning to the page, just refresh if needed
            // This handles cases where ingredients might have been added/modified
            await _viewModel.ReloadAsync();
        }
    }

    private async Task HandleEmptyIngredientsAsync()
    {
        try
        {
            bool create = await DisplayAlert(IngredientsPageResources.EmptyDialogTitle,
                IngredientsPageResources.EmptyDialogMessage,
                IngredientsPageResources.EmptyDialogConfirm,
                IngredientsPageResources.EmptyDialogCancel);
            
            if (create && MauiProgram.ServiceProvider != null)
            {
                using var scope = MauiProgram.ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await SeedData.SeedIngredientsAsync(db);
                
                // Reload the data after seeding
                await _viewModel.ReloadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling empty ingredients: {ex.Message}");
            await DisplayAlert(IngredientsPageResources.LoadErrorTitle,
                IngredientsPageResources.LoadErrorMessage,
                IngredientsPageResources.Ok);
        }
    }
}
