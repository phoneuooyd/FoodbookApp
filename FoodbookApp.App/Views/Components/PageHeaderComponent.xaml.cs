namespace Foodbook.Views.Components;

public partial class PageHeaderComponent : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(PageHeaderComponent), string.Empty);

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeaderComponent), string.Empty);

    public static readonly BindableProperty HeaderPaddingProperty =
        BindableProperty.Create(nameof(HeaderPadding), typeof(Thickness), typeof(PageHeaderComponent), new Thickness(16, 44, 16, 16));

    public PageHeaderComponent()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public Thickness HeaderPadding
    {
        get => (Thickness)GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }
}
