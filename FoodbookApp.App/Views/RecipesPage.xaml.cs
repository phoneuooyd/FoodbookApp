using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using Foodbook.Models; // added for Recipe type
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.Views;

public partial class RecipesPage : ContentPage
{
    private readonly RecipeViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;
    private bool _isInitialized;

    public RecipesPage(RecipeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();

        // Stop pull-to-refresh spinner when VM signals all data loaded
        _viewModel.DataLoaded += (_, __) =>
        {
            try
            {
                var list = this.FindByName<Foodbook.Views.Components.GenericListComponent>("ListComponent");
                list?.RequestStopRefreshing();
            }
            catch { }
        };

        // Register for FAB collapse message via WeakReferenceMessenger (replaces deprecated MessagingCenter)
        WeakReferenceMessenger.Default.Register<FabCollapseMessage>(this, async (_, __) =>
        {
            try
            {
                var fab = this.FindByName<Foodbook.Views.Components.FloatingActionButtonComponent>("RecipesFab");
                if (fab != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        var method = fab.GetType().GetMethod("CollapseAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (method != null)
                        {
                            if (method.Invoke(fab, null) is Task t) await t;
                        }
                    });
                }
            }
            catch { }
        });

        // Collapse fab after executing actions
        AttachFabActionCollapsers();
    }

    private void AttachFabActionCollapsers()
    {
        Loaded += (_, __) =>
        {
            try
            {
                var fab = this.FindByName<Foodbook.Views.Components.FloatingActionButtonComponent>("RecipesFab");
                if (fab == null) return;

                // Wrap existing commands to collapse after execute
                foreach (var action in fab.Actions)
                {
                    var original = action.Command;
                    action.Command = new Command(async (p) =>
                    {
                        try { if (original?.CanExecute(p) == true) original.Execute(p); }
                        finally
                        {
                            var method = fab.GetType().GetMethod("CollapseAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (method?.Invoke(fab, null) is Task t) await t;
                        }
                    });
                }
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
            await _viewModel.LoadRecipesAsync();
            _isInitialized = true;
        }
        else
        {
            await _viewModel.LoadRecipesAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
        WeakReferenceMessenger.Default.Unregister<FabCollapseMessage>(this);
    }

    private void OnBackClicked(object? sender, System.EventArgs e)
    {
        if (_viewModel.GoBackCommand?.CanExecute(null) == true)
            _viewModel.GoBackCommand.Execute(null);
    }

    // Handle dropping a recipe onto the back button: move up one level
    private async void OnBackDrop(object? sender, DropEventArgs e)
    {
        try
        {
            if (e?.Data?.Properties?.TryGetValue("SourceItem", out var source) == true && source is Recipe recipe)
            {
                await _viewModel.MoveRecipeUpAsync(recipe);
            }
        }
        catch
        {
            // ignore errors
        }
    }

    // Drop anywhere on breadcrumb area moves up one level
    private async void OnBreadcrumbDrop(object? sender, DropEventArgs e)
    {
        try
        {
            if (e?.Data?.Properties?.TryGetValue("SourceItem", out var source) == true && source is Recipe recipe)
            {
                await _viewModel.MoveRecipeUpAsync(recipe);
            }
        }
        catch { }
    }

    // Open filter/sort popup from breadcrumb image button
    private async void OnFilterSortClicked(object? sender, EventArgs e)
    {
        try
        {
            // Labels available for filtering are managed in SettingsViewModel
            var settingsVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<SettingsViewModel>();
            var allLabels = settingsVm?.Labels?.ToList() ?? new List<RecipeLabel>();

            var popup = new FilterSortPopup(
                showLabels: true,
                labels: allLabels,
                preselectedLabelIds: _viewModel.SelectedLabelIds,
                sortByName: _viewModel.SortByName);

            var hostPage = Application.Current?.MainPage ?? this;
            hostPage.ShowPopup(popup);
            var result = await popup.ResultTask;
            if (result != null)
            {
                _viewModel.ApplySortingAndLabelFilter(result.SortByName, result.SelectedLabelIds);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] OnFilterSortClicked error: {ex.Message}");
        }
    }
}
