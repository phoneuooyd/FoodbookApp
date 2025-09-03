using System.Windows.Input;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components
{
    public partial class FloatingActionButtonComponent : ContentView
    {
        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FloatingActionButtonComponent));

        public static readonly BindableProperty ButtonTextProperty =
            BindableProperty.Create(nameof(ButtonText), typeof(string), typeof(FloatingActionButtonComponent), "+");

        public static readonly BindableProperty IsVisibleProperty =
            BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(FloatingActionButtonComponent), true);

        private readonly PageThemeHelper _themeHelper;

        public ICommand Command
        {
            get => (ICommand)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        public new bool IsVisible
        {
            get => (bool)GetValue(IsVisibleProperty);
            set => SetValue(IsVisibleProperty, value);
        }

        public FloatingActionButtonComponent()
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