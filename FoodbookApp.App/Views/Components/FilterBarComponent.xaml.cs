using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using StatsFilterMode = Foodbook.ViewModels.FilterMode;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public sealed class FilterChipItem : INotifyPropertyChanged
{
    private Color _backgroundColor = Colors.Transparent;
    private Color _textColor = Colors.Gray;

    public StatsFilterMode Mode { get; init; }

    public string Title { get; init; } = string.Empty;

    public Color BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (_backgroundColor == value)
            {
                return;
            }

            _backgroundColor = value;
            OnPropertyChanged();
        }
    }

    public Color TextColor
    {
        get => _textColor;
        set
        {
            if (_textColor == value)
            {
                return;
            }

            _textColor = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class FilterBarComponent : ContentView
{
    private static readonly ResourceManager ResourceManager =
        new("FoodbookApp.Localization.DietStatisticsPageResources", typeof(FilterBarComponent).Assembly);

    public static readonly BindableProperty SelectedFilterProperty =
        BindableProperty.Create(
            nameof(SelectedFilter),
            typeof(StatsFilterMode),
            typeof(FilterBarComponent),
            StatsFilterMode.Day,
            BindingMode.TwoWay,
            propertyChanged: OnSelectedFilterChanged);

    public static readonly BindableProperty AvailablePlansProperty =
        BindableProperty.Create(
            nameof(AvailablePlans),
            typeof(IEnumerable<Plan>),
            typeof(FilterBarComponent));

    public static readonly BindableProperty SelectedPlanProperty =
        BindableProperty.Create(
            nameof(SelectedPlan),
            typeof(Plan),
            typeof(FilterBarComponent),
            null,
            BindingMode.TwoWay);

    public static readonly BindableProperty SelectFilterCommandProperty =
        BindableProperty.Create(
            nameof(SelectFilterCommand),
            typeof(ICommand),
            typeof(FilterBarComponent));

    public static readonly BindableProperty SelectPlanCommandProperty =
        BindableProperty.Create(
            nameof(SelectPlanCommand),
            typeof(ICommand),
            typeof(FilterBarComponent));

    private readonly PageThemeHelper _themeHelper;

    public FilterBarComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();

        Chips = new ObservableCollection<FilterChipItem>();
        BindingContext = this;

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    public ObservableCollection<FilterChipItem> Chips { get; }

    public StatsFilterMode SelectedFilter
    {
        get => (StatsFilterMode)GetValue(SelectedFilterProperty);
        set => SetValue(SelectedFilterProperty, value);
    }

    public IEnumerable<Plan>? AvailablePlans
    {
        get => (IEnumerable<Plan>?)GetValue(AvailablePlansProperty);
        set => SetValue(AvailablePlansProperty, value);
    }

    public Plan? SelectedPlan
    {
        get => (Plan?)GetValue(SelectedPlanProperty);
        set => SetValue(SelectedPlanProperty, value);
    }

    public ICommand? SelectFilterCommand
    {
        get => (ICommand?)GetValue(SelectFilterCommandProperty);
        set => SetValue(SelectFilterCommandProperty, value);
    }

    public ICommand? SelectPlanCommand
    {
        get => (ICommand?)GetValue(SelectPlanCommandProperty);
        set => SetValue(SelectPlanCommandProperty, value);
    }

    public bool IsPlanPickerVisible => SelectedFilter == StatsFilterMode.Plan;

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
        BuildChips();
        RefreshVisualState(animateSelectedChip: false);
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
    }

    private static void OnSelectedFilterChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not FilterBarComponent component)
        {
            return;
        }

        component.OnPropertyChanged(nameof(component.IsPlanPickerVisible));
        component.RefreshVisualState(animateSelectedChip: true);
    }

    private void BuildChips()
    {
        Chips.Clear();
        Chips.Add(new FilterChipItem { Mode = StatsFilterMode.Day, Title = Localize("FilterDay") });
        Chips.Add(new FilterChipItem { Mode = StatsFilterMode.Week, Title = Localize("FilterWeek") });
        Chips.Add(new FilterChipItem { Mode = StatsFilterMode.Month, Title = Localize("FilterMonth") });
        Chips.Add(new FilterChipItem { Mode = StatsFilterMode.Custom, Title = Localize("FilterCustom") });
        Chips.Add(new FilterChipItem { Mode = StatsFilterMode.Plan, Title = Localize("FilterPlan") });
    }

    private void RefreshVisualState(bool animateSelectedChip)
    {
        var activeBackground = TryGetColor("DietStatsChipActiveBackgroundColor", Color.FromArgb("#8B72FF"));
        var activeText = TryGetColor("DietStatsChipActiveTextColor", Colors.White);
        var inactiveBackground = TryGetColor("DietStatsChipInactiveBackgroundColor", Color.FromRgba(255, 255, 255, 0.12f));
        var inactiveText = TryGetColor("DietStatsChipInactiveTextColor", Color.FromArgb("#AFAFB8"));

        foreach (var chip in Chips)
        {
            var isSelected = chip.Mode == SelectedFilter;
            chip.BackgroundColor = isSelected ? activeBackground : inactiveBackground;
            chip.TextColor = isSelected ? activeText : inactiveText;
        }

        if (animateSelectedChip)
        {
            _ = AnimateSelectionAsync();
        }
    }

    private async Task AnimateSelectionAsync()
    {
        try
        {
            await this.FadeTo(0.92, 80, Easing.CubicOut);
            await this.ScaleTo(0.995, 80, Easing.CubicOut);
            await this.FadeTo(1, 120, Easing.CubicIn);
            await this.ScaleTo(1, 120, Easing.CubicIn);
        }
        catch
        {
        }
    }

    private void OnChipTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not FilterChipItem chip)
        {
            return;
        }

        SelectedFilter = chip.Mode;
        if (SelectFilterCommand?.CanExecute(chip.Mode) == true)
        {
            SelectFilterCommand.Execute(chip.Mode);
        }
    }

    private void OnPlanPickerChanged(object? sender, EventArgs e)
    {
        if (SelectedPlan == null)
        {
            return;
        }

        if (SelectPlanCommand?.CanExecute(SelectedPlan) == true)
        {
            SelectPlanCommand.Execute(SelectedPlan);
        }
    }

    private string Localize(string key)
    {
        return key switch
        {
            "FilterDay" => GetText("FilterDay", "Day"),
            "FilterWeek" => GetText("FilterWeek", "Week"),
            "FilterMonth" => GetText("FilterMonth", "Month"),
            "FilterCustom" => GetText("FilterCustom", "Custom"),
            "FilterPlan" => GetText("FilterPlan", "Plan"),
            _ => key
        };
    }

    private static Color TryGetColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    private static string GetText(string key, string fallback)
        => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
}
