using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Foodbook.Converters;
using Foodbook.Utils;

namespace Foodbook.Views.Components;

/// <summary>
/// Controls the height of the tab header bar.
/// Small = 44px (default), Mid = 88px (2�), Big = 132px (3�).
/// </summary>
public enum TabHeaderSize
{
    Small = 44,
    Mid = 88,
    Big = 132
}

/// <summary>
/// Reusable tab component that provides a consistent tab interface across the application.
/// Supports dynamic tab creation with automatic styling and theme support.
/// </summary>
[ContentProperty(nameof(Tabs))]
public class TabComponent : ContentView, INotifyPropertyChanged
{
    private readonly ObservableCollection<TabItem> _tabs = new();
    private Grid? _tabHeaderGrid;
    private ContentView? _tabContentContainer;
    private ContentView? _tabTransitionOverlay;
    private readonly SemaphoreSlim _contentSwapGate = new(1, 1);
    private bool _hasRenderedInitialContent;

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(TabComponent), 0, BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);

    public static readonly BindableProperty SelectTabCommandProperty =
        BindableProperty.Create(nameof(SelectTabCommand), typeof(System.Windows.Input.ICommand), typeof(TabComponent), null);

    /// <summary>
    /// Gets or sets the size of the tab header bar.
    /// Small (44px, default), Mid (88px), Big (132px).
    /// Changing this value at runtime rebuilds the header.
    /// </summary>
    public static readonly BindableProperty TabSizeProperty =
        BindableProperty.Create(
            nameof(TabSize),
            typeof(TabHeaderSize),
            typeof(TabComponent),
            TabHeaderSize.Small,
            propertyChanged: OnTabSizeChanged);

    /// <summary>Gets or sets the currently selected tab index.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to execute when a tab is selected.
    /// The command parameter will be the tab index.
    /// </summary>
    public System.Windows.Input.ICommand? SelectTabCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(SelectTabCommandProperty);
        set => SetValue(SelectTabCommandProperty, value);
    }

    /// <summary>Gets or sets the visual size of the tab header.</summary>
    public TabHeaderSize TabSize
    {
        get => (TabHeaderSize)GetValue(TabSizeProperty);
        set => SetValue(TabSizeProperty, value);
    }

    /// <summary>Gets the collection of tabs in this component.</summary>
    public ObservableCollection<TabItem> Tabs => _tabs;

    // ?? Derived metrics from TabSize ????????????????????????????????????????
    /// <summary>Header height in device-independent pixels (44 / 88 / 132).</summary>
    private double HeaderHeight => (double)(int)TabSize;

    /// <summary>Font size for tab button labels, scales with header height.</summary>
    private double ButtonFontSize => TabSize switch
    {
        TabHeaderSize.Mid  => 16,
        TabHeaderSize.Big  => 20,
        _                  => 13   // Small
    };

    /// <summary>Vertical padding inside each tab button, scales with header height.</summary>
    private Thickness ButtonPadding => TabSize switch
    {
        TabHeaderSize.Mid  => new Thickness(0, 8),
        TabHeaderSize.Big  => new Thickness(0, 20),
        _                  => new Thickness(0)   // Small
    };

    public TabComponent()
    {
        _tabs.CollectionChanged += OnTabsCollectionChanged;
        BuildControl();
    }

    private void BuildControl()
    {
        // Create header grid � two rows: label row + thin underline row
        _tabHeaderGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(2, GridUnitType.Absolute) }
            },
            HeightRequest = HeaderHeight
        };
        _tabHeaderGrid.SetAppThemeColor(Grid.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D30"));

        // Create content containers (active content + transition overlay)
        _tabContentContainer = new ContentView();
        _tabTransitionOverlay = new ContentView
        {
            IsVisible = false,
            InputTransparent = true
        };

        var contentHostGrid = new Grid();
        contentHostGrid.Add(_tabContentContainer);
        contentHostGrid.Add(_tabTransitionOverlay);

        // Create main layout
        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            }
        };

        mainGrid.Add(_tabHeaderGrid, 0, 0);
        mainGrid.Add(contentHostGrid, 0, 1);

        Content = mainGrid;
    }

    private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RebuildTabHeaders();

        if (_tabs.Count == 0)
        {
            if (_tabContentContainer != null)
                _tabContentContainer.Content = null;
            return;
        }

        if (SelectedIndex < 0 || SelectedIndex >= _tabs.Count)
        {
            SelectedIndex = Math.Clamp(SelectedIndex, 0, _tabs.Count - 1);
            return;
        }

        _ = UpdateTabVisibilityAsync(SelectedIndex, SelectedIndex, animated: false);
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TabComponent tabComponent)
        {
            var previousIndex = oldValue is int oldIndex ? oldIndex : tabComponent.SelectedIndex;
            var currentIndex = newValue is int newIndex ? newIndex : tabComponent.SelectedIndex;
            _ = tabComponent.UpdateTabVisibilityAsync(previousIndex, currentIndex, animated: true);
            tabComponent.OnPropertyChanged(nameof(SelectedIndex));
        }
    }

    private static void OnTabSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TabComponent tc && tc._tabHeaderGrid != null)
        {
            tc._tabHeaderGrid.HeightRequest = tc.HeaderHeight;
            tc.RebuildTabHeaders();
        }
    }

    /// <summary>
    /// Rebuilds the tab header buttons based on the current tabs collection.
    /// </summary>
    private void RebuildTabHeaders()
    {
        if (_tabHeaderGrid == null) return;
        
        _tabHeaderGrid.Children.Clear();
        _tabHeaderGrid.ColumnDefinitions.Clear();

        if (_tabs.Count == 0)
            return;

        // Create column definitions for each tab
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var boolToColorConverter = new BoolToColorConverter();

        // Create tab buttons
        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            var tabIndex = i;

            // Create tab button � compact, no border, transparent bg
            var button = new Button
            {
                FontSize  = ButtonFontSize,
                CornerRadius = 0,
                BorderWidth  = 0,
                Padding      = ButtonPadding
            };

            // Bind the button text to the TabItem.Name property so translated names update dynamically
            button.SetBinding(Button.TextProperty, new Binding(nameof(TabItem.Name), source: tab));

            // Bind button properties to tab selection state
            button.SetBinding(Button.BackgroundColorProperty, new Binding(
                path: nameof(TabItem.IsSelected),
                source: tab,
                converter: boolToColorConverter,
                converterParameter: "TabBackground"
            ));

            button.SetBinding(Button.TextColorProperty, new Binding(
                path: nameof(TabItem.IsSelected),
                source: tab,
                converter: boolToColorConverter,
                converterParameter: "Text"
            ));

            button.SetBinding(Button.FontAttributesProperty, new Binding(
                path: nameof(TabItem.IsSelected),
                source: tab,
                converter: boolToColorConverter,
                converterParameter: "Bold"
            ));

            button.Command = new Command(() => SelectTab(tabIndex));

            Grid.SetRow(button, 0);
            Grid.SetColumn(button, i);
            _tabHeaderGrid.Children.Add(button);

            // Underline indicator: a narrow pill centered under the tab,
            // occupying ~60% of the column width and centered horizontally.
            var indicatorContainer = new Grid
            {
                Padding = new Thickness(0),
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(6, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                }
            };

            var borderBox = new BoxView
            {
                HeightRequest = 2,
                CornerRadius = 1
            };

            borderBox.SetBinding(BoxView.BackgroundColorProperty, new Binding(
                path: nameof(TabItem.IsSelected),
                source: tab,
                converter: boolToColorConverter,
                converterParameter: "TabBorder"
            ));

            Grid.SetColumn(borderBox, 1);
            indicatorContainer.Children.Add(borderBox);

            Grid.SetRow(indicatorContainer, 1);
            Grid.SetColumn(indicatorContainer, i);
            _tabHeaderGrid.Children.Add(indicatorContainer);
        }

        // Bottom separator line
        var separatorBox = new BoxView
        {
            HeightRequest = 1,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            VerticalOptions = LayoutOptions.End
        };

        separatorBox.SetAppThemeColor(BoxView.BackgroundColorProperty, 
            Color.FromArgb("#E1E1E1"), 
            Color.FromArgb("#404040"));

        Grid.SetRow(separatorBox, 1);
        Grid.SetColumnSpan(separatorBox, _tabs.Count);
        _tabHeaderGrid.Children.Add(separatorBox);
    }

    /// <summary>
    /// Selects a tab by index and executes the SelectTabCommand if available.
    /// </summary>
    private void SelectTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
            return;

        SelectedIndex = index;

        // Execute the external command if provided
        if (SelectTabCommand?.CanExecute(index) == true)
        {
            SelectTabCommand.Execute(index);
        }
    }

    /// <summary>
    /// Updates tab visibility based on the currently selected index.
    /// </summary>
    private async Task UpdateTabVisibilityAsync(int previousIndex, int currentIndex, bool animated)
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].IsSelected = i == currentIndex;
        }

        if (_tabContentContainer == null || currentIndex < 0 || currentIndex >= _tabs.Count)
            return;

        var nextView = _tabs[currentIndex].Content;
        if (nextView == null)
            return;

        await _contentSwapGate.WaitAsync();
        try
        {
            var previousView = _tabContentContainer.Content;

            if (ReferenceEquals(previousView, nextView))
            {
                _hasRenderedInitialContent = true;
                return;
            }

            DetachViewFromParent(nextView);

            var shouldAnimate = animated
                && _hasRenderedInitialContent
                && previousView != null
                && previousIndex != currentIndex;

            if (shouldAnimate)
            {
                _tabContentContainer.Content = null;
                if (_tabTransitionOverlay != null)
                {
                    _tabTransitionOverlay.Content = previousView;
                    _tabTransitionOverlay.IsVisible = true;
                }
            }
            else if (_tabTransitionOverlay != null)
            {
                _tabTransitionOverlay.Content = null;
                _tabTransitionOverlay.IsVisible = false;
            }

            _tabContentContainer.Content = nextView;

            if (shouldAnimate)
            {
                var direction = currentIndex > previousIndex ? 1 : -1;
                await TabContentTransitionAnimator.AnimateContentSwapAsync(
                    _tabTransitionOverlay,
                    previousView,
                    nextView,
                    direction);
            }
            else
            {
                nextView.Opacity = 1;
                nextView.TranslationX = 0;
            }

            _hasRenderedInitialContent = true;
        }
        finally
        {
            _contentSwapGate.Release();
        }
    }

    private static void DetachViewFromParent(View view)
    {
        if (view.Parent is ContentView contentViewParent)
        {
            contentViewParent.Content = null;
        }
        else if (view.Parent is ContentPage contentPageParent)
        {
            contentPageParent.Content = null;
        }
        else if (view.Parent is Border borderParent)
        {
            borderParent.Content = null;
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Represents a single tab item with a name and content.
/// </summary>
[ContentProperty(nameof(Content))]
public class TabItem : BindableObject, INotifyPropertyChanged
{
    private bool _isSelected;

    public static readonly BindableProperty NameProperty =
        BindableProperty.Create(nameof(Name), typeof(string), typeof(TabItem), string.Empty);

    public static readonly BindableProperty ContentProperty =
        BindableProperty.Create(nameof(Content), typeof(View), typeof(TabItem), null);

    /// <summary>
    /// Gets or sets the display name of the tab.
    /// </summary>
    public string Name
    {
        get => (string)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <summary>
    /// Gets or sets the content view displayed when this tab is selected.
    /// </summary>
    public View? Content
    {
        get => (View?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this tab is currently selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        base.OnPropertyChanged(propertyName);
    }
}
