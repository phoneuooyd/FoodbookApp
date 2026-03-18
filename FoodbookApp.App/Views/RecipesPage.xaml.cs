using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using CommunityToolkit.Mvvm.Messaging;
using System.Threading.Tasks;
using Foodbook.Models;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Behaviors;
using Foodbook.Views.Components;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;
using Foodbook.Services;
using FoodbookApp.Models.Messages;

namespace Foodbook.Views;

public partial class RecipesPage : ContentPage, ITabLoadable
{
    private readonly RecipeViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;
    private bool _isInitialized;

    // Guard to avoid opening FilterSortPopup multiple times rapidly
    private bool _isFilterPopupOpening;

    public RecipesPage(RecipeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();

        // ? CRITICAL: Subscribe to AppEvents in constructor so they work even when page is cached
        AppEvents.RecipesChangedAsync += OnRecipesChangedAsync;
        AppEvents.RecipeSaved += OnRecipeSaved;

        // Reload when a recipe is saved from AddRecipePage (page may not re-appear if pushed modally)
        WeakReferenceMessenger.Default.Register<RecipesReloadMessage>(this, async (_, msg) =>
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        _viewModel.ResetFolderNavigation();
                        await _viewModel.LoadRecipesAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipesPage] Reload via RecipesReloadMessage failed: {ex.Message}");
                    }
                });
            }
            catch { }
        });

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
        _themeHelper.ThemeChanged += OnThemeChanged;

        // Apply initial tint color
        RefreshFilterButtonTintColor();

        // ? NEW: Drain any pending RecipeSaved events that may have queued before page was created
        AppEvents.DrainPendingRecipeSavedEvents(OnRecipeSaved);

        // Always attempt a fresh reload when appearing to ensure UI reflects latest DB state
        try
        {
            await _viewModel.LoadRecipesAsync();
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] Initial reload failed: {ex.Message}");
            try { await _viewModel.LoadRecipesAsync(); } catch { }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _themeHelper.ThemeChanged -= OnThemeChanged;
        _themeHelper.Cleanup();
        WeakReferenceMessenger.Default.Unregister<FabCollapseMessage>(this);
        WeakReferenceMessenger.Default.Unregister<RecipesReloadMessage>(this);

        // ? DO NOT unsubscribe from AppEvents here - keep them alive for cached page
        // AppEvents.RecipesChangedAsync -= OnRecipesChangedAsync;
        // AppEvents.RecipeSaved -= OnRecipeSaved;
    }

    // ? PUBLIC: Called by TabBarComponent when RecipesPage tab is selected (even when cached)
    public async Task TriggerReloadAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[RecipesPage] TriggerReloadAsync called by TabBar - forcing reload");
            
            // ? Drain any pending events BEFORE reload
            AppEvents.DrainPendingRecipeSavedEvents(OnRecipeSaved);
            
            _viewModel.ResetFolderNavigation();
            await _viewModel.LoadRecipesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] TriggerReloadAsync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Called by TabBarComponent when this tab is activated.
    /// </summary>
    public async Task OnTabActivatedAsync()
    {
        await TriggerReloadAsync();
    }

    private async Task OnRecipesChangedAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () => await _viewModel.ReloadAsync());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] Reload on RecipesChanged failed: {ex.Message}");
        }
    }

    private void OnRecipeSaved(Guid id)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] OnRecipeSaved - Recipe ID: {id}, forcing FULL reload");
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // Reset folder navigation to root
                    _viewModel.ResetFolderNavigation();
                    
                    // Force fresh load from database (same as app startup)
                    await _viewModel.LoadRecipesAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"[RecipesPage] FULL reload completed after recipe save");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipesPage] FULL reload failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] OnRecipeSaved handler error: {ex.Message}");
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshFilterButtonTintColor);
    }

    private void RefreshFilterButtonTintColor()
    {
        try
        {
            // Read Primary from resources updated by ThemeService
            var app = Application.Current;
            if (app?.Resources == null || FilterButton == null) return;

            Color? tintColor = null;
            if (app.Resources.TryGetValue("Primary", out var primaryObj))
            {
                if (primaryObj is Color c) tintColor = c;
                else if (primaryObj is SolidColorBrush b) tintColor = b.Color;
            }

            // Fallback
            if (tintColor == null && app.Resources.TryGetValue("TabBarIconTint", out var iconTintObj))
            {
                if (iconTintObj is Color c2) tintColor = c2; else if (iconTintObj is SolidColorBrush b2) tintColor = b2.Color;
            }

            var behavior = FilterButton.Behaviors.OfType<IconTintColorBehavior>().FirstOrDefault();
            if (behavior != null && tintColor != null)
            {
                behavior.TintColor = tintColor;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] RefreshFilterButtonTintColor failed: {ex.Message}");
        }
    }

    // Public method for TabBarComponent to cleanup when switching tabs
    public void CleanupForTabBar()
    {
        WeakReferenceMessenger.Default.Unregister<FabCollapseMessage>(this);
    }

    private void OnBackClicked(object? sender, System.EventArgs e)
    {
        if (_viewModel.GoBackCommand?.CanExecute(null) == true)
            _viewModel.GoBackCommand.Execute(null);
    }

    // Breadcrumb drop
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

    private async void OnFilterSortClicked(object? sender, EventArgs e)
    {
        if (_isFilterPopupOpening || !FilterSortPopup.TryAcquireOpen())
            return;
        _isFilterPopupOpening = true;

        try
        {
            System.Diagnostics.Debug.WriteLine("[RecipesPage] OnFilterSortClicked invoked");
             var settingsVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<SettingsViewModel>();
             var allLabels = settingsVm?.Labels?.ToList() ?? new List<RecipeLabel>();

            var ingredientService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IIngredientService>();
            var allIngredients = ingredientService != null ? await ingredientService.GetIngredientsAsync() : new List<Ingredient>();

            var popup = new FilterSortPopup(
                showLabels: true,
                labels: allLabels,
                preselectedLabelIds: _viewModel.SelectedLabelIds,
                sortOrder: _viewModel.SortOrder,
                showIngredients: true,
                ingredients: allIngredients,
                preselectedIngredientNames: _viewModel.SelectedIngredientNames,
                sortBy: _viewModel.CurrentSortBy,
                showApplyButton: true);

            var nav = Application.Current?.MainPage?.Navigation ?? Navigation;
            if (nav == null)
            {
                System.Diagnostics.Debug.WriteLine("[RecipesPage] OnFilterSortClicked: Navigation is null");
                return;
            }

            await nav.PushModalAsync(popup, true);
              var result = await popup.ResultTask;
              if (result != null)
              {
                  _viewModel.ApplySortingLabelAndIngredientFilter(result.SortBy, result.SelectedLabelIds, result.SelectedIngredientNames);
              }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RecipesPage] OnFilterSortClicked error: {ex.Message}");
        }
        finally
        {
            _isFilterPopupOpening = false;
            FilterSortPopup.ReleaseOpen();
        }
    }
}
