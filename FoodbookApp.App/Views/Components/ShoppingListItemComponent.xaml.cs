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
}
