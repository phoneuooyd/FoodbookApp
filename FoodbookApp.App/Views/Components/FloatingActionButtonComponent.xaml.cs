using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Foodbook.Views.Base;

namespace Foodbook.Views.Components
{
    public class FabActionItem : BindableObject
    {
        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(FabActionItem), string.Empty);

        public static readonly BindableProperty IconProperty = BindableProperty.Create(
            nameof(Icon), typeof(string), typeof(FabActionItem), default(string));

        public static readonly BindableProperty CommandProperty = BindableProperty.Create(
            nameof(Command), typeof(ICommand), typeof(FabActionItem));

        public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
            nameof(CommandParameter), typeof(object), typeof(FabActionItem));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string? Icon
        {
            get => (string?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public ICommand? Command
        {
            get => (ICommand?)GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }
    }

    public partial class FloatingActionButtonComponent : ContentView
    {
        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FloatingActionButtonComponent));

        public static readonly BindableProperty ButtonTextProperty =
            BindableProperty.Create(nameof(ButtonText), typeof(string), typeof(FloatingActionButtonComponent), "+");

        public static readonly BindableProperty IsVisibleProperty =
            BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(FloatingActionButtonComponent), true);

        // Expandable behavior toggle
        public static readonly BindableProperty IsExpandableProperty =
            BindableProperty.Create(nameof(IsExpandable), typeof(bool), typeof(FloatingActionButtonComponent), false, propertyChanged: OnIsExpandableChanged);

        // Actions collection when expandable
        public static readonly BindableProperty ActionsProperty = BindableProperty.Create(
            nameof(Actions), typeof(ObservableCollection<FabActionItem>), typeof(FloatingActionButtonComponent),
            defaultValueCreator: _ => new ObservableCollection<FabActionItem>(), propertyChanged: OnActionsChanged);

        private readonly PageThemeHelper _themeHelper;
        private bool _isExpanded;

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

        public bool IsExpandable
        {
            get => (bool)GetValue(IsExpandableProperty);
            set => SetValue(IsExpandableProperty, value);
        }

        public ObservableCollection<FabActionItem> Actions
        {
            get => (ObservableCollection<FabActionItem>)GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public FloatingActionButtonComponent()
        {
            InitializeComponent();
            _themeHelper = new PageThemeHelper();
            Loaded += OnComponentLoaded;
            Unloaded += OnComponentUnloaded;
        }

        private void OnComponentLoaded(object? sender, EventArgs e)
        {
            _themeHelper.Initialize();
            BuildActions();
        }

        private void OnComponentUnloaded(object? sender, EventArgs e)
        {
            _themeHelper.Cleanup();
        }

        private static void OnIsExpandableChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is FloatingActionButtonComponent fab)
            {
                fab.UpdateVisualStateForMode();
            }
        }

        private static void OnActionsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is FloatingActionButtonComponent fab)
            {
                fab.BuildActions();
            }
        }

        private void UpdateVisualStateForMode()
        {
            if (!IsExpandable)
            {
                _ = CollapseAsync();
            }
            BuildActions();
        }

        private void BuildActions()
        {
            if (ActionsStack == null) return;
            ActionsStack.Children.Clear();

            if (!IsExpandable || Actions == null || Actions.Count == 0)
                return;

            foreach (var action in Actions)
            {
                var btn = new Button
                {
                    Text = action.Text,
                    Command = action.Command,
                    CommandParameter = action.CommandParameter,
                    HorizontalOptions = LayoutOptions.End,
                    CornerRadius = 24,
                    HeightRequest = 48,
                    BackgroundColor = (Color)Application.Current?.Resources["Primary"] ?? Colors.Blue,
                    TextColor = Colors.White
                };

                if (!string.IsNullOrWhiteSpace(action.Icon))
                    btn.ImageSource = ImageSource.FromFile(action.Icon);

                ActionsStack.Children.Add(btn);
            }
        }

        private async void OnMainFabClicked(object? sender, EventArgs e)
        {
            if (!IsExpandable)
            {
                if (Command?.CanExecute(null) == true)
                    Command.Execute(null);
                return;
            }

            if (_isExpanded)
                await CollapseAsync();
            else
                await ExpandAsync();
        }

        private async void OnOverlayTapped(object? sender, TappedEventArgs e)
        {
            if (_isExpanded)
                await CollapseAsync();
        }

        private async Task ExpandAsync()
        {
            _isExpanded = true;
            if (Overlay != null) Overlay.IsVisible = true;
            if (ActionsStack != null) ActionsStack.IsVisible = true;

            var rotate = MainFab?.RotateTo(45, 150, Easing.CubicIn) ?? Task.CompletedTask;
            var fade = ActionsStack != null ? ActionsStack.FadeTo(1, 150, Easing.CubicIn) : Task.CompletedTask;
            await Task.WhenAll(rotate, fade);
        }

        private async Task CollapseAsync()
        {
            _isExpanded = false;
            var rotate = MainFab?.RotateTo(0, 150, Easing.CubicOut) ?? Task.CompletedTask;
            var fade = ActionsStack != null ? ActionsStack.FadeTo(0, 150, Easing.CubicOut) : Task.CompletedTask;
            await Task.WhenAll(rotate, fade);
            if (ActionsStack != null) ActionsStack.IsVisible = false;
            if (Overlay != null) Overlay.IsVisible = false;
        }
    }
}