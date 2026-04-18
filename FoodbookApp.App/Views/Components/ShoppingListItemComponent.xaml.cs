using System.Collections;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Utils;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public partial class ShoppingListItemComponent : ContentView
{
    public sealed class UnitSelectionChangedEventArgs : EventArgs
    {
        public UnitSelectionChangedEventArgs(Unit? selectedUnit)
        {
            SelectedUnit = selectedUnit;
        }

        public Unit? SelectedUnit { get; }
    }

    public static readonly BindableProperty RemoveItemCommandProperty =
        BindableProperty.Create(nameof(RemoveItemCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty ChangeUnitCommandProperty =
        BindableProperty.Create(nameof(ChangeUnitCommand), typeof(ICommand), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty UnitsSourceProperty =
        BindableProperty.Create(nameof(UnitsSource), typeof(IEnumerable), typeof(ShoppingListItemComponent));

    public static readonly BindableProperty ShowDragAndDropProperty =
        BindableProperty.Create(nameof(ShowDragAndDrop), typeof(bool), typeof(ShoppingListItemComponent), true);

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

    // Events for focus handling
    public event EventHandler? EntryFocused;
    public event EventHandler? EntryUnfocused;
    public event EventHandler<UnitSelectionChangedEventArgs>? UnitSelectionChanged;

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
        _ = ComponentAnimationHelper.AnimateEntranceAsync(ItemFrame, offsetY: 8);
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        _ = ComponentAnimationHelper.AnimateEmphasisAsync(ItemFrame, true);
        EntryFocused?.Invoke(this, EventArgs.Empty);
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        _ = ComponentAnimationHelper.AnimateEmphasisAsync(ItemFrame, false);
        EntryUnfocused?.Invoke(this, EventArgs.Empty);
    }

    private void OnUnitPickerSelectionChanged(object? sender, EventArgs e)
    {
        _ = ComponentAnimationHelper.AnimateSoftRefreshAsync(ItemFrame);

        Unit? selectedUnit = sender is SimplePicker picker && picker.SelectedItem is Unit unit
            ? unit
            : null;

        UnitSelectionChanged?.Invoke(this, new UnitSelectionChangedEventArgs(selectedUnit));
    }
}
