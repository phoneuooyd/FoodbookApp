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
        try
        {
            if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
            if (!(src is Ingredient dragged)) return;
            if (!(sender is VisualElement el && el.BindingContext is Ingredient target)) return;

            // call VM reorder with insertAfter = false
            await _viewModel.ReorderItemsAsync(dragged, target, insertAfter: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TopInsertDrop error: {ex.Message}");
        }
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
        try
        {
            if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
            if (!(src is Ingredient dragged)) return;
            if (!(sender is VisualElement el && el.BindingContext is Ingredient target)) return;

            // call VM reorder with insertAfter = true
            await _viewModel.ReorderItemsAsync(dragged, target, insertAfter: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BottomInsertDrop error: {ex.Message}");
        }
    }
}
