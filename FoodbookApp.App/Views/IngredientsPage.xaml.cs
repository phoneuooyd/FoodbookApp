using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Data;
using Foodbook.Views.Base;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;
using Foodbook.Views.Components;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.Views;

public partial class IngredientsPage : ContentPage
{
    private readonly IngredientsViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;
    private bool _isInitialized;

    // Expose current instance for direct refresh from other views/popups
    public static IngredientsPage? Current { get; private set; }

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
        
        Current = this; // register current instance for external refresh
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        
        // Subscribe to global ingredients-changed event
        AppEvents.IngredientsChangedAsync += OnIngredientsChangedAsync;
        // Subscribe to single-ingredient saved event to force reload immediately
        AppEvents.IngredientSaved += OnIngredientSaved;
        
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

        // Unsubscribe to avoid leaks
        AppEvents.IngredientsChangedAsync -= OnIngredientsChangedAsync;
        AppEvents.IngredientSaved -= OnIngredientSaved;

        if (Current == this)
            Current = null;
    }

    // Direct refresh API used by other components
    public async Task ForceReloadAsync()
    {
        try
        {
            await _viewModel.ReloadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsPage] ForceReloadAsync error: {ex.Message}");
        }
    }

    private async Task OnIngredientsChangedAsync()
    {
        try
        {
            await _viewModel.ReloadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsPage] Reload on IngredientsChanged failed: {ex.Message}");
        }
    }

    // Handler for AppEvents.IngredientSaved
    private void OnIngredientSaved(int id)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() => _ = ForceReloadAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsPage] OnIngredientSaved handler error: {ex.Message}");
        }
    }

    private async Task HandleEmptyIngredientsAsync()
    {
        try
        {
            bool create = await DisplayAlert(
                FoodbookApp.Localization.IngredientsPageResources.EmptyIngredientsTitle, 
                FoodbookApp.Localization.IngredientsPageResources.EmptyIngredientsMessage, 
                FoodbookApp.Localization.IngredientsPageResources.Yes, 
                FoodbookApp.Localization.IngredientsPageResources.No);
            
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
            await DisplayAlert(
                FoodbookApp.Localization.IngredientsPageResources.ErrorTitle, 
                FoodbookApp.Localization.IngredientsPageResources.LoadingIngredientsError, 
                FoodbookApp.Localization.IngredientsPageResources.OK);
        }
    }

    private async void OnIngredientSortClicked(object? sender, EventArgs e)
    {
        try
        {
            var popup = new FilterSortPopup(showLabels: false, labels: null, preselectedLabelIds: null, sortOrder: _viewModel.SortOrder, showApplyButton: false);
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
