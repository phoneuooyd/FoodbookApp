using System.Windows.Input;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public partial class MealSlotItemComponent : ContentView
{
    public static readonly BindableProperty MealNameProperty =
        BindableProperty.Create(nameof(MealName), typeof(string), typeof(MealSlotItemComponent), string.Empty);

    public static readonly BindableProperty IngredientsSummaryProperty =
        BindableProperty.Create(nameof(IngredientsSummary), typeof(string), typeof(MealSlotItemComponent), string.Empty);

    public static readonly BindableProperty CaloriesProperty =
        BindableProperty.Create(nameof(Calories), typeof(double), typeof(MealSlotItemComponent), 0d, propertyChanged: OnCaloriesChanged);

    public static readonly BindableProperty ItemModelProperty =
        BindableProperty.Create(nameof(ItemModel), typeof(MealSlotViewModel), typeof(MealSlotItemComponent));

    public static readonly BindableProperty AddMealCommandProperty =
        BindableProperty.Create(nameof(AddMealCommand), typeof(ICommand), typeof(MealSlotItemComponent));

    public static readonly BindableProperty OpenMealDetailCommandProperty =
        BindableProperty.Create(nameof(OpenMealDetailCommand), typeof(ICommand), typeof(MealSlotItemComponent));

    private readonly PageThemeHelper _themeHelper;
    private Color _circleColor = Color.FromArgb("#444444");

    public MealSlotItemComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();
        BindingContext = this;

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    public string MealName
    {
        get => (string)GetValue(MealNameProperty);
        set => SetValue(MealNameProperty, value);
    }

    public string IngredientsSummary
    {
        get => (string)GetValue(IngredientsSummaryProperty);
        set => SetValue(IngredientsSummaryProperty, value);
    }

    public double Calories
    {
        get => (double)GetValue(CaloriesProperty);
        set => SetValue(CaloriesProperty, value);
    }

    public MealSlotViewModel? ItemModel
    {
        get => (MealSlotViewModel?)GetValue(ItemModelProperty);
        set => SetValue(ItemModelProperty, value);
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

    public Color CircleColor
    {
        get => _circleColor;
        private set
        {
            if (_circleColor == value)
            {
                return;
            }

            _circleColor = value;
            OnPropertyChanged();
        }
    }

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
        UpdateCircleColor();
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
    }

    private static void OnCaloriesChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MealSlotItemComponent component)
        {
            component.UpdateCircleColor();
        }
    }

    private void UpdateCircleColor()
    {
        CircleColor = Calories > 0
            ? TryGetColor("DietStatsProteinColor", Color.FromArgb("#8B72FF"))
            : Color.FromArgb("#5A5A61");
    }

    private void OnOpenMealTapped(object? sender, TappedEventArgs e)
    {
        if (ItemModel == null)
        {
            return;
        }

        if (OpenMealDetailCommand?.CanExecute(ItemModel) == true)
        {
            OpenMealDetailCommand.Execute(ItemModel);
        }
    }

    private void OnAddMealTapped(object? sender, TappedEventArgs e)
    {
        if (ItemModel == null)
        {
            return;
        }

        if (AddMealCommand?.CanExecute(ItemModel) == true)
        {
            AddMealCommand.Execute(ItemModel);
        }
    }

    private static Color TryGetColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return fallback;
    }
}
