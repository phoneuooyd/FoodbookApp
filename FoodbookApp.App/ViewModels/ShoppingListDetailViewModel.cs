using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Sharpnado.CollectionView.ViewModels;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel : INotifyPropertyChanged
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;
    private int _currentPlanId;
    private Ingredient? _itemBeingDragged;

    public ObservableCollection<Ingredient> UncheckedItems { get; } = new();
    public ObservableCollection<Ingredient> CheckedItems { get; } = new();
    
    // ? Flat list for Sharpnado CollectionView - contains section headers and items
    public ObservableCollection<IShoppingListItem> FlatItems { get; } = new();
    
    // ? Section headers for group titles
    private ShoppingListSectionHeader? _toBuyHeader;
    private ShoppingListSectionHeader? _collectedHeader;

    public ObservableCollection<IngredientGroup> Groups { get; } = new();
    private IngredientGroup? _uncheckedGroup;
    private IngredientGroup? _checkedGroup;
    
    // For CollectionView grouping
    public ObservableCollection<ShoppingItemGroup> AllItemsGrouped { get; } = new();
    private ShoppingItemGroup? _uncheckedItemsGroup;
    private ShoppingItemGroup? _checkedItemsGroup;
    
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
    public ICommand SaveAllCommand { get; }
    public ICommand ChangeUnitCommand { get; }

    // ? Sharpnado CollectionView drag-and-drop commands
    public ICommand DragStartedCommand { get; }
    public ICommand DragEndedCommand { get; }
    public ICommand ItemTappedCommand { get; }

    private const bool AUTO_SAVE_ENABLED = false;
    public event Action<Ingredient>? ItemEditingCompleted;

    // Event raised when save completes successfully so page can navigate back
    public event Func<Task>? SaveCompletedAsync;

    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<Ingredient>(RemoveItem);
        MoveUpCommand = new Command<Ingredient>(async (item) => await MoveItemUpAsync(item));
        MoveDownCommand = new Command<Ingredient>(async (item) => await MoveItemDownAsync(item));
        SaveAllCommand = new Command(async () => await ManualSaveAllAsync());
        ChangeUnitCommand = new Command<(Ingredient, Unit)>((tuple) => ChangeUnit(tuple.Item1, tuple.Item2));

        // ? Sharpnado drag-and-drop commands with DragAndDropInfo parameter
        DragStartedCommand = new Command<DragAndDropInfo>(OnDragStarted);
        DragEndedCommand = new Command<DragAndDropInfo>(OnDragEnded);
        ItemTappedCommand = new Command<object>(OnItemTapped);

        if (AUTO_SAVE_ENABLED)
            ItemEditingCompleted += async (item) => await SaveItemStateAsync(item);

        UncheckedItems.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(HasCheckedItems)); UpdateHeaderCounts(); };
        CheckedItems.CollectionChanged += (s, e) => { OnPropertyChanged(nameof(HasCheckedItems)); UpdateHeaderCounts(); };
    }

    /// <summary>
    /// ? Updates header item counts for visibility
    /// </summary>
    private void UpdateHeaderCounts()
    {
        if (_toBuyHeader != null)
            _toBuyHeader.ItemCount = UncheckedItems.Count;
        if (_collectedHeader != null)
            _collectedHeader.ItemCount = CheckedItems.Count;
    }

    #region Sharpnado Drag-and-Drop Handlers

    /// <summary>
    /// ? Called when drag starts - receives DragAndDropInfo with From index and Content
    /// </summary>
    private void OnDragStarted(DragAndDropInfo? info)
    {
        try
        {
            if (info == null) return;

            if (info.Content is Ingredient ing)
            {
                _itemBeingDragged = ing;
                ing.IsBeingDragged = true;
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Drag started: '{ing.Name}' at index {info.From}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] OnDragStarted error: {ex.Message}");
        }
    }

    /// <summary>
    /// ? Called when drag ends - receives DragAndDropInfo with From and To indices
    /// Sharpnado automatically reorders the FlatItems collection, we sync back to source collections
    /// </summary>
    private void OnDragEnded(DragAndDropInfo? info)
    {
        try
        {
            if (info == null)
            {
                ClearDragStates();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Drag ended: from {info.From} to {info.To}");

            // Sharpnado has already reordered FlatItems, sync back to source collections
            SyncFlatItemsToSourceCollections();
            
            MarkDirty();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] OnDragEnded error: {ex.Message}");
        }
        finally
        {
            _itemBeingDragged = null;
            ClearDragStates();
        }
    }

    /// <summary>
    /// ? Syncs the reordered FlatItems back to UncheckedItems and CheckedItems
    /// </summary>
    private void SyncFlatItemsToSourceCollections()
    {
        try
        {
            // Extract only Ingredient items (skip headers) by their IsChecked status
            var allIngredients = FlatItems.OfType<Ingredient>().ToList();
            var newUncheckedItems = allIngredients.Where(i => !i.IsChecked).ToList();
            var newCheckedItems = allIngredients.Where(i => i.IsChecked).ToList();

            // Update source collections
            UncheckedItems.Clear();
            foreach (var item in newUncheckedItems)
                UncheckedItems.Add(item);

            CheckedItems.Clear();
            foreach (var item in newCheckedItems)
                CheckedItems.Add(item);

            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Synced: {UncheckedItems.Count} unchecked, {CheckedItems.Count} checked");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] SyncFlatItemsToSourceCollections error: {ex.Message}");
        }
    }

    /// <summary>
    /// ? Called when an item is tapped
    /// </summary>
    private void OnItemTapped(object? item)
    {
        if (item is Ingredient ing)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Item tapped: {ing.Name}");
        }
        // Ignore header taps
    }

    #endregion

    /// <summary>
    /// ? Rebuilds the flat list from headers + UncheckedItems + CheckedItems
    /// </summary>
    private void RebuildFlatItems()
    {
        try
        {
            FlatItems.Clear();
            
            // Initialize section headers if needed
            if (_toBuyHeader == null)
            {
                var toBuyTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.ToBuy ?? "Do zebrania";
                _toBuyHeader = new ShoppingListSectionHeader(toBuyTitle, false);
            }
            if (_collectedHeader == null)
            {
                var collectedTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.Collected ?? "Zebrane";
                _collectedHeader = new ShoppingListSectionHeader(collectedTitle, true);
            }
            
            // Update counts
            _toBuyHeader.ItemCount = UncheckedItems.Count;
            _collectedHeader.ItemCount = CheckedItems.Count;
            
            // Add "To Buy" section header (always visible if there are items)
            if (UncheckedItems.Count > 0)
            {
                FlatItems.Add(_toBuyHeader);
                foreach (var item in UncheckedItems)
                    FlatItems.Add(item);
            }
            
            // Add "Collected" section header (only if there are checked items)
            if (CheckedItems.Count > 0)
            {
                FlatItems.Add(_collectedHeader);
                foreach (var item in CheckedItems)
                    FlatItems.Add(item);
            }
                
            OnPropertyChanged(nameof(FlatItems));
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] RebuildFlatItems: {FlatItems.Count} total ({UncheckedItems.Count} unchecked, {CheckedItems.Count} checked)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] RebuildFlatItems error: {ex.Message}");
        }
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
        
        if (_uncheckedItemsGroup == null)
        {
            var toBuyTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.ToBuy ?? "To Buy";
            _uncheckedItemsGroup = new ShoppingItemGroup(toBuyTitle, false, UncheckedItems);
            AllItemsGrouped.Add(_uncheckedItemsGroup);
        }
        if (_checkedItemsGroup == null)
        {
            var collectedTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.Collected ?? "Collected";
            _checkedItemsGroup = new ShoppingItemGroup(collectedTitle, true, CheckedItems);
            AllItemsGrouped.Add(_checkedItemsGroup);
        }
        
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(AllItemsGrouped));
        OnPropertyChanged(nameof(HasCheckedItems));
        
        RebuildFlatItems();
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
        Groups.Clear(); _uncheckedGroup = null; _checkedGroup = null;
        AllItemsGrouped.Clear(); _uncheckedItemsGroup = null; _checkedItemsGroup = null;
        _toBuyHeader = null; _collectedHeader = null; // Reset headers
        EnsureGroups();
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
            
            RebuildFlatItems();
        }

        if (e.PropertyName == nameof(Ingredient.IsChecked) ||
            e.PropertyName == nameof(Ingredient.Unit) ||
            e.PropertyName == nameof(Ingredient.Name) ||
            e.PropertyName == nameof(Ingredient.Quantity))
        {
            MarkDirty();
        }

        if (e.PropertyName == nameof(Ingredient.Unit) && item.Id > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Unit changed for '{item.Name}' - saving to DB immediately");
            _ = SaveUnitChangeToDatabase(item);
        }

        if (AUTO_SAVE_ENABLED && (e.PropertyName == nameof(Ingredient.IsChecked) ||
                                  e.PropertyName == nameof(Ingredient.Unit) ||
                                  e.PropertyName == nameof(Ingredient.Name) ||
                                  e.PropertyName == nameof(Ingredient.Quantity)))
        {
            await SaveItemStateAsync(item);
        }
    }

    private async Task SaveUnitChangeToDatabase(Ingredient item)
    {
        if (item == null || item.Id <= 0) return;

        try
        {
            await _shoppingListService.SaveShoppingListItemStateAsync(
                _currentPlanId, item.Id, item.Order, item.Name, item.Unit, item.IsChecked, item.Quantity);
            
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Unit change saved: '{item.Name}' -> {item.Unit}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Failed to save unit change: {ex.Message}");
        }
    }

    private async Task ManualSaveAllAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ManualSaveAllAsync started");
            
            PrepareForSave();
            await SaveAllStatesAsync();
            HasUnsavedChanges = false;
            
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] Save completed successfully");
            
            if (SaveCompletedAsync != null)
            {
                var handlers = SaveCompletedAsync.GetInvocationList().Cast<Func<Task>>();
                var tasks = handlers.Select(h => h());
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Manual save error: {ex.Message}");
            
            try
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                    "B³¹d", $"Nie uda³o siê zapisaæ listy zakupów: {ex.Message}", "OK");
            }
            catch { }
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
            if (item.Id != savedId) { item.Id = savedId; }
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
        RebuildFlatItems();
        MarkDirty();
    }

    private async void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        item.PropertyChanged -= OnItemPropertyChanged;
        var removed = UncheckedItems.Remove(item) || CheckedItems.Remove(item);
        if (removed)
        {
            RebuildFlatItems();
            MarkDirty();
            if (item.Id > 0)
            {
                try { await _shoppingListService.RemoveShoppingListItemByIdAsync(item.Id); } 
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error removing item by Id: {ex.Message}"); }
                item.Id = 0;
            }
        }
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
                collection.RemoveAt(idx); 
                collection.Insert(idx - 1, item);
                RebuildFlatItems();
                MarkDirty();
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
                collection.RemoveAt(idx); 
                collection.Insert(idx + 1, item);
                RebuildFlatItems();
                MarkDirty();
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error moving item down: {ex.Message}"); }
    }

    private void ClearDragStates()
    {
        foreach (var ing in UncheckedItems.Concat(CheckedItems))
        {
            ing.IsBeingDragged = false;
            ing.IsBeingDraggedOver = false;
        }
    }

    public void DiscardChanges() => HasUnsavedChanges = false;

    public void ChangeUnit(Ingredient? item, Unit newUnit)
    {
        if (item == null) return;
        if (item.Unit == newUnit) return;
        
        var old = item.Unit;
        item.Unit = newUnit;
        
        System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ChangeUnit called: {old} -> {newUnit}");
        
        if (!HasUnsavedChanges) MarkDirty();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
