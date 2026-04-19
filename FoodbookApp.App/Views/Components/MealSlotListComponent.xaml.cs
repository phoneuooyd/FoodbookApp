using System.Windows.Input;
using Foodbook.ViewModels;

namespace Foodbook.Views.Components;

public partial class MealSlotListComponent : ContentView
{
    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(
            nameof(Data),
            typeof(MealSlotListData),
            typeof(MealSlotListComponent),
            MealSlotListData.Empty,
            propertyChanged: OnDataChanged);

    public static readonly BindableProperty AddMealCommandProperty =
        BindableProperty.Create(nameof(AddMealCommand), typeof(ICommand), typeof(MealSlotListComponent));

    public static readonly BindableProperty OpenMealDetailCommandProperty =
        BindableProperty.Create(nameof(OpenMealDetailCommand), typeof(ICommand), typeof(MealSlotListComponent));

    public MealSlotListComponent()
    {
        InitializeComponent();
    }

    public MealSlotListData Data
    {
        get => (MealSlotListData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
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

    public bool IsEmpty => Data.IsEmpty;

    private static void OnDataChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not MealSlotListComponent component)
        {
            return;
        }

        component.OnPropertyChanged(nameof(component.IsEmpty));
    }
}
