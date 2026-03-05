using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Sharpnado.CollectionView.ViewModels; // Sharpnado DragAndDropInfo

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel : INotifyPropertyChanged, IHasUnsavedChanges
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;
    private Guid _currentPlanId;
    private Ingredient? _itemBeingDragged;
    
    // Header items for the flat list
    private ShoppingListHeader? _toBuyHeader;
    private ShoppingListHeader? _collectedHeader;

    public ObservableCollection<Ingredient> UncheckedItems { get; } = new();
    public ObservableCollection<Ingredient> CheckedItems { get; } = new();
    
    // Flat list for Sharpnado CollectionView - contains both headers and items
    public ObservableCollection<object> FlatItems { get; } = new();

    public ObservableCollection<IngredientGroup> Groups { get; } = new();
    private IngredientGroup? _uncheckedGroup;
    private IngredientGroup? _checkedGroup;
    
    // For CollectionView grouping (legacy - kept for compatibility)
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

    // Sharpnado CollectionView drag-and-drop commands
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

        // Sharpnado drag-and-drop commands with DragAndDropInfo parameter
        DragStartedCommand = new Command<DragAndDropInfo>(OnDragStarted);
        DragEndedCommand = new Command<DragAndDropInfo>(OnDragEnded);
        ItemTappedCommand = new Command<object>(OnItemTapped);

        if (AUTO_SAVE_ENABLED)
            ItemEditingCompleted += async (item) => await SaveItemStateAsync(item);

        UncheckedItems.CollectionChanged += (s, e) => 
        {
            OnPropertyChanged(nameof(HasCheckedItems));
            UpdateHeaderVisibility();
        };
        CheckedItems.CollectionChanged += (s, e) => 
        {
            OnPropertyChanged(nameof(HasCheckedItems));
            UpdateHeaderVisibility();
        };
    }

    #region Sharpnado Drag-and-Drop Handlers

    /// <summary>
    /// Called when drag starts - receives DragAndDropInfo with From index and Content
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
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Drag started: '{ing.Name}' at index {info.From}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] OnDragStarted error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when drag ends - receives DragAndDropInfo with From and To indices
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

            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Drag ended: from {info.From} to {info.To}");

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
    /// Syncs the reordered FlatItems back to UncheckedItems and CheckedItems
    /// </summary>
    private void SyncFlatItemsToSourceCollections()
    {
        try
        {
            // Extract Ingredient items from FlatItems (skip headers) and update their Order
            var ingredients = FlatItems.OfType<Ingredient>().ToList();
            
            // Update Order property based on position in FlatItems (global order)
            for (int i = 0; i < ingredients.Count; i++)
            {
                ingredients[i].Order = i;
            }
            
            // Separate by IsChecked status while preserving FlatItems order
            var newUncheckedItems = ingredients.Where(i => !i.IsChecked).ToList();
            var newCheckedItems = ingredients.Where(i => i.IsChecked).ToList();

            // Update source collections without triggering full rebuild
            UncheckedItems.Clear();
            foreach (var item in newUncheckedItems)
                UncheckedItems.Add(item);

            CheckedItems.Clear();
            foreach (var item in newCheckedItems)
                CheckedItems.Add(item);

            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Synced: {UncheckedItems.Count} unchecked, {CheckedItems.Count} checked (orders updated)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] SyncFlatItemsToSourceCollections error: {ex.Message}");
        }
    }

    /// <summary>
    /// Called when an item is tapped (optional, for future use)
    /// </summary>
    private void OnItemTapped(object? item)
    {
        if (item is Ingredient ing)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Item tapped: {ing.Name}");
            // Could toggle IsChecked or open edit mode
        }
    }

    #endregion

    /// <summary>
    /// Updates the visibility of header items based on whether sections have items
    /// </summary>
    private void UpdateHeaderVisibility()
    {
        if (_toBuyHeader != null)
            _toBuyHeader.IsVisible = UncheckedItems.Count > 0;
        if (_collectedHeader != null)
            _collectedHeader.IsVisible = CheckedItems.Count > 0;
    }

    /// <summary>
    /// Rebuilds the flat list from UncheckedItems + CheckedItems with headers
    /// </summary>
    private void RebuildFlatItems()
    {
        try
        {
            FlatItems.Clear();
            
            // Create or reuse header items
            if (_toBuyHeader == null)
            {
                var toBuyTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.ToBuy ?? "To Buy";
                _toBuyHeader = new ShoppingListHeader 
                { 
                    Title = toBuyTitle, 
                    IsCheckedSection = false,
                    IsVisible = UncheckedItems.Count > 0
                };
            }
            
            if (_collectedHeader == null)
            {
                var collectedTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.Collected ?? "Collected";
                _collectedHeader = new ShoppingListHeader 
                { 
                    Title = collectedTitle, 
                    IsCheckedSection = true,
                    IsVisible = CheckedItems.Count > 0
                };
            }
            
            // Add "To Buy" section
            if (UncheckedItems.Count > 0)
            {
                FlatItems.Add(_toBuyHeader);
                foreach (var item in UncheckedItems)
                    FlatItems.Add(item);
            }
            
            // Add "Collected" section
            if (CheckedItems.Count > 0)
            {
                FlatItems.Add(_collectedHeader);
                foreach (var item in CheckedItems)
                    FlatItems.Add(item);
            }
                
            OnPropertyChanged(nameof(FlatItems));
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] RebuildFlatItems: {FlatItems.Count} items ({UncheckedItems.Count} unchecked, {CheckedItems.Count} checked)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] RebuildFlatItems error: {ex.Message}");
        }
    }

    /// <summary>
    /// Moves an item between sections without full rebuild to preserve scroll position.
    /// This is called when IsChecked changes on an item.
    /// </summary>
    private void MoveItemBetweenSections(Ingredient item, bool wasChecked)
    {
        try
        {
            // Find current position in FlatItems
            int currentIndex = -1;
            for (int i = 0; i < FlatItems.Count; i++)
            {
                if (FlatItems[i] == item)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                // Item not found, do full rebuild
                RebuildFlatItems();
                UpdateAllItemOrders();
                return;
            }

            // Remove from current position
            FlatItems.RemoveAt(currentIndex);

            if (item.IsChecked)
            {
                // Move from Unchecked to Checked section
                // Ensure Collected header exists
                if (_collectedHeader == null)
                {
                    var collectedTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.Collected ?? "Collected";
                    _collectedHeader = new ShoppingListHeader 
                    { 
                        Title = collectedTitle, 
                        IsCheckedSection = true,
                        IsVisible = true
                    };
                }

                // Find or add Collected header
                int collectedHeaderIndex = -1;
                for (int i = 0; i < FlatItems.Count; i++)
                {
                    if (FlatItems[i] is ShoppingListHeader h && h.IsCheckedSection)
                    {
                        collectedHeaderIndex = i;
                        break;
                    }
                }

                if (collectedHeaderIndex < 0)
                {
                    // Add Collected header at the end
                    FlatItems.Add(_collectedHeader);
                    collectedHeaderIndex = FlatItems.Count - 1;
                }

                // Insert item after the header (at the end of checked items)
                FlatItems.Add(item);

                // Remove ToBuy header if no more unchecked items
                if (UncheckedItems.Count == 0 && _toBuyHeader != null)
                {
                    FlatItems.Remove(_toBuyHeader);
                }
            }
            else
            {
                // Move from Checked to Unchecked section
                // Ensure ToBuy header exists
                if (_toBuyHeader == null)
                {
                    var toBuyTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.ToBuy ?? "To Buy";
                    _toBuyHeader = new ShoppingListHeader 
                    { 
                        Title = toBuyTitle, 
                        IsCheckedSection = false,
                        IsVisible = true
                    };
                }

                // Find or add ToBuy header
                int toBuyHeaderIndex = -1;
                for (int i = 0; i < FlatItems.Count; i++)
                {
                    if (FlatItems[i] is ShoppingListHeader h && !h.IsCheckedSection)
                    {
                        toBuyHeaderIndex = i;
                        break;
                    }
                }

                if (toBuyHeaderIndex < 0)
                {
                    // Add ToBuy header at the beginning
                    FlatItems.Insert(0, _toBuyHeader);
                    toBuyHeaderIndex = 0;
                }

                // Find the end of unchecked section (before Collected header or end of list)
                int insertIndex = toBuyHeaderIndex + 1;
                for (int i = toBuyHeaderIndex + 1; i < FlatItems.Count; i++)
                {
                    if (FlatItems[i] is ShoppingListHeader h && h.IsCheckedSection)
                        break;
                    insertIndex = i + 1;
                }

                // Insert at the end of unchecked items
                FlatItems.Insert(insertIndex, item);

                // Remove Collected header if no more checked items
                if (CheckedItems.Count == 0 && _collectedHeader != null)
                {
                    FlatItems.Remove(_collectedHeader);
                }
            }

            // Update Order property for all items after move
            UpdateAllItemOrders();

            UpdateHeaderVisibility();
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Moved item '{item.Name}' - IsChecked: {item.IsChecked}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] MoveItemBetweenSections error: {ex.Message}");
            // Fallback to full rebuild
            RebuildFlatItems();
            UpdateAllItemOrders();
        }
    }

    /// <summary>
    /// Updates the Order property for all items based on their position in FlatItems
    /// </summary>
    private void UpdateAllItemOrders()
    {
        var ingredients = FlatItems.OfType<Ingredient>().ToList();
        for (int i = 0; i < ingredients.Count; i++)
        {
            ingredients[i].Order = i;
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

    public async Task LoadAsync(Guid planId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ========== LoadAsync STARTED ==========");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Loading plan: {planId}");
            
            _currentPlanId = planId;
            var plan = await _planService.GetPlanAsync(planId);
            
            if (plan == null) 
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Plan {planId} not found");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Plan loaded: {plan.Name} ({plan.Type})");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Plan dates: {plan.StartDate:yyyy-MM-dd} to {plan.EndDate:yyyy-MM-dd}");

            var items = await _shoppingListService.GetShoppingListWithCheckedStateAsync(planId);
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Retrieved {items.Count} items from service");
            
            UnsubscribeAllItems();
            UncheckedItems.Clear();
            CheckedItems.Clear();
            
            // Reset headers
            _toBuyHeader = null;
            _collectedHeader = null;
            
            int uncheckedCount = 0;
            int checkedCount = 0;
            int manuallyAddedCount = 0;
            
            foreach (var item in items)
            {
                // Log item details for diagnostics
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Loading item: [{item.Order}] {item.Name} | {item.Quantity} {item.Unit} | Checked: {item.IsChecked} | Id: {item.Id}");
                
                // Track manually added items (those with empty GUID before save would have been assigned new IDs)
                if (item.Id == Guid.Empty)
                {
                    manuallyAddedCount++;
                }
                
                item.PropertyChanged += OnItemPropertyChanged;
                
                if (item.IsChecked) 
                {
                    CheckedItems.Add(item);
                    checkedCount++;
                }
                else 
                {
                    UncheckedItems.Add(item);
                    uncheckedCount++;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Load summary:");
            System.Diagnostics.Debug.WriteLine($"  - Total items: {items.Count}");
            System.Diagnostics.Debug.WriteLine($"  - Unchecked: {uncheckedCount}");
            System.Diagnostics.Debug.WriteLine($"  - Checked: {checkedCount}");
            System.Diagnostics.Debug.WriteLine($"  - Items without ID: {manuallyAddedCount}");
            
            Groups.Clear(); 
            _uncheckedGroup = null; 
            _checkedGroup = null;
            AllItemsGrouped.Clear(); 
            _uncheckedItemsGroup = null; 
            _checkedItemsGroup = null;
            
            EnsureGroups();
            
            HasUnsavedChanges = false;
            
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ========== LoadAsync COMPLETED ==========");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? ERROR in LoadAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Exception type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void UnsubscribeAllItems()
    {
        foreach (var item in UncheckedItems.Concat(CheckedItems))
            item.PropertyChanged -= OnItemPropertyChanged;
    }

    private async void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not Ingredient item) return;

        if (e.PropertyName == nameof(Ingredient.IsChecked))
        {
            bool wasChecked = !item.IsChecked;
            
            MainThread.BeginInvokeOnMainThread(() =>
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
                
                MoveItemBetweenSections(item, wasChecked);
            });
        }

        if (e.PropertyName == nameof(Ingredient.IsChecked) ||
            e.PropertyName == nameof(Ingredient.Unit) ||
            e.PropertyName == nameof(Ingredient.Name) ||
            e.PropertyName == nameof(Ingredient.Quantity))
        {
            MarkDirty();
        }

        if (e.PropertyName == nameof(Ingredient.Unit) && item.Id != Guid.Empty)
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
        if (item == null || item.Id == Guid.Empty) return;

        await _shoppingListService.SaveShoppingListItemStateAsync(
            _currentPlanId, item.Id, item.Order, item.Name, item.Unit, item.IsChecked, item.Quantity);
    }

    private async Task ManualSaveAllAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ========== SAVE OPERATION STARTED ==========");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] PlanId: {_currentPlanId}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Total items to save: {FlatItems.OfType<Ingredient>().Count()}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Unchecked items: {UncheckedItems.Count}, Checked items: {CheckedItems.Count}");
                        
            // ? VALIDATION: Check if plan ID is valid
            if (_currentPlanId == Guid.Empty)
            {
                var errorMsg = "Nieprawid³owy identyfikator planu. Nie mo¿na zapisaæ listy zakupów.";
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? VALIDATION ERROR: {errorMsg}");
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert("B³¹d walidacji", errorMsg, "OK");
                return;
            }

            // ? VALIDATION: Validate all items before saving
            var allItems = FlatItems.OfType<Ingredient>().ToList();
            var invalidItems = new List<string>();
            
            for (int i = 0; i < allItems.Count; i++)
            {
                var item = allItems[i];
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    invalidItems.Add($"Pozycja {i + 1}: brak nazwy");
                }
                if (item.Quantity <= 0)
                {
                    invalidItems.Add($"Pozycja {i + 1} ({item.Name}): nieprawid³owa iloœæ ({item.Quantity})");
                }
            }

            if (invalidItems.Any())
            {
                var errorMsg = $"Znaleziono nieprawid³owe elementy:\n{string.Join("\n", invalidItems.Take(5))}";
                if (invalidItems.Count > 5)
                    errorMsg += $"\n...i {invalidItems.Count - 5} wiêcej";
                
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? VALIDATION ERROR: {invalidItems.Count} invalid items");
                foreach (var err in invalidItems)
                    System.Diagnostics.Debug.WriteLine($"  - {err}");
                
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                    "B³¹d walidacji", 
                    errorMsg + "\n\nUsuñ lub popraw nieprawid³owe pozycje przed zapisaniem.", 
                    "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ? Validation passed");
            
            // ? PREPARE: Assign order to all items
            PrepareForSave();
            
            // Log items being saved for diagnostics
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] Items to save:");
            foreach (var item in allItems)
            {
                System.Diagnostics.Debug.WriteLine($"  - [{item.Order}] {item.Name} | {item.Quantity} {item.Unit} | Checked: {item.IsChecked} | Id: {item.Id}");
            }
            
            // ? SAVE: Attempt to save with detailed error handling
            try
            {
                await SaveAllStatesAsync();
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ? SaveAllStatesAsync completed successfully");
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                // Database-specific error (e.g., constraint violation, connection issue)
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? DATABASE ERROR during save: {dbEx.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Inner exception: {dbEx.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Stack trace: {dbEx.StackTrace}");
                
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                    "B³¹d bazy danych",
                    $"Nie uda³o siê zapisaæ listy zakupów do bazy danych.\n\n" +
                    $"Szczegó³y: {dbEx.InnerException?.Message ?? dbEx.Message}\n\n" +
                    $"Spróbuj ponownie lub skontaktuj siê z obs³ug¹.",
                    "OK");
                return;
            }
            catch (InvalidOperationException invEx)
            {
                // Invalid operation (e.g., context disposed, concurrent access)
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? INVALID OPERATION ERROR during save: {invEx.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Stack trace: {invEx.StackTrace}");
                
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                    "B³¹d operacji",
                    $"Wyst¹pi³ problem z zapisem listy zakupów.\n\n" +
                    $"Szczegó³y: {invEx.Message}\n\n" +
                    $"Spróbuj ponownie.",
                    "OK");
                return;
            }
            catch (Exception saveEx)
            {
                // Generic save error
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? UNEXPECTED ERROR during SaveAllStatesAsync: {saveEx.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Exception type: {saveEx.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Stack trace: {saveEx.StackTrace}");
                
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                    "B³¹d zapisu",
                    $"Nie uda³o siê zapisaæ listy zakupów.\n\n" +
                    $"Szczegó³y: {saveEx.Message}\n\n" +
                    $"Typ b³êdu: {saveEx.GetType().Name}",
                    "OK");
                return;
            }
            
            // ? SUCCESS: Clear dirty flag and notify
            HasUnsavedChanges = false;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ? HasUnsavedChanges cleared");
            
            // ? NOTIFY: Raise SaveCompletedAsync event to trigger navigation
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] Raising SaveCompletedAsync event");
            if (SaveCompletedAsync != null)
            {
                try
                {
                    var handlers = SaveCompletedAsync.GetInvocationList().Cast<Func<Task>>();
                    var tasks = handlers.Select(h => h());
                    await Task.WhenAll(tasks);
                    System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ? SaveCompletedAsync event handlers executed");
                }
                catch (Exception eventEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ?? Error in SaveCompletedAsync event handler: {eventEx.Message}");
                    // Don't block the save operation if event handler fails
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ========== SAVE OPERATION COMPLETED SUCCESSFULLY ==========");
        }
        catch (Exception ex)
        {
            // ? CATCH-ALL: Handle any unexpected errors not caught by inner try-catch
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? CRITICAL ERROR in ManualSaveAllAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Exception type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Stack trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Inner exception: {ex.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Inner stack trace: {ex.InnerException.StackTrace}");
            }
            
            try
            {
                await Microsoft.Maui.Controls.Shell.Current.DisplayAlert(
                    "B³¹d krytyczny", 
                    $"Wyst¹pi³ nieoczekiwany b³¹d podczas zapisywania listy zakupów.\n\n" +
                    $"Szczegó³y: {ex.Message}\n\n" +
                    $"Jeœli problem siê powtarza, spróbuj:\n" +
                    $"1. Usun¹æ puste lub nieprawid³owe pozycje\n" +
                    $"2. Zrestartowaæ aplikacjê\n" +
                    $"3. Skontaktowaæ siê z obs³ug¹ techniczn¹",
                    "OK");
            }
            catch (Exception displayEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? Failed to display error alert: {displayEx.Message}");
            }
        }
    }

    private void AddItem()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] AddItem called");
            
            // Calculate the new item's order based on current FlatItems
            var currentIngredientCount = FlatItems.OfType<Ingredient>().Count();
            
            // ? IMPROVEMENT: Create new item with valid default values
            var newItem = new Ingredient 
            { 
                Id = Guid.Empty, // Will be assigned by service on save
                Name = string.Empty, 
                Quantity = 1, 
                Unit = Unit.Piece, 
                Order = currentIngredientCount,
                IsChecked = false,
                Calories = 0,
                Protein = 0,
                Fat = 0,
                Carbs = 0,
                UnitWeight = 1.0
            };
            
            newItem.PropertyChanged += OnItemPropertyChanged;
            UncheckedItems.Add(newItem);
            
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] New item added with Order={currentIngredientCount}");
            
            // Add item to FlatItems incrementally
            if (_toBuyHeader == null)
            {
                var toBuyTitle = FoodbookApp.Localization.ShoppingListDetailPageResources.ToBuy ?? "To Buy";
                _toBuyHeader = new ShoppingListHeader 
                { 
                    Title = toBuyTitle, 
                    IsCheckedSection = false,
                    IsVisible = true
                };
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] Created ToBuy header");
            }

            // Find ToBuy header or add it
            int toBuyHeaderIndex = -1;
            for (int i = 0; i < FlatItems.Count; i++)
            {
                if (FlatItems[i] is ShoppingListHeader h && !h.IsCheckedSection)
                {
                    toBuyHeaderIndex = i;
                    break;
                }
            }

            if (toBuyHeaderIndex < 0)
            {
                FlatItems.Insert(0, _toBuyHeader);
                toBuyHeaderIndex = 0;
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] Added ToBuy header to FlatItems");
            }

            // Find end of unchecked section
            int insertIndex = toBuyHeaderIndex + 1;
            for (int i = toBuyHeaderIndex + 1; i < FlatItems.Count; i++)
            {
                if (FlatItems[i] is ShoppingListHeader h && h.IsCheckedSection)
                    break;
                insertIndex = i + 1;
            }

            FlatItems.Insert(insertIndex, newItem);
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] New item inserted into FlatItems at index {insertIndex}");
            
            // Update Order for this new item based on its position in FlatItems
            UpdateAllItemOrders();
            
            MarkDirty();
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailVM] ? AddItem completed - marked dirty");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] ? ERROR in AddItem: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] Stack trace: {ex.StackTrace}");
        }
    }

    private async void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        item.PropertyChanged -= OnItemPropertyChanged;
        var removed = UncheckedItems.Remove(item) || CheckedItems.Remove(item);
        if (removed)
        {
            // Remove from FlatItems
            FlatItems.Remove(item);
            
            // Remove headers if sections are empty
            if (UncheckedItems.Count == 0 && _toBuyHeader != null)
                FlatItems.Remove(_toBuyHeader);
            if (CheckedItems.Count == 0 && _collectedHeader != null)
                FlatItems.Remove(_collectedHeader);
            
            MarkDirty();
            if (item.Id != Guid.Empty)
            {
                try { await _shoppingListService.RemoveShoppingListItemByIdAsync(item.Id); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error removing item by Id: {ex.Message}"); }
                item.Id = Guid.Empty;
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
                UpdateAllItemOrders();
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
                UpdateAllItemOrders();
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

    private void PrepareForSave()
    {
        // Use the FlatItems order as the source of truth for global ordering
        var ingredients = FlatItems.OfType<Ingredient>().ToList();
        for (int i = 0; i < ingredients.Count; i++)
        {
            ingredients[i].Order = i;
        }
        
        System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] PrepareForSave: assigned global order to {ingredients.Count} items");
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
            // Use FlatItems order to ensure correct order is preserved
            var allItems = FlatItems.OfType<Ingredient>().ToList();
            await _shoppingListService.SaveAllShoppingListStatesAsync(_currentPlanId, allItems);
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailVM] SaveAllStatesAsync: saved {allItems.Count} items in FlatItems order");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error saving all shopping list states: {ex.Message}"); }
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
