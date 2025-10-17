using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Data;
using Foodbook.Views.Base;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;
using Foodbook.Views.Components;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Models;

namespace Foodbook.Views;

public partial class IngredientsPage : ContentPage
{
    private readonly IngredientsViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;
    private bool _isInitialized;

    public IngredientsPage(IngredientsViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();

        // Stop pull-to-refresh spinner exactly when data fully loaded
        _viewModel.DataLoaded += (_, __) =>
        {
            try
            {
                // Find the GenericListComponent and request stop
                var list = this.FindByName<Foodbook.Views.Components.GenericListComponent>("ListComponent");
                list?.RequestStopRefreshing();
            }
            catch { }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
    }

    private async Task HandleEmptyIngredientsAsync()
    {
        try
        {
            bool create = await DisplayAlert("Brak składników", 
                "Utworzyć listę przykładowych składników?", "Tak", "Nie");
            
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
            await DisplayAlert("Błąd", "Wystąpił problem podczas ładowania składników.", "OK");
        }
    }

    private async void OnIngredientSortClicked(object? sender, EventArgs e)
    {
        try
        {
            var popup = new FilterSortPopup(showLabels: false, labels: null, preselectedLabelIds: null, sortOrder: _viewModel.SortOrder);
            var hostPage = Application.Current?.MainPage ?? this;
            hostPage.ShowPopup(popup);
            var result = await popup.ResultTask;
            if (result != null)
            {
                _viewModel.SortOrder = result.SortOrder;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsPage] OnIngredientSortClicked error: {ex.Message}");
        }
    }
}
