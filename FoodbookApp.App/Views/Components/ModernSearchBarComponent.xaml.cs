using System.Windows.Input;

namespace Foodbook.Views.Components
{
    public partial class ModernSearchBarComponent : ContentView
    {
        public static readonly BindableProperty SearchTextProperty =
            BindableProperty.Create(nameof(SearchText), typeof(string), typeof(ModernSearchBarComponent), string.Empty, BindingMode.TwoWay);

        public static readonly BindableProperty PlaceholderTextProperty =
            BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(ModernSearchBarComponent), "Search...");

        public static readonly BindableProperty ClearCommandProperty =
            BindableProperty.Create(nameof(ClearCommand), typeof(ICommand), typeof(ModernSearchBarComponent));

        public static readonly BindableProperty IsVisibleProperty =
            BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(ModernSearchBarComponent), true);

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public ICommand ClearCommand
        {
            get => (ICommand)GetValue(ClearCommandProperty);
            set => SetValue(ClearCommandProperty, value);
        }

        public new bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public ModernSearchBarComponent()
        {
            InitializeComponent();
        }
    }
}