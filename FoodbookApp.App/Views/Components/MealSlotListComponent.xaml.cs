using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public partial class MealSlotListComponent : ContentView
{
    public static readonly BindableProperty MealSlotsProperty =
        BindableProperty.Create(
            nameof(MealSlots),
            typeof(ObservableCollection<MealSlotViewModel>),
            typeof(MealSlotListComponent),
            null,
            propertyChanged: OnMealSlotsChanged);

    public static readonly BindableProperty AddMealCommandProperty =
        BindableProperty.Create(nameof(AddMealCommand), typeof(ICommand), typeof(MealSlotListComponent));

    public static readonly BindableProperty OpenMealDetailCommandProperty =
        BindableProperty.Create(nameof(OpenMealDetailCommand), typeof(ICommand), typeof(MealSlotListComponent));

    private readonly PageThemeHelper _themeHelper;

    public MealSlotListComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();
        BindingContext = this;

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    public ObservableCollection<MealSlotViewModel>? MealSlots
    {
        get => (ObservableCollection<MealSlotViewModel>?)GetValue(MealSlotsProperty);
        set => SetValue(MealSlotsProperty, value);
    }

    public ICommand? AddMealCommand
    {
        get => (ICommand?)GetValue(AddMealCommandProperty);
        set => SetValue(AddMealCommandProperty, value);
    }

    public ICommand? OpenMealDetailCommand
    {
        get => (ICommand?)GetValue(OpenMealDetailCommandProperty);
        set => SetValue(OpenMealDetailCommandProperty, value);
    }

    public bool IsEmpty => MealSlots == null || MealSlots.Count == 0;

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
        UnsubscribeCollection(MealSlots);
    }

    private static void OnMealSlotsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not MealSlotListComponent component)
        {
            return;
        }

        component.UnsubscribeCollection(oldValue as ObservableCollection<MealSlotViewModel>);
        component.SubscribeCollection(newValue as ObservableCollection<MealSlotViewModel>);
        component.OnPropertyChanged(nameof(component.IsEmpty));
    }

    private void SubscribeCollection(ObservableCollection<MealSlotViewModel>? collection)
    {
        if (collection == null)
        {
            return;
        }

        collection.CollectionChanged += OnCollectionChanged;
    }

    private void UnsubscribeCollection(ObservableCollection<MealSlotViewModel>? collection)
    {
        if (collection == null)
        {
            return;
        }

        collection.CollectionChanged -= OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsEmpty));
    }
}
