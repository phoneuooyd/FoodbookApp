using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel : INotifyPropertyChanged
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;
    private int _currentPlanId;
    private Ingredient? _itemBeingDragged;

    // Dwie oddzielne kolekcje
    public ObservableCollection<Ingredient> UncheckedItems { get; } = new();
    public ObservableCollection<Ingredient> CheckedItems { get; } = new();
    
    // W³aœciwoœæ do sprawdzania czy s¹ zebrane produkty
    public bool HasCheckedItems => CheckedItems.Count > 0;
    
    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ItemDraggedCommand { get; }
    public ICommand ItemDraggedOverCommand { get; }
    public ICommand ItemDragLeaveCommand { get; }
    public ICommand ItemDroppedCommand { get; }

    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<Ingredient>(RemoveItem);
        MoveUpCommand = new Command<Ingredient>(async (item) => await MoveItemUpAsync(item));
        MoveDownCommand = new Command<Ingredient>(async (item) => await MoveItemDownAsync(item));
        
        // Drag and drop commands
        ItemDraggedCommand = new Command<Ingredient>(OnItemDragged);
        ItemDraggedOverCommand = new Command<Ingredient>(OnItemDraggedOver);
        ItemDragLeaveCommand = new Command<Ingredient>(OnItemDragLeave);
        ItemDroppedCommand = new Command<Ingredient>(async (item) => await OnItemDroppedAsync(item));
        
        // S³uchaj zmian w kolekcjach
        UncheckedItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCheckedItems));
        CheckedItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCheckedItems));
    }

    public async Task LoadAsync(int planId)
    {
        _currentPlanId = planId;
        var plan = await _planService.GetPlanAsync(planId);
        if (plan == null) return;

        // Use the new method that includes checked state
        var items = await _shoppingListService.GetShoppingListWithCheckedStateAsync(planId);
        
        UncheckedItems.Clear();
        CheckedItems.Clear();
        
        foreach (var item in items)
        {
            // Dodaj obs³ugê zmiany stanu CheckBox
            item.PropertyChanged += OnItemPropertyChanged;
            
            if (item.IsChecked)
                CheckedItems.Add(item);
            else
                UncheckedItems.Add(item);
        }
    }

    private async void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Ingredient.IsChecked) && sender is Ingredient item)
        {
            if (item.IsChecked)
            {
                // Przenoszenie z UncheckedItems do CheckedItems
                if (UncheckedItems.Contains(item))
                {
                    UncheckedItems.Remove(item);
                    CheckedItems.Add(item);
                }
            }
            else
            {
                // Przenoszenie z CheckedItems do UncheckedItems
                if (CheckedItems.Contains(item))
                {
                    CheckedItems.Remove(item);
                    UncheckedItems.Add(item);
                }
            }

            // Save the state immediately when changed
            await SaveItemStateAsync(item);
        }
    }

    private async Task SaveItemStateAsync(Ingredient item)
    {
        try
        {
            await _shoppingListService.SaveShoppingListItemStateAsync(
                _currentPlanId, 
                item.Name, 
                item.Unit, 
                item.IsChecked, 
                item.Quantity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving shopping list item state: {ex.Message}");
        }
    }

    public async Task SaveAllStatesAsync()
    {
        try
        {
            var allItems = UncheckedItems.Concat(CheckedItems).ToList();
            await _shoppingListService.SaveAllShoppingListStatesAsync(_currentPlanId, allItems);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving all shopping list states: {ex.Message}");
        }
    }

    public async Task ReorderItemsAsync(Ingredient draggedItem, Ingredient targetItem)
    {
        try
        {
            // Determine which collection the items belong to
            var draggedInUnchecked = UncheckedItems.Contains(draggedItem);
            var targetInUnchecked = UncheckedItems.Contains(targetItem);

            // Only allow reordering within the same collection (checked/unchecked)
            if (draggedInUnchecked != targetInUnchecked)
                return;

            var targetCollection = draggedInUnchecked ? UncheckedItems : CheckedItems;

            var draggedIndex = targetCollection.IndexOf(draggedItem);
            var targetIndex = targetCollection.IndexOf(targetItem);

            if (draggedIndex != -1 && targetIndex != -1 && draggedIndex != targetIndex)
            {
                // Remove the dragged item
                targetCollection.RemoveAt(draggedIndex);

                // Adjust target index if needed
                if (draggedIndex < targetIndex)
                    targetIndex--;

                // Insert at new position
                targetCollection.Insert(targetIndex, draggedItem);

                // Notify UI about changes
                OnPropertyChanged(nameof(UncheckedItems));
                OnPropertyChanged(nameof(CheckedItems));

                await SaveOrderAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reordering items: {ex.Message}");
        }
    }

    public async Task SaveOrderAsync()
    {
        try
        {
            // Assign order indices to all items
            for (int i = 0; i < UncheckedItems.Count; i++)
            {
                UncheckedItems[i].Order = i;
            }
            
            for (int i = 0; i < CheckedItems.Count; i++)
            {
                CheckedItems[i].Order = i;
            }

            // Save all items with their new order
            var allItems = UncheckedItems.Concat(CheckedItems).ToList();
            await _shoppingListService.SaveAllShoppingListStatesAsync(_currentPlanId, allItems);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving item order: {ex.Message}");
        }
    }

    private void AddItem()
    {
        var newItem = new Ingredient { Name = string.Empty, Quantity = 0, Unit = Unit.Gram };
        newItem.PropertyChanged += OnItemPropertyChanged;
        UncheckedItems.Add(newItem);
    }

    private void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        
        // Usuñ event handler
        item.PropertyChanged -= OnItemPropertyChanged;
        
        // Usuñ z odpowiedniej kolekcji
        UncheckedItems.Remove(item);
        CheckedItems.Remove(item);
    }

    public async Task MoveItemUpAsync(Ingredient? item)
    {
        if (item == null) return;

        try
        {
            var collection = UncheckedItems.Contains(item) ? UncheckedItems : CheckedItems;
            var currentIndex = collection.IndexOf(item);
            
            if (currentIndex > 0)
            {
                collection.RemoveAt(currentIndex);
                collection.Insert(currentIndex - 1, item);
                
                await SaveOrderAsync();
                System.Diagnostics.Debug.WriteLine($"Moved {item.Name} up from index {currentIndex} to {currentIndex - 1}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error moving item up: {ex.Message}");
        }
    }

    public async Task MoveItemDownAsync(Ingredient? item)
    {
        if (item == null) return;

        try
        {
            var collection = UncheckedItems.Contains(item) ? UncheckedItems : CheckedItems;
            var currentIndex = collection.IndexOf(item);
            
            if (currentIndex >= 0 && currentIndex < collection.Count - 1)
            {
                collection.RemoveAt(currentIndex);
                collection.Insert(currentIndex + 1, item);
                
                await SaveOrderAsync();
                System.Diagnostics.Debug.WriteLine($"Moved {item.Name} down from index {currentIndex} to {currentIndex + 1}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error moving item down: {ex.Message}");
        }
    }

    private void OnItemDragged(Ingredient item)
    {
        if (item == null) return;
        
        System.Diagnostics.Debug.WriteLine($"ItemDragged: {item.Name}");
        item.IsBeingDragged = true;
        _itemBeingDragged = item;
    }

    private void OnItemDraggedOver(Ingredient item)
    {
        if (item == null) return;
        
        System.Diagnostics.Debug.WriteLine($"ItemDraggedOver: {item.Name}");
        
        // Reset drag state for the dragged item when it's over another item
        if (item == _itemBeingDragged)
        {
            item.IsBeingDragged = false;
        }
        
        // Only show drag over state if it's not the same item being dragged
        item.IsBeingDraggedOver = item != _itemBeingDragged;
    }

    private void OnItemDragLeave(Ingredient item)
    {
        if (item == null) return;
        
        System.Diagnostics.Debug.WriteLine($"ItemDragLeave: {item.Name}");
        item.IsBeingDraggedOver = false;
    }

    private async Task OnItemDroppedAsync(Ingredient item)
    {
        if (item == null || _itemBeingDragged == null) return;
        
        try
        {
            var itemToMove = _itemBeingDragged;
            var itemToInsertBefore = item;
            
            if (itemToMove == itemToInsertBefore) return;
            
            // Check if items are in the same collection
            var draggedInUnchecked = UncheckedItems.Contains(itemToMove);
            var targetInUnchecked = UncheckedItems.Contains(itemToInsertBefore);
            
            if (draggedInUnchecked != targetInUnchecked) return; // Can't move between collections
            
            var collection = draggedInUnchecked ? UncheckedItems : CheckedItems;
            
            int insertAtIndex = collection.IndexOf(itemToInsertBefore);
            if (insertAtIndex >= 0 && insertAtIndex < collection.Count)
            {
                collection.Remove(itemToMove);
                collection.Insert(insertAtIndex, itemToMove);
                
                // Reset drag states
                itemToMove.IsBeingDragged = false;
                itemToInsertBefore.IsBeingDraggedOver = false;
                
                await SaveOrderAsync();
                
                System.Diagnostics.Debug.WriteLine($"ItemDropped: [{itemToMove.Name}] => [{itemToInsertBefore.Name}], target index = [{insertAtIndex}]");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnItemDroppedAsync: {ex.Message}");
        }
        finally
        {
            _itemBeingDragged = null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
