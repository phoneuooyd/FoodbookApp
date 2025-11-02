using System.Collections;
using System.ComponentModel;
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

        // Re-introduced as no-op bindable properties for compatibility with existing XAML usages
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

        // Kept for compatibility but no longer used internally
        public static readonly BindableProperty DisableRefreshWhenEmptyProperty =
            BindableProperty.Create(nameof(DisableRefreshWhenEmpty), typeof(bool), typeof(GenericListComponent), true);

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

        // No-op properties so bindings compile but have no effect
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

        public bool DisableRefreshWhenEmpty
        {
            get => (bool)GetValue(DisableRefreshWhenEmptyProperty);
            set => SetValue(DisableRefreshWhenEmptyProperty, value);
        }

        public GenericListComponent()
        {
            InitializeComponent();
            _themeHelper = new PageThemeHelper();
            
            Loaded += OnComponentLoaded;
            Unloaded += OnComponentUnloaded;
        }

        private void OnComponentLoaded(object? sender, EventArgs e)
        {
            _themeHelper.Initialize();

            // Ensure dynamic height works when templates change at runtime
            InnerCollectionView.ItemTemplate = ItemTemplate;
            InnerCollectionView.ItemsSource = ItemsSource;

            this.PropertyChanged += OnHostPropertyChanged;
        }

        private void OnComponentUnloaded(object? sender, EventArgs e)
        {
            _themeHelper.Cleanup();
            this.PropertyChanged -= OnHostPropertyChanged;
        }

        // Keep CollectionView bindings in sync when host bindable properties change
        private void OnHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ItemTemplate))
            {
                InnerCollectionView.ItemTemplate = ItemTemplate;
                InvalidateListItemSizes();
            }
            else if (e.PropertyName == nameof(ItemsSource))
            {
                InnerCollectionView.ItemsSource = ItemsSource;
                InvalidateListItemSizes();
            }
        }

        // Helper method: request remeasure of items after template/data changes
        private void InvalidateListItemSizes()
        {
            try
            {
                // InvalidateMeasure is sufficient; ForceLayout is not available across all targets
                InnerCollectionView.InvalidateMeasure();
            }
            catch { }
        }

        private bool IsItemsSourceEmpty()
        {
            try
            {
                var enumerable = ItemsSource;
                if (enumerable == null) return true;
                var enumerator = enumerable.GetEnumerator();
                using (enumerator as IDisposable)
                {
                    return !enumerator.MoveNext();
                }
            }
            catch { return true; }
        }

        // Backwards-compat method: no-op now
        public void RequestStopRefreshing() { }
    }
}