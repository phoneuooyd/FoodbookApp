using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "id")]
public partial class ShoppingListDetailPage : ContentPage
{
    private readonly ShoppingListDetailViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;

    // ? Debouncing support for text changes
    private CancellationTokenSource? _debounceCts;
    private const int DebounceDelayMs = 300;

    // ? Page lifecycle cancellation token
    private CancellationTokenSource? _pageCts;

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
        
        // ? Create new cancellation token for this page session
        _pageCts = new CancellationTokenSource();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        
        // ? Load with cancellation token support
        try
        {
            await _viewModel.LoadAsync(PlanId);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Load cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Load error: {ex.Message}");
            await DisplayAlert("B³¹d", "Nie mo¿na za³adowaæ listy zakupów", "OK");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        
        // ? Cancel any pending debounce operations
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        // ? Cancel page-level operations
        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pageCts = null;
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
        
        // ? DISABLED: Auto-save on page disappearing
        // Manual save button is now used instead
        // if (!_viewModel.IsEditing)
        // {
        //     await SaveAllStatesSafelyAsync();
        // }
    }

    /// <summary>
    /// ? Safe wrapper for SaveAllStatesAsync with error handling
    /// ? DISABLED: Now using manual save button instead
    /// </summary>
    private async Task SaveAllStatesSafelyAsync()
    {
        try
        {
            await _viewModel.SaveAllStatesAsync();
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Save cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Save error: {ex.Message}");
            // Don't show alert on disappearing - just log
        }
    }

    protected override bool OnBackButtonPressed()
    {
        // ? Use async-safe navigation
        SafeNavigateBack();
        return true;
    }

    /// <summary>
    /// ? Safe async navigation wrapper
    /// </summary>
    private void SafeNavigateBack()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Navigation error: {ex.Message}");
            }
        });
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        _viewModel.IsEditing = true;
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        _viewModel.IsEditing = false;
        
        // ? DISABLED: Auto-save when editing is completed
        // Manual save button is now used instead
        // var entry = sender as Entry;
        // var ingredient = entry?.BindingContext as Ingredient;
        // if (ingredient != null)
        // {
        //     _viewModel.OnItemEditingCompleted(ingredient);
        // }
    }

    private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        // ? DISABLED: Debounced auto-save on text change
        // Manual save button is now used instead
        
        // var entry = sender as Entry;
        // var ingredient = entry?.BindingContext as Ingredient;
        // if (ingredient == null) return;

        // SaveItemWithDebounceAsync(ingredient);
    }

    /// <summary>
    /// ? Debounced save implementation - prevents excessive saves during rapid typing
    /// ? DISABLED: Now using manual save button instead
    /// </summary>
    private void SaveItemWithDebounceAsync(Ingredient ingredient)
    {
        // Cancel previous debounce
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();

        var ct = _debounceCts.Token;

        // ? Safe fire-and-forget with proper error handling
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelayMs, ct);
                
                if (!ct.IsCancellationRequested)
                {
                    await _viewModel.SaveItemImmediatelyAsync(ingredient);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when user types quickly - ignore
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Debounced save error: {ex.Message}");
            }
        }, ct);
    }

    // Drag helpers for insert zones (top/bottom)
    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        // DragStartingCommand already invoked via binding; nothing to do here except ensure Data contains SourceItem
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            e.Data.Properties["SourceItem"] = ing;
        }
    }

    private void OnTopInsertDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertBefore = true;
        }
    }

    private void OnTopInsertDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertBefore = false;
        }
    }

    private async void OnTopInsertDrop(object? sender, DropEventArgs e)
    {
        await HandleDropAsync(sender, e, insertAfter: false);
    }

    private void OnBottomInsertDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertAfter = true;
        }
    }

    private void OnBottomInsertDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertAfter = false;
        }
    }

    private async void OnBottomInsertDrop(object? sender, DropEventArgs e)
    {
        await HandleDropAsync(sender, e, insertAfter: true);
    }

    /// <summary>
    /// ? Consolidated drop handler with proper error handling and MainThread safety
    /// </summary>
    private async Task HandleDropAsync(object? sender, DropEventArgs e, bool insertAfter)
    {
        try
        {
            if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
            if (src is not Ingredient dragged) return;
            if (sender is not VisualElement el || el.BindingContext is not Ingredient target) return;

            // ? Ensure UI updates happen on main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Clear visual indicators
                target.ShowInsertBefore = false;
                target.ShowInsertAfter = false;

                // Perform reorder
                await _viewModel.ReorderItemsAsync(dragged, target, insertAfter);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Drop error: {ex.Message}");
            
            // ? Show user-friendly error message
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("B³¹d", "Nie mo¿na zmieniæ kolejnoœci elementu", "OK");
            });
        }
    }
}
