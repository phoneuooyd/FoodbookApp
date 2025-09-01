using System.Collections;
using System.Windows.Input;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components
{
    public partial class GenericListComponent : ContentView
    {
        public static readonly BindableProperty ItemsSourceProperty =
            BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(GenericListComponent));

        public static readonly BindableProperty ItemTemplateProperty =
            BindableProperty.Create(nameof(ItemTemplate), typeof(DataTemplate), typeof(GenericListComponent));

        public static readonly BindableProperty IsRefreshingProperty =
            BindableProperty.Create(nameof(IsRefreshing), typeof(bool), typeof(GenericListComponent), false);

        public static readonly BindableProperty RefreshCommandProperty =
            BindableProperty.Create(nameof(RefreshCommand), typeof(ICommand), typeof(GenericListComponent));

        public static readonly BindableProperty EmptyTitleProperty =
            BindableProperty.Create(nameof(EmptyTitle), typeof(string), typeof(GenericListComponent), "No items found");

        public static readonly BindableProperty EmptyHintProperty =
            BindableProperty.Create(nameof(EmptyHint), typeof(string), typeof(GenericListComponent), "Add some items to get started");

        public static readonly BindableProperty IsVisibleProperty =
            BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(GenericListComponent), true);

        private readonly PageThemeHelper _themeHelper;

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public bool IsRefreshing
        {
            get => (bool)GetValue(IsRefreshingProperty);
            set => SetValue(IsRefreshingProperty, value);
        }

        public ICommand RefreshCommand
        {
            get => (ICommand)GetValue(RefreshCommandProperty);
            set => SetValue(RefreshCommandProperty, value);
        }

        public string EmptyTitle
        {
            get => (string)GetValue(EmptyTitleProperty);
            set => SetValue(EmptyTitleProperty, value);
        }

        public string EmptyHint
        {
            get => (string)GetValue(EmptyHintProperty);
            set => SetValue(EmptyHintProperty, value);
        }

        public new bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public GenericListComponent()
        {
            InitializeComponent();
            _themeHelper = new PageThemeHelper();
            
            // Initialize theme handling when component is loaded
            Loaded += OnComponentLoaded;
            Unloaded += OnComponentUnloaded;
        }

        private void OnComponentLoaded(object? sender, EventArgs e)
        {
            _themeHelper.Initialize();
        }

        private void OnComponentUnloaded(object? sender, EventArgs e)
        {
            _themeHelper.Cleanup();
        }
    }
}