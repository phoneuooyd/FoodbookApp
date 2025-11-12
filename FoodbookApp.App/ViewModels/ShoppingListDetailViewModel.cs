using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel : INotifyPropertyChanged
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;
    private int _currentPlanId;
    private Ingredient? _itemBeingDragged;

    // Two separate backing collections (data model)
    public ObservableCollection<Ingredient> UncheckedItems { get; } = new();
    public ObservableCollection<Ingredient> CheckedItems { get; } = new();

    // Grouped view for UI (prevents nested ScrollView + multiple CollectionViews causing jumpiness)
    public ObservableCollection<IngredientGroup> Groups { get; } = new();
    private IngredientGroup? _uncheckedGroup;
    private IngredientGroup? _checkedGroup;
    
    public bool HasCheckedItems => CheckedItems.Count > 0;
    
    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing != value)
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ItemDraggedCommand { get; }
    public ICommand ItemDraggedOverCommand { get; }
    public ICommand ItemDragLeaveCommand { get; }
    public ICommand ItemDroppedCommand { get; }

    // Optional commands kept for compatibility with XAML bindings if needed
    public ICommand ItemDroppedBeforeCommand { get; private set; }
    public ICommand ItemDroppedAfterCommand { get; private set; }

    public event Action<Ingredient>? ItemEditingCompleted;

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

        ItemDroppedBeforeCommand = new Command<Ingredient>(async (item) => await ReorderItemsAsync(_itemBeingDragged ?? null, item, insertAfter: false));
        ItemDroppedAfterCommand = new Command<Ingredient>(async (item) => await ReorderItemsAsync(_itemBeingDragged ?? null, item, insertAfter: true));
        
        // Subscribe to editing completed event
        ItemEditingCompleted += async (item) => await SaveItemStateAsync(item);
        
        // Listen to changes in collections for HasCheckedItems re-evaluation
        UncheckedItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCheckedItems));
        CheckedItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCheckedItems));
    }

    private void EnsureGroups()
    {
        if (_uncheckedGroup == null)
        {
            _uncheckedGroup = new IngredientGroup("ToBuy", UncheckedItems, isUnchecked: true);
            Groups.Add(_uncheckedGroup);
        }

        if (_checkedGroup == null)
        {
            _checkedGroup = new IngredientGroup("Collected", CheckedItems, isUnchecked: false);
            Groups.Add(_checkedGroup);
        }

        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(HasCheckedItems));
    }

    public async Task LoadAsync(int planId)
    {
        _currentPlanId = planId;
        var plan = await _planService.GetPlanAsync(planId);
        if (plan == null) return;

        var items = await _shoppingListService.GetShoppingListWithCheckedStateAsync(planId);
        
        UncheckedItems.Clear();
        CheckedItems.Clear();
        
        foreach (var item in items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            if (item.IsChecked)
                CheckedItems.Add(item);
            else
                UncheckedItems.Add(item);
        }

        // Recreate groups after data load (ensures a single CV in UI)
        Groups.Clear();
        _uncheckedGroup = null;
        _checkedGroup = null;
        EnsureGroups();
    }

    private async void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is Ingredient item)
        {
            // Save on any relevant property change
            if (e.PropertyName == nameof(Ingredient.IsChecked))
            {
                if (item.IsChecked)
                {
                    if (UncheckedItems.Contains(item))
                    {
                        UncheckedItems.Remove(item);
                        CheckedItems.Add(item);
                    }
                }
                else
                {
                    if (CheckedItems.Contains(item))
                    {
                        CheckedItems.Remove(item);
                        UncheckedItems.Add(item);
                    }
                }
            }

            // Save on any change to Name, Quantity, Unit, IsChecked
            if (e.PropertyName == nameof(Ingredient.IsChecked) ||
                e.PropertyName == nameof(Ingredient.Unit) ||
                e.PropertyName == nameof(Ingredient.Name) ||
                e.PropertyName == nameof(Ingredient.Quantity))
            {
                await SaveItemStateAsync(item);
            }
        }
    }

    // Public method for code-behind to call on any UI change
    public async Task SaveItemImmediatelyAsync(Ingredient item)
    {
        await SaveItemStateAsync(item);
    }

    private async Task SaveItemStateAsync(Ingredient item)
    {
        try
        {
            await _shoppingListService.SaveShoppingListItemStateAsync(
                _currentPlanId,
                item.Id,      // przekazuj Id
                item.Order,   // przekazuj Order
                item.Name,
                item.Unit,
                item.IsChecked,
                item.Quantity
            );
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
            // Save all, including new/empty items
            await _shoppingListService.SaveAllShoppingListStatesAsync(_currentPlanId, allItems);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving all shopping list states: {ex.Message}");
        }
    }

    private void AddItem()
    {
        var newItem = new Ingredient 
        { 
            Name = string.Empty,
            Quantity = 1,
            Unit = Unit.Piece,
            Order = UncheckedItems.Count
        };
        newItem.PropertyChanged += OnItemPropertyChanged;
        UncheckedItems.Add(newItem);
        // Save immediately after adding
        _ = SaveItemStateAsync(newItem);
    }

    private async void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        
        item.PropertyChanged -= OnItemPropertyChanged;
        
        var removedFromUnchecked = UncheckedItems.Remove(item);
        var removedFromChecked = CheckedItems.Remove(item);
        
        if (removedFromUnchecked || removedFromChecked)
        {
            try
            {
                await _shoppingListService.RemoveShoppingListItemAsync(_currentPlanId, item.Name, item.Unit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing shopping list item: {ex.Message}");
            }
        }
    }

    public async Task ReorderItemsAsync(Ingredient draggedItem, Ingredient targetItem, bool insertAfter = false)
    {
        try
        {
            if (draggedItem == null || targetItem == null) return;

            var draggedInUnchecked = UncheckedItems.Contains(draggedItem);
            var targetInUnchecked = UncheckedItems.Contains(targetItem);

            if (draggedInUnchecked != targetInUnchecked)
                return;

            var targetCollection = draggedInUnchecked ? UncheckedItems : CheckedItems;

            var draggedIndex = targetCollection.IndexOf(draggedItem);
            var targetIndex = targetCollection.IndexOf(targetItem);

            if (draggedIndex == -1 || targetIndex == -1) return;

            // Compute insertion index depending on insertAfter flag
            int insertIndex = targetIndex + (insertAfter ? 1 : 0);

            if (draggedIndex == insertIndex || draggedIndex == insertIndex - 1)
                return; // no-op (already in desired position)

            targetCollection.RemoveAt(draggedIndex);

            // Adjust insertIndex if item was before target and removed
            if (draggedIndex < insertIndex)
                insertIndex--;

            insertIndex = Math.Clamp(insertIndex, 0, targetCollection.Count);
            targetCollection.Insert(insertIndex, draggedItem);

            OnPropertyChanged(nameof(UncheckedItems));
            OnPropertyChanged(nameof(CheckedItems));

            await SaveOrderAsync();
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
            for (int i = 0; i < UncheckedItems.Count; i++)
            {
                UncheckedItems[i].Order = i;
            }
            
            for (int i = 0; i < CheckedItems.Count; i++)
            {
                CheckedItems[i].Order = i;
            }

            var allItems = UncheckedItems.Concat(CheckedItems).ToList();
            await _shoppingListService.SaveAllShoppingListStatesAsync(_currentPlanId, allItems);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving item order: {ex.Message}");
        }
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
        
        if (item == _itemBeingDragged)
        {
            item.IsBeingDragged = false;
        }
        
        item.IsBeingDraggedOver = item != _itemBeingDragged;
    }

    private void OnItemDragLeave(Ingredient item)
    {
        if (item == null) return;
        
        System.Diagnostics.Debug.WriteLine($"ItemDragLeave: {item.Name}");
        item.IsBeingDraggedOver = false;
        item.ShowInsertBefore = false;
        item.ShowInsertAfter = false;
    }

    private async Task OnItemDroppedAsync(Ingredient item)
    {
        if (item == null || _itemBeingDragged == null) return;
        
        try
        {
            var itemToMove = _itemBeingDragged;
            var itemToInsertBefore = item;
            
            if (itemToMove == itemToInsertBefore) return;
            
            var draggedInUnchecked = UncheckedItems.Contains(itemToMove);
            var targetInUnchecked = UncheckedItems.Contains(itemToInsertBefore);
            
            if (draggedInUnchecked != targetInUnchecked) return;
            
            var collection = draggedInUnchecked ? UncheckedItems : CheckedItems;
            
            int insertAtIndex = collection.IndexOf(itemToInsertBefore);
            if (insertAtIndex >= 0 && insertAtIndex < collection.Count)
            {
                collection.Remove(itemToMove);
                collection.Insert(insertAtIndex, itemToMove);
                
                itemToMove.IsBeingDragged = false;
                itemToInsertBefore.IsBeingDraggedOver = false;
                itemToInsertBefore.ShowInsertBefore = false;
                itemToInsertBefore.ShowInsertAfter = false;
                
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

    public void OnItemEditingCompleted(Ingredient item)
    {
        ItemEditingCompleted?.Invoke(item);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
