using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Models;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "id")]
public partial class ShoppingListDetailPage : ContentPage
{
    private readonly ShoppingListDetailViewModel _viewModel;
    private Ingredient? _draggedItem;

    public int PlanId { get; set; }

    public ShoppingListDetailPage(ShoppingListDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(PlanId);
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        // Save all states when leaving the page
        await _viewModel.SaveAllStatesAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        Shell.Current.GoToAsync("..");
        return true;
    }

    private void OnItemDragStarting(object sender, DragStartingEventArgs e)
    {
        try
        {
            // Get the dragged item from the binding context
            if (sender is Element element && element.BindingContext is Ingredient item)
            {
                _draggedItem = item;
                
                // Set the data for the drag operation
                e.Data.Properties["DraggedItem"] = item;

                System.Diagnostics.Debug.WriteLine($"Drag started for: {item.Name}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnItemDragStarting: {ex.Message}");
        }
    }

    private void OnItemDragOver(object sender, DragEventArgs e)
    {
        // Allow the drop operation
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void OnItemDropped(object sender, Microsoft.Maui.Controls.DropEventArgs e)
    {
        try
        {
            // Get the target item from the binding context
            if (sender is Element element && element.BindingContext is Ingredient targetItem && _draggedItem != null)
            {
                // Check if we're dropping on a different item
                if (_draggedItem != targetItem)
                {
                    // Determine which collection the items belong to
                    var draggedInUnchecked = _viewModel.UncheckedItems.Contains(_draggedItem);
                    var targetInUnchecked = _viewModel.UncheckedItems.Contains(targetItem);

                    // Only allow reordering within the same collection
                    if (draggedInUnchecked == targetInUnchecked)
                    {
                        await _viewModel.ReorderItemsAsync(_draggedItem, targetItem);
                        System.Diagnostics.Debug.WriteLine($"Reordered {_draggedItem.Name} relative to {targetItem.Name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnItemDropped: {ex.Message}");
        }
        finally
        {
            _draggedItem = null;
        }
    }
}
