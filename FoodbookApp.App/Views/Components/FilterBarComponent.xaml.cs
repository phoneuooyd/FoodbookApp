namespace Foodbook.Views.Components;

public partial class FilterBarComponent : ContentView
{
    public static readonly BindableProperty SelectedFilterProperty =
        BindableProperty.Create(nameof(SelectedFilter), typeof(ViewModels.FilterMode), typeof(FilterBarComponent), ViewModels.FilterMode.Day, propertyChanged: OnStateChanged);

    public static readonly BindableProperty AvailablePlansProperty =
        BindableProperty.Create(nameof(AvailablePlans), typeof(IEnumerable<Models.Plan>), typeof(FilterBarComponent), null);

    public static readonly BindableProperty SelectedPlanProperty =
        BindableProperty.Create(nameof(SelectedPlan), typeof(Models.Plan), typeof(FilterBarComponent), null, BindingMode.TwoWay);

    public static readonly BindableProperty SelectFilterCommandProperty =
        BindableProperty.Create(nameof(SelectFilterCommand), typeof(System.Windows.Input.ICommand), typeof(FilterBarComponent), null);

    public static readonly BindableProperty IsPlanPickerVisibleProperty =
        BindableProperty.Create(nameof(IsPlanPickerVisible), typeof(bool), typeof(FilterBarComponent), false);

    public static readonly BindableProperty IsCustomRangeVisibleProperty =
        BindableProperty.Create(nameof(IsCustomRangeVisible), typeof(bool), typeof(FilterBarComponent), false);

    public static readonly BindableProperty FilterStartDateProperty =
        BindableProperty.Create(nameof(FilterStartDate), typeof(DateTime), typeof(FilterBarComponent), DateTime.Today, BindingMode.TwoWay);

    public static readonly BindableProperty FilterEndDateProperty =
        BindableProperty.Create(nameof(FilterEndDate), typeof(DateTime), typeof(FilterBarComponent), DateTime.Today, BindingMode.TwoWay);

    public FilterBarComponent()
    {
        InitializeComponent();
    }

    public ViewModels.FilterMode SelectedFilter
    {
        get => (ViewModels.FilterMode)GetValue(SelectedFilterProperty);
        set => SetValue(SelectedFilterProperty, value);
    }

    public IEnumerable<Models.Plan>? AvailablePlans
    {
        get => (IEnumerable<Models.Plan>?)GetValue(AvailablePlansProperty);
        set => SetValue(AvailablePlansProperty, value);
    }

    public Models.Plan? SelectedPlan
    {
        get => (Models.Plan?)GetValue(SelectedPlanProperty);
        set => SetValue(SelectedPlanProperty, value);
    }

    public System.Windows.Input.ICommand? SelectFilterCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(SelectFilterCommandProperty);
        set => SetValue(SelectFilterCommandProperty, value);
    }

    public bool IsPlanPickerVisible
    {
        get => (bool)GetValue(IsPlanPickerVisibleProperty);
        set => SetValue(IsPlanPickerVisibleProperty, value);
    }

    public bool IsCustomRangeVisible
    {
        get => (bool)GetValue(IsCustomRangeVisibleProperty);
        set => SetValue(IsCustomRangeVisibleProperty, value);
    }

    public DateTime FilterStartDate
    {
        get => (DateTime)GetValue(FilterStartDateProperty);
        set => SetValue(FilterStartDateProperty, value);
    }

    public DateTime FilterEndDate
    {
        get => (DateTime)GetValue(FilterEndDateProperty);
        set => SetValue(FilterEndDateProperty, value);
    }

    public Color ChipDayColor => GetChipBackground(ViewModels.FilterMode.Day);

    public Color ChipDayTextColor => GetChipText(ViewModels.FilterMode.Day);

    public Color ChipWeekColor => GetChipBackground(ViewModels.FilterMode.Week);

    public Color ChipWeekTextColor => GetChipText(ViewModels.FilterMode.Week);

    public Color ChipMonthColor => GetChipBackground(ViewModels.FilterMode.Month);

    public Color ChipMonthTextColor => GetChipText(ViewModels.FilterMode.Month);

    public Color ChipCustomColor => GetChipBackground(ViewModels.FilterMode.Custom);

    public Color ChipCustomTextColor => GetChipText(ViewModels.FilterMode.Custom);

    public Color ChipPlanColor => GetChipBackground(ViewModels.FilterMode.Plan);

    public Color ChipPlanTextColor => GetChipText(ViewModels.FilterMode.Plan);

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FilterBarComponent component)
        {
            component.OnPropertyChanged(nameof(ChipDayColor));
            component.OnPropertyChanged(nameof(ChipDayTextColor));
            component.OnPropertyChanged(nameof(ChipWeekColor));
            component.OnPropertyChanged(nameof(ChipWeekTextColor));
            component.OnPropertyChanged(nameof(ChipMonthColor));
            component.OnPropertyChanged(nameof(ChipMonthTextColor));
            component.OnPropertyChanged(nameof(ChipCustomColor));
            component.OnPropertyChanged(nameof(ChipCustomTextColor));
            component.OnPropertyChanged(nameof(ChipPlanColor));
            component.OnPropertyChanged(nameof(ChipPlanTextColor));
        }
    }

    private Color GetChipBackground(ViewModels.FilterMode mode)
        => SelectedFilter == mode ? Color.FromArgb("#8B72FF") : Color.FromArgb("#338B72FF");

    private Color GetChipText(ViewModels.FilterMode mode)
        => SelectedFilter == mode ? Colors.White : Color.FromArgb("#AAAAAA");
}
