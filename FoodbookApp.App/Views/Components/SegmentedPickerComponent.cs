using System.Windows.Input;

namespace Foodbook.Views.Components;

public class SegmentedPickerComponent : ContentView
{
    public static readonly BindableProperty Option1TextProperty = BindableProperty.Create(nameof(Option1Text), typeof(string), typeof(SegmentedPickerComponent), string.Empty);
    public static readonly BindableProperty Option2TextProperty = BindableProperty.Create(nameof(Option2Text), typeof(string), typeof(SegmentedPickerComponent), string.Empty);
    public static readonly BindableProperty Option3TextProperty = BindableProperty.Create(nameof(Option3Text), typeof(string), typeof(SegmentedPickerComponent), string.Empty);

    public static readonly BindableProperty Option1CommandProperty = BindableProperty.Create(nameof(Option1Command), typeof(ICommand), typeof(SegmentedPickerComponent));
    public static readonly BindableProperty Option2CommandProperty = BindableProperty.Create(nameof(Option2Command), typeof(ICommand), typeof(SegmentedPickerComponent));
    public static readonly BindableProperty Option3CommandProperty = BindableProperty.Create(nameof(Option3Command), typeof(ICommand), typeof(SegmentedPickerComponent));

    public static readonly BindableProperty IsOption1SelectedProperty = BindableProperty.Create(nameof(IsOption1Selected), typeof(bool), typeof(SegmentedPickerComponent), false, propertyChanged: (bindable, _, _) => ((SegmentedPickerComponent)bindable).UpdateVisualState());
    public static readonly BindableProperty IsOption2SelectedProperty = BindableProperty.Create(nameof(IsOption2Selected), typeof(bool), typeof(SegmentedPickerComponent), false, propertyChanged: (bindable, _, _) => ((SegmentedPickerComponent)bindable).UpdateVisualState());
    public static readonly BindableProperty IsOption3SelectedProperty = BindableProperty.Create(nameof(IsOption3Selected), typeof(bool), typeof(SegmentedPickerComponent), false, propertyChanged: (bindable, _, _) => ((SegmentedPickerComponent)bindable).UpdateVisualState());

    public static readonly BindableProperty ShowOption3Property = BindableProperty.Create(nameof(ShowOption3), typeof(bool), typeof(SegmentedPickerComponent), true, propertyChanged: (bindable, _, _) => ((SegmentedPickerComponent)bindable).UpdateVisualState());

    private readonly Grid _grid;
    private readonly Button _button1;
    private readonly Button _button2;
    private readonly Button _button3;

    public string Option1Text { get => (string)GetValue(Option1TextProperty); set => SetValue(Option1TextProperty, value); }
    public string Option2Text { get => (string)GetValue(Option2TextProperty); set => SetValue(Option2TextProperty, value); }
    public string Option3Text { get => (string)GetValue(Option3TextProperty); set => SetValue(Option3TextProperty, value); }

    public ICommand? Option1Command { get => (ICommand?)GetValue(Option1CommandProperty); set => SetValue(Option1CommandProperty, value); }
    public ICommand? Option2Command { get => (ICommand?)GetValue(Option2CommandProperty); set => SetValue(Option2CommandProperty, value); }
    public ICommand? Option3Command { get => (ICommand?)GetValue(Option3CommandProperty); set => SetValue(Option3CommandProperty, value); }

    public bool IsOption1Selected { get => (bool)GetValue(IsOption1SelectedProperty); set => SetValue(IsOption1SelectedProperty, value); }
    public bool IsOption2Selected { get => (bool)GetValue(IsOption2SelectedProperty); set => SetValue(IsOption2SelectedProperty, value); }
    public bool IsOption3Selected { get => (bool)GetValue(IsOption3SelectedProperty); set => SetValue(IsOption3SelectedProperty, value); }

    public bool ShowOption3 { get => (bool)GetValue(ShowOption3Property); set => SetValue(ShowOption3Property, value); }

    public SegmentedPickerComponent()
    {
        _button1 = CreateButton();
        _button1.SetBinding(Button.TextProperty, new Binding(nameof(Option1Text), source: this));
        _button1.SetBinding(Button.CommandProperty, new Binding(nameof(Option1Command), source: this));

        _button2 = CreateButton();
        _button2.SetBinding(Button.TextProperty, new Binding(nameof(Option2Text), source: this));
        _button2.SetBinding(Button.CommandProperty, new Binding(nameof(Option2Command), source: this));

        _button3 = CreateButton();
        _button3.SetBinding(Button.TextProperty, new Binding(nameof(Option3Text), source: this));
        _button3.SetBinding(Button.CommandProperty, new Binding(nameof(Option3Command), source: this));

        _grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new() { Width = GridLength.Star },
                new() { Width = GridLength.Star },
                new() { Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };

        _grid.Add(_button1, 0, 0);
        _grid.Add(_button2, 1, 0);
        _grid.Add(_button3, 2, 0);

        var border = new Border();
        border.SetDynamicResource(StyleProperty, "SegmentedPickerContainerBorder");
        border.Content = _grid;

        Content = border;

        UpdateVisualState();
    }

    private static Button CreateButton()
    {
        var button = new Button();
        button.SetDynamicResource(StyleProperty, "SegmentedPickerOptionButton");
        return button;
    }

    private void UpdateVisualState()
    {
        _button3.IsVisible = ShowOption3;
        _grid.ColumnDefinitions[2].Width = ShowOption3 ? GridLength.Star : new GridLength(0);

        ApplySelectionState(_button1, IsOption1Selected);
        ApplySelectionState(_button2, IsOption2Selected);
        ApplySelectionState(_button3, IsOption3Selected);
    }

    private static void ApplySelectionState(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.SetDynamicResource(Button.BackgroundColorProperty, "SegmentedSelectedColor");
            button.SetDynamicResource(Button.TextColorProperty, "FrameTextColor");
            button.FontAttributes = FontAttributes.Bold;
            button.Opacity = 1.0;
            button.BorderWidth = 1;

            var primary = Application.Current?.Resources.TryGetValue("Primary", out var primaryObj) == true && primaryObj is Color primaryColor
                ? primaryColor
                : Color.FromArgb("#8B72FF");

            button.BorderColor = Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.42f);
            button.Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.55f)),
                Offset = new Point(0, 0),
                Radius = 12,
                Opacity = 0.9f
            };
            return;
        }

        button.BackgroundColor = Colors.Transparent;
        button.TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#F1F1F1")
            : Color.FromArgb("#404040");
        button.FontAttributes = FontAttributes.None;
        button.Opacity = 0.86;
        button.BorderWidth = 0;
        button.Shadow = null;
    }
}
