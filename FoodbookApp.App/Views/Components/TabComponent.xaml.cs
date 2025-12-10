using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Foodbook.Converters;
using Foodbook.Services;
using Foodbook.Models;


namespace Foodbook.Views.Components;

/// <summary>
/// Reusable tab component that provides a consistent tab interface across the application.
/// Supports dynamic tab creation with automatic styling and theme support.
/// </summary>
[ContentProperty(nameof(Tabs))]
public class TabComponent : ContentView, INotifyPropertyChanged
{
    private int _selectedIndex = 0;
    private readonly ObservableCollection<TabItem> _tabs = new();
    private Grid? _tabHeaderGrid;
    private ContentView? _tabContentContainer;
    private Color _iconColor;

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int), typeof(TabComponent), 0, BindingMode.TwoWay, propertyChanged: OnSelectedIndexChanged);

    public static readonly BindableProperty SelectTabCommandProperty =
        BindableProperty.Create(nameof(SelectTabCommand), typeof(System.Windows.Input.ICommand), typeof(TabComponent), null);

    /// <summary>
    /// Gets or sets the currently selected tab index.
    /// </summary>
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

    /// <summary>
    /// Gets the collection of tabs in this component.
    /// </summary>
    public ObservableCollection<TabItem> Tabs => _tabs;

    public TabComponent()
    {
        _tabs.CollectionChanged += OnTabsCollectionChanged;
        BuildControl();
    }

    private void BuildControl()
    {
        //get primary color from ThemeService to the _iconColor field
        _iconColor = (Color)Application.Current.Resources["Primary"];

        // Create header grid
        _tabHeaderGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(3, GridUnitType.Absolute) }
            },
            HeightRequest = DeviceInfo.Platform == DevicePlatform.iOS ? 70 : 60
        };
        _tabHeaderGrid.SetAppThemeColor(Grid.BackgroundColorProperty, Colors.White, Color.FromArgb("#2D2D30"));

        // Create content container
        _tabContentContainer = new ContentView();

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
        mainGrid.Add(_tabContentContainer, 0, 1);

        Content = mainGrid;
    }

    private void OnTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RebuildTabHeaders();
        UpdateTabVisibility();
    }

    private static void OnSelectedIndexChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TabComponent tabComponent)
        {
            tabComponent.UpdateTabVisibility();
            tabComponent.OnPropertyChanged(nameof(SelectedIndex));
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

            // Create tab button
            var button = new Button
            {
                // Text is bound so that updates to TabItem.Name (e.g., from localization) update automatically
                FontSize = 14,
                CornerRadius = 0,
                BorderWidth = 0,
                Padding = new Thickness(0)
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

            // Create bottom border indicator
            var borderBox = new BoxView
            {
                HeightRequest = 3,
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            borderBox.SetBinding(BoxView.BackgroundColorProperty, new Binding(
                path: nameof(TabItem.IsSelected),
                source: tab,
                converter: boolToColorConverter,
                converterParameter: "TabBorder"
            ));

            Grid.SetRow(borderBox, 1);
            Grid.SetColumn(borderBox, i);
            _tabHeaderGrid.Children.Add(borderBox);
        }

        // Add bottom separator line
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
    private void UpdateTabVisibility()
    {
        for (int i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].IsSelected = i == SelectedIndex;
        }

        // Update content
        if (_tabContentContainer != null && SelectedIndex >= 0 && SelectedIndex < _tabs.Count)
        {
            _tabContentContainer.Content = _tabs[SelectedIndex].Content;
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
