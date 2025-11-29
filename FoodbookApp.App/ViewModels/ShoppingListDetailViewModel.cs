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

    public ObservableCollection<Ingredient> UncheckedItems { get; } = new();
    public ObservableCollection<Ingredient> CheckedItems { get; } = new();

    public ObservableCollection<IngredientGroup> Groups { get; } = new();
    private IngredientGroup? _uncheckedGroup;
    private IngredientGroup? _checkedGroup;
    
    public bool HasCheckedItems => CheckedItems.Count > 0;
    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set { if (_isEditing != value) { _isEditing = value; OnPropertyChanged(); } }
    }

    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set { if (_hasUnsavedChanges != value) { _hasUnsavedChanges = value; OnPropertyChanged(); } }
    }

    private void MarkDirty() => HasUnsavedChanges = true;

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand ItemDraggedCommand { get; }
    public ICommand ItemDraggedOverCommand { get; }
    public ICommand ItemDragLeaveCommand { get; }
    public ICommand ItemDroppedCommand { get; }
    public ICommand SaveAllCommand { get; }

    public ICommand ItemDroppedBeforeCommand { get; private set; }
    public ICommand ItemDroppedAfterCommand { get; private set; }

    private const bool AUTO_SAVE_ENABLED = false;
    public event Action<Ingredient>? ItemEditingCompleted;

    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<Ingredient>(RemoveItem);
        MoveUpCommand = new Command<Ingredient>(async (item) => await MoveItemUpAsync(item));
        MoveDownCommand = new Command<Ingredient>(async (item) => await MoveItemDownAsync(item));
        SaveAllCommand = new Command(async () => await ManualSaveAllAsync());

        ItemDraggedCommand = new Command<Ingredient>(OnItemDragged);
        ItemDraggedOverCommand = new Command<Ingredient>(OnItemDraggedOver);
        ItemDragLeaveCommand = new Command<Ingredient>(OnItemDragLeave);
        ItemDroppedCommand = new Command<Ingredient>(async (item) => await OnItemDroppedAsync(item));
        ItemDroppedBeforeCommand = new Command<Ingredient>(async (item) => await ReorderItemsAsync(_itemBeingDragged ?? null, item, insertAfter: false));
        ItemDroppedAfterCommand = new Command<Ingredient>(async (item) => await ReorderItemsAsync(_itemBeingDragged ?? null, item, insertAfter: true));

        if (AUTO_SAVE_ENABLED)
            ItemEditingCompleted += async (item) => await SaveItemStateAsync(item);

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
            if (item.IsChecked) CheckedItems.Add(item); else UncheckedItems.Add(item);
        }
        Groups.Clear(); _uncheckedGroup = null; _checkedGroup = null; EnsureGroups();
        HasUnsavedChanges = false;
    }

    private async void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Ingredient item) return;

        if (e.PropertyName == nameof(Ingredient.IsChecked))
        {
            if (item.IsChecked)
            {
                if (UncheckedItems.Contains(item)) { UncheckedItems.Remove(item); CheckedItems.Add(item); }
            }
            else
            {
                if (CheckedItems.Contains(item)) { CheckedItems.Remove(item); UncheckedItems.Add(item); }
            }
        }

        if (e.PropertyName == nameof(Ingredient.IsChecked) ||
            e.PropertyName == nameof(Ingredient.Unit) ||
            e.PropertyName == nameof(Ingredient.Name) ||
            e.PropertyName == nameof(Ingredient.Quantity))
        {
            MarkDirty();
        }

        if (AUTO_SAVE_ENABLED && (e.PropertyName == nameof(Ingredient.IsChecked) ||
                                  e.PropertyName == nameof(Ingredient.Unit) ||
                                  e.PropertyName == nameof(Ingredient.Name) ||
                                  e.PropertyName == nameof(Ingredient.Quantity)))
        {
            await SaveItemStateAsync(item);
        }
    }

    private async Task ManualSaveAllAsync()
    {
        try
        {
            PrepareForSave();
            await SaveAllStatesAsync();
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Manual save error: {ex.Message}");
        }
    }

    private void PrepareForSave()
    {
        for (int i = 0; i < UncheckedItems.Count; i++) UncheckedItems[i].Order = i;
        for (int i = 0; i < CheckedItems.Count; i++) CheckedItems[i].Order = i;
    }

    private async Task SaveItemStateAsync(Ingredient item)
    {
        try
        {
            var savedId = await _shoppingListService.SaveShoppingListItemStateAsync(_currentPlanId, item.Id, item.Order, item.Name, item.Unit, item.IsChecked, item.Quantity);
            if (item.Id != savedId) { item.Id = savedId; System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Updated item '{item.Name}' Id: {savedId}"); }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving shopping list item state: {ex.Message}"); }
    }

    public async Task SaveAllStatesAsync()
    {
        try
        {
            var allItems = UncheckedItems.Concat(CheckedItems).ToList();
            await _shoppingListService.SaveAllShoppingListStatesAsync(_currentPlanId, allItems);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving all shopping list states: {ex.Message}"); }
    }

    private void AddItem()
    {
        var newItem = new Ingredient { Name = string.Empty, Quantity = 1, Unit = Unit.Piece, Order = UncheckedItems.Count };
        newItem.PropertyChanged += OnItemPropertyChanged;
        UncheckedItems.Add(newItem);
        MarkDirty();
    }

    private async void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        item.PropertyChanged -= OnItemPropertyChanged;
        var removed = UncheckedItems.Remove(item) || CheckedItems.Remove(item);
        if (removed)
        {
            MarkDirty();
            if (item.Id > 0)
            {
                try { await _shoppingListService.RemoveShoppingListItemByIdAsync(item.Id); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error removing item by Id: {ex.Message}"); }
                item.Id = 0; // ensure not matched later
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
            if (draggedInUnchecked != targetInUnchecked) return;
            var collection = draggedInUnchecked ? UncheckedItems : CheckedItems;
            var from = collection.IndexOf(draggedItem); var to = collection.IndexOf(targetItem);
            if (from == -1 || to == -1) return;
            int insertIndex = to + (insertAfter ? 1 : 0);
            if (from == insertIndex || from == insertIndex - 1) return;
            collection.RemoveAt(from);
            if (from < insertIndex) insertIndex--;
            insertIndex = Math.Clamp(insertIndex, 0, collection.Count);
            collection.Insert(insertIndex, draggedItem);
            MarkDirty();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error reordering items: {ex.Message}"); }
    }

    public async Task MoveItemUpAsync(Ingredient? item)
    {
        if (item == null) return;
        try
        {
            var collection = UncheckedItems.Contains(item) ? UncheckedItems : CheckedItems;
            var idx = collection.IndexOf(item);
            if (idx > 0)
            {
                collection.RemoveAt(idx); collection.Insert(idx - 1, item); MarkDirty();
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error moving item up: {ex.Message}"); }
    }

    public async Task MoveItemDownAsync(Ingredient? item)
    {
        if (item == null) return;
        try
        {
            var collection = UncheckedItems.Contains(item) ? UncheckedItems : CheckedItems;
            var idx = collection.IndexOf(item);
            if (idx >= 0 && idx < collection.Count - 1)
            {
                collection.RemoveAt(idx); collection.Insert(idx + 1, item); MarkDirty();
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error moving item down: {ex.Message}"); }
    }

    private void OnItemDragged(Ingredient item)
    {
        if (item == null) return;
        item.IsBeingDragged = true; _itemBeingDragged = item;
    }

    private void OnItemDraggedOver(Ingredient item)
    {
        if (item == null) return;
        if (item == _itemBeingDragged) item.IsBeingDragged = false;
        item.IsBeingDraggedOver = item != _itemBeingDragged;
    }

    private void OnItemDragLeave(Ingredient item)
    {
        if (item == null) return;
        item.IsBeingDraggedOver = false; item.ShowInsertBefore = false; item.ShowInsertAfter = false;
    }

    private async Task OnItemDroppedAsync(Ingredient item)
    {
        if (item == null || _itemBeingDragged == null) return;
        try
        {
            var move = _itemBeingDragged; var before = item;
            if (move == before) return;
            var draggedInUnchecked = UncheckedItems.Contains(move);
            var targetInUnchecked = UncheckedItems.Contains(before);
            if (draggedInUnchecked != targetInUnchecked) return;
            var collection = draggedInUnchecked ? UncheckedItems : CheckedItems;
            int idx = collection.IndexOf(before);
            if (idx >= 0 && idx < collection.Count)
            {
                collection.Remove(move); collection.Insert(idx, move); MarkDirty();
                move.IsBeingDragged = false; before.IsBeingDraggedOver = false; before.ShowInsertBefore = false; before.ShowInsertAfter = false;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error in OnItemDroppedAsync: {ex.Message}"); }
        finally { _itemBeingDragged = null; }
    }

    public void OnItemEditingCompleted(Ingredient item) => ItemEditingCompleted?.Invoke(item);
    public void DiscardChanges() => HasUnsavedChanges = false;

    /// <summary>
    /// Change unit for a given ingredient in a controlled, testable way.
    /// Encapsulates logic instead of handling it in the page code-behind.
    /// </summary>
    public void ChangeUnit(Ingredient? item, Unit newUnit)
    {
        if (item == null) return;
        if (item.Unit == newUnit) return; // no change
        var old = item.Unit;
        item.Unit = newUnit; // triggers PropertyChanged + MarkDirty via OnItemPropertyChanged
        System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Unit changed: {old} -> {newUnit}");
        // If PropertyChanged did not mark (e.g., event unsubscribed), ensure dirty state
        if (!HasUnsavedChanges) MarkDirty();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
