using System.Collections;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public partial class ShoppingListItemComponent : ContentView
{
    public static readonly BindableProperty RemoveItemCommandProperty =
        BindableProperty.Create(nameof(RemoveItemCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty ChangeUnitCommandProperty =
        BindableProperty.Create(nameof(ChangeUnitCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty UnitsSourceProperty =
        BindableProperty.Create(nameof(UnitsSource), typeof(IEnumerable), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty ShowDragAndDropProperty =
        BindableProperty.Create(nameof(ShowDragAndDrop), typeof(bool), typeof(ShoppingListItemComponent), true);

    // Drag & Drop commands
    public static readonly BindableProperty DragStartingCommandProperty =
        BindableProperty.Create(nameof(DragStartingCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty DragOverCommandProperty =
        BindableProperty.Create(nameof(DragOverCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty DragLeaveCommandProperty =
        BindableProperty.Create(nameof(DragLeaveCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty DropCommandProperty =
        BindableProperty.Create(nameof(DropCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty ItemDroppedBeforeCommandProperty =
        BindableProperty.Create(nameof(ItemDroppedBeforeCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty ItemDroppedAfterCommandProperty =
        BindableProperty.Create(nameof(ItemDroppedAfterCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    private readonly PageThemeHelper _themeHelper;

    public ICommand? RemoveItemCommand
    {
        get => (ICommand?)GetValue(RemoveItemCommandProperty);
        set => SetValue(RemoveItemCommandProperty, value);
    }

    public ICommand? ChangeUnitCommand
    {
        get => (ICommand?)GetValue(ChangeUnitCommandProperty);
        set => SetValue(ChangeUnitCommandProperty, value);
    }

    public IEnumerable? UnitsSource
    {
        get => (IEnumerable?)GetValue(UnitsSourceProperty);
        set => SetValue(UnitsSourceProperty, value);
    }

    public bool ShowDragAndDrop
    {
        get => (bool)GetValue(ShowDragAndDropProperty);
        set => SetValue(ShowDragAndDropProperty, value);
    }

    public ICommand? DragStartingCommand
    {
        get => (ICommand?)GetValue(DragStartingCommandProperty);
        set => SetValue(DragStartingCommandProperty, value);
    }

    public ICommand? DragOverCommand
    {
        get => (ICommand?)GetValue(DragOverCommandProperty);
        set => SetValue(DragOverCommandProperty, value);
    }

    public ICommand? DragLeaveCommand
    {
        get => (ICommand?)GetValue(DragLeaveCommandProperty);
        set => SetValue(DragLeaveCommandProperty, value);
    }

    public ICommand? DropCommand
    {
        get => (ICommand?)GetValue(DropCommandProperty);
        set => SetValue(DropCommandProperty, value);
    }

    public ICommand? ItemDroppedBeforeCommand
    {
        get => (ICommand?)GetValue(ItemDroppedBeforeCommandProperty);
        set => SetValue(ItemDroppedBeforeCommandProperty, value);
    }

    public ICommand? ItemDroppedAfterCommand
    {
        get => (ICommand?)GetValue(ItemDroppedAfterCommandProperty);
        set => SetValue(ItemDroppedAfterCommandProperty, value);
    }

    // Events for focus handling
    public event EventHandler? EntryFocused;
    public event EventHandler? EntryUnfocused;
    public event EventHandler? UnitSelectionChanged;

    public ShoppingListItemComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        EntryFocused?.Invoke(this, EventArgs.Empty);
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        EntryUnfocused?.Invoke(this, EventArgs.Empty);
    }

    private void OnUnitPickerSelectionChanged(object? sender, EventArgs e)
    {
        UnitSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Drag & Drop handlers
    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (!ShowDragAndDrop) { e.Cancel = true; return; }

        // Store source item in drag data
        e.Data.Properties["SourceItem"] = BindingContext;

        if (DragStartingCommand?.CanExecute(BindingContext) == true)
        {
            DragStartingCommand.Execute(BindingContext);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!ShowDragAndDrop) return;
        
        // NEW: Show drop space below this item when dragging over the item itself
        if (BottomInsertZone != null && sender != BottomInsertZone)
        {
            BottomInsertZone.HeightRequest = 60; // Size of dragged item
            BottomInsertZone.BackgroundColor = Color.FromArgb("#15000000"); // Subtle hint
        }
        
        if (DragOverCommand?.CanExecute(BindingContext) == true)
        {
            DragOverCommand.Execute(BindingContext);
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (!ShowDragAndDrop) return;
        
        // NEW: Collapse drop space when leaving
        if (BottomInsertZone != null)
        {
            BottomInsertZone.HeightRequest = 0;
            BottomInsertZone.BackgroundColor = Colors.Transparent;
        }
        
        if (DragLeaveCommand?.CanExecute(BindingContext) == true)
        {
            DragLeaveCommand.Execute(BindingContext);
        }
    }

    private void OnDrop(object? sender, DropEventArgs e)
    {
        if (!ShowDragAndDrop) return;

        // Collapse space after drop
        if (BottomInsertZone != null)
        {
            BottomInsertZone.HeightRequest = 0;
            BottomInsertZone.BackgroundColor = Colors.Transparent;
        }

        e.Data.Properties.TryGetValue("SourceItem", out var source);
        
        // Use standard drop command (reorder to position of target)
        if (DropCommand?.CanExecute(BindingContext) == true)
        {
            DropCommand.Execute(BindingContext);
        }
    }

    private void OnTopInsertDragOver(object? sender, DragEventArgs e)
    {
        if (!ShowDragAndDrop) return;
        // Expand space ABOVE this item to 60px
        if (TopInsertZone != null)
        {
            TopInsertZone.HeightRequest = 60;
            TopInsertZone.BackgroundColor = Color.FromArgb("#15000000");
        }
        
        // Hide bottom space to avoid double indicators
        if (BottomInsertZone != null)
        {
            BottomInsertZone.HeightRequest = 0;
            BottomInsertZone.BackgroundColor = Colors.Transparent;
        }
    }

    private void OnTopInsertDragLeave(object? sender, DragEventArgs e)
    {
        // Collapse space back to 0
        if (TopInsertZone != null)
        {
            TopInsertZone.HeightRequest = 0;
            TopInsertZone.BackgroundColor = Colors.Transparent;
        }
    }

    private void OnTopInsertDrop(object? sender, DropEventArgs e)
    {
        if (!ShowDragAndDrop) return;
        // Collapse space after drop
        if (TopInsertZone != null)
        {
            TopInsertZone.HeightRequest = 0;
            TopInsertZone.BackgroundColor = Colors.Transparent;
        }

        e.Data.Properties.TryGetValue("SourceItem", out var source);
        
        // Drop BEFORE this item
        if (ItemDroppedBeforeCommand?.CanExecute(BindingContext) == true)
        {
            ItemDroppedBeforeCommand.Execute(BindingContext);
        }
    }

    private void OnBottomInsertDragOver(object? sender, DragEventArgs e)
    {
        if (!ShowDragAndDrop) return;
        // Expand space BELOW this item to 60px
        if (BottomInsertZone != null)
        {
            BottomInsertZone.HeightRequest = 60;
            BottomInsertZone.BackgroundColor = Color.FromArgb("#15000000");
        }
        
        // Hide top space to avoid double indicators
        if (TopInsertZone != null)
        {
            TopInsertZone.HeightRequest = 0;
            TopInsertZone.BackgroundColor = Colors.Transparent;
        }
    }

    private void OnBottomInsertDragLeave(object? sender, DragEventArgs e)
    {
        // Collapse space back to 0
        if (BottomInsertZone != null)
        {
            BottomInsertZone.HeightRequest = 0;
            BottomInsertZone.BackgroundColor = Colors.Transparent;
        }
    }

    private void OnBottomInsertDrop(object? sender, DropEventArgs e)
    {
        if (!ShowDragAndDrop) return;
        // Collapse space after drop
        if (BottomInsertZone != null)
        {
            BottomInsertZone.HeightRequest = 0;
            BottomInsertZone.BackgroundColor = Colors.Transparent;
        }

        e.Data.Properties.TryGetValue("SourceItem", out var source);
        
        // Drop AFTER this item
        if (ItemDroppedAfterCommand?.CanExecute(BindingContext) == true)
        {
            ItemDroppedAfterCommand.Execute(BindingContext);
        }
    }
}
