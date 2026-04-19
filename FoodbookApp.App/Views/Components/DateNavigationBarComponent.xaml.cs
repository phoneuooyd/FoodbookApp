using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public partial class DateNavigationBarComponent : ContentView
{
    public static readonly BindableProperty DateRangeProperty =
        BindableProperty.Create(
            nameof(DateRange),
            typeof(ObservableCollection<DateItem>),
            typeof(DateNavigationBarComponent),
            null,
            propertyChanged: OnDateRangeChanged);

    public static readonly BindableProperty SelectedDateProperty =
        BindableProperty.Create(
            nameof(SelectedDate),
            typeof(DateItem),
            typeof(DateNavigationBarComponent),
            null,
            BindingMode.TwoWay,
            propertyChanged: OnSelectedDateChanged);

    public static readonly BindableProperty SelectDateCommandProperty =
        BindableProperty.Create(
            nameof(SelectDateCommand),
            typeof(ICommand),
            typeof(DateNavigationBarComponent));

    public static readonly BindableProperty IsVisibleForFilterProperty =
        BindableProperty.Create(
            nameof(IsVisibleForFilter),
            typeof(bool),
            typeof(DateNavigationBarComponent),
            true);

    public static readonly BindableProperty SelectedDateCaloriesTextProperty =
        BindableProperty.Create(
            nameof(SelectedDateCaloriesText),
            typeof(string),
            typeof(DateNavigationBarComponent),
            string.Empty);

    private readonly PageThemeHelper _themeHelper;

    public DateNavigationBarComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();
        BindingContext = this;

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    public ObservableCollection<DateItem>? DateRange
    {
        get => (ObservableCollection<DateItem>?)GetValue(DateRangeProperty);
        set => SetValue(DateRangeProperty, value);
    }

    public DateItem? SelectedDate
    {
        get => (DateItem?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public ICommand? SelectDateCommand
    {
        get => (ICommand?)GetValue(SelectDateCommandProperty);
        set => SetValue(SelectDateCommandProperty, value);
    }

    public bool IsVisibleForFilter
    {
        get => (bool)GetValue(IsVisibleForFilterProperty);
        set => SetValue(IsVisibleForFilterProperty, value);
    }

    public string SelectedDateCaloriesText
    {
        get => (string)GetValue(SelectedDateCaloriesTextProperty);
        set => SetValue(SelectedDateCaloriesTextProperty, value);
    }

    public bool IsTooltipVisible => SelectedDate != null;

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
        TryCenterSelectedDate(animated: false);
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
        UnsubscribeDateRange(DateRange);
    }

    private static void OnDateRangeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not DateNavigationBarComponent component)
        {
            return;
        }

        component.UnsubscribeDateRange(oldValue as ObservableCollection<DateItem>);
        component.SubscribeDateRange(newValue as ObservableCollection<DateItem>);
        component.TryCenterSelectedDate(animated: false);
    }

    private static void OnSelectedDateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not DateNavigationBarComponent component)
        {
            return;
        }

        component.OnPropertyChanged(nameof(component.IsTooltipVisible));
        component.TryCenterSelectedDate(animated: true);
    }

    private void SubscribeDateRange(ObservableCollection<DateItem>? range)
    {
        if (range == null)
        {
            return;
        }

        range.CollectionChanged += OnDateRangeCollectionChanged;
    }

    private void UnsubscribeDateRange(ObservableCollection<DateItem>? range)
    {
        if (range == null)
        {
            return;
        }

        range.CollectionChanged -= OnDateRangeCollectionChanged;
    }

    private void OnDateRangeCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TryCenterSelectedDate(animated: false);
    }

    private void OnDateTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not DateItem dateItem)
        {
            return;
        }

        SelectedDate = dateItem;
        if (SelectDateCommand?.CanExecute(dateItem) == true)
        {
            SelectDateCommand.Execute(dateItem);
        }
    }

    private void TryCenterSelectedDate(bool animated)
    {
        if (SelectedDate == null || DateRange == null || DateRange.Count == 0)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                DateCollection.ScrollTo(SelectedDate, position: ScrollToPosition.Center, animate: animated);
            }
            catch
            {
            }
        });
    }
}
