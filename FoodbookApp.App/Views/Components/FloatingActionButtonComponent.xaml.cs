using System;
using System.Collections.ObjectModel;
using System.Linq;
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
            nameof(Text), typeof(string), typeof(FabActionItem), string.Empty, propertyChanged: OnActionChanged);
        public static readonly BindableProperty IconProperty = BindableProperty.Create(
            nameof(Icon), typeof(string), typeof(FabActionItem), default(string), propertyChanged: OnActionChanged);
        public static readonly BindableProperty CommandProperty = BindableProperty.Create(
            nameof(Command), typeof(ICommand), typeof(FabActionItem));
        public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
            nameof(CommandParameter), typeof(object), typeof(FabActionItem));

        public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public string? Icon { get => (string?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
        public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

        private static void OnActionChanged(BindableObject bindable, object oldValue, object newValue) => (bindable as FabActionItem)?.ParentFab?.RequestRebuild();
        internal FloatingActionButtonComponent? ParentFab { get; set; }
    }

    public partial class FloatingActionButtonComponent : ContentView
    {
        public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FloatingActionButtonComponent));
        public static readonly BindableProperty ButtonTextProperty = BindableProperty.Create(nameof(ButtonText), typeof(string), typeof(FloatingActionButtonComponent), "+");
        public static readonly BindableProperty IsVisibleProperty = BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(FloatingActionButtonComponent), true);
        public static readonly BindableProperty IsExpandableProperty = BindableProperty.Create(nameof(IsExpandable), typeof(bool), typeof(FloatingActionButtonComponent), false, propertyChanged: OnIsExpandableChanged);
        public static readonly BindableProperty ActionsProperty = BindableProperty.Create(nameof(Actions), typeof(ObservableCollection<FabActionItem>), typeof(FloatingActionButtonComponent), defaultValueCreator: _ => new ObservableCollection<FabActionItem>(), propertyChanged: OnActionsChanged);

        private readonly PageThemeHelper _themeHelper;
        private bool _isExpanded;
        private double _uniformActionWidth = 0;

        // Backing fields for named elements when building UI in code
        private BoxView? _overlay;
        private VerticalStackLayout? _actionsStack;
        private Button? _mainFab;

        public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public string ButtonText { get => (string)GetValue(ButtonTextProperty); set => SetValue(ButtonTextProperty, value); }
        public new bool IsVisible { get => (bool)GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
        public bool IsExpandable { get => (bool)GetValue(IsExpandableProperty); set => SetValue(IsExpandableProperty, value); }
        public ObservableCollection<FabActionItem> Actions { get => (ObservableCollection<FabActionItem>)GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

        public FloatingActionButtonComponent()
        {
            // Build the visual tree in code to avoid XAML resource loading issues on some platforms
            BuildVisualTreeInCode();

            _themeHelper = new PageThemeHelper();
            Loaded += OnComponentLoaded;
            Unloaded += OnComponentUnloaded;
        }

        private void BuildVisualTreeInCode()
        {
            var root = new Grid();

            _overlay = new BoxView
            {
                IsVisible = false,
                BackgroundColor = Colors.Black,
                Opacity = 0.2
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += OnOverlayTapped;
            _overlay.GestureRecognizers.Add(tap);
            root.Add(_overlay);

            var anchor = new Grid
            {
                Padding = new Thickness(16),
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.End
            };

            _actionsStack = new VerticalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 72),
                TranslationX = -5,
                TranslationY = -5,
                IsVisible = false,
                Opacity = 0
            };
            anchor.Add(_actionsStack);

            _mainFab = new Button
            {
                Text = ButtonText,
                CornerRadius = 24,
                WidthRequest = 56,
                HeightRequest = 56,
                Padding = new Thickness(0),
            };
            // Bind to dynamic resources so color updates with theme changes
            _mainFab.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
            _mainFab.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");
            // Try apply FloatingActionButton style if exists (will not override dynamic colors)
            if (Application.Current?.Resources.TryGetValue("FloatingActionButton", out var styleObj) == true && styleObj is Style style)
            {
                _mainFab.Style = style;
            }
            _mainFab.Clicked += OnMainFabClicked;
            anchor.Add(_mainFab);

            root.Add(anchor);
            Content = root;
        }

        private void OnComponentLoaded(object? sender, EventArgs e)
        {
            _themeHelper.Initialize();
            AttachActions();
            BuildActions();
        }
        private void OnComponentUnloaded(object? sender, EventArgs e) => _themeHelper.Cleanup();

        private static void OnIsExpandableChanged(BindableObject bindable, object oldValue, object newValue)
        { if (bindable is FloatingActionButtonComponent fab) fab.UpdateVisualStateForMode(); }
        private static void OnActionsChanged(BindableObject bindable, object oldValue, object newValue)
        { if (bindable is FloatingActionButtonComponent fab) { fab.AttachActions(); fab.BuildActions(); } }

        private void AttachActions()
        {
            if (Actions == null) return;
            foreach (var a in Actions) a.ParentFab = this;
            Actions.CollectionChanged += (_, __) => RequestRebuild();
        }
        internal void RequestRebuild() => BuildActions();

        private void UpdateVisualStateForMode()
        { if (!IsExpandable) _ = CollapseAsync(); BuildActions(); }

        private void BuildActions()
        {
            var actionsStack = _actionsStack;
            if (actionsStack == null) return;
            actionsStack.Children.Clear();
            if (!IsExpandable || Actions == null || Actions.Count == 0) return;

            // Approximate width: char count * factor + padding
            int maxChars = Actions.Max(a => (a.Text?.Length ?? 0));
            _uniformActionWidth = Math.Max(120, maxChars * 10 + 32);

            foreach (var action in Actions)
            {
                var btn = new Button
                {
                    Text = action.Text,
                    Command = action.Command,
                    CommandParameter = action.CommandParameter,
                    HorizontalOptions = LayoutOptions.End,
                    CornerRadius = 12,
                    HeightRequest = 48,
                    WidthRequest = _uniformActionWidth,
                    Padding = new Thickness(16, 10),
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                };

                btn.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
                btn.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");

                var states = new VisualStateGroupList();
                var common = new VisualStateGroup { Name = "CommonStates" };
                var normal = new VisualState { Name = "Normal" };
                var pressed = new VisualState { Name = "Pressed" };
                pressed.Setters.Add(new Setter { Property = Button.ScaleProperty, Value = 0.97 });
                if (Application.Current?.Resources.TryGetValue("Secondary", out var sec) == true)
                    pressed.Setters.Add(new Setter { Property = Button.BackgroundColorProperty, Value = sec });
                common.States.Add(normal);
                common.States.Add(pressed);
                VisualStateManager.SetVisualStateGroups(btn, states);
                states.Add(common);

                actionsStack.Children.Add(btn);
            }
        }

        private async void OnMainFabClicked(object? sender, EventArgs e)
        {
            if (!IsExpandable) return;
            if (_isExpanded) await CollapseAsync(); else await ExpandAsync();
        }
        private async void OnOverlayTapped(object? sender, TappedEventArgs e) { if (_isExpanded) await CollapseAsync(); }

        private async Task ExpandAsync()
        {
            _isExpanded = true;
            if (_overlay != null) _overlay.IsVisible = true;
            if (_actionsStack != null) _actionsStack.IsVisible = true;
            var rotate = _mainFab?.RotateTo(45, 150, Easing.CubicIn) ?? Task.CompletedTask;
            var fade = _actionsStack != null ? _actionsStack.FadeTo(1, 150, Easing.CubicIn) : Task.CompletedTask;
            await Task.WhenAll(rotate, fade);
        }
        private async Task CollapseAsync()
        {
            _isExpanded = false;
            var rotate = _mainFab?.RotateTo(0, 150, Easing.CubicOut) ?? Task.CompletedTask;
            var fade = _actionsStack != null ? _actionsStack.FadeTo(0, 150, Easing.CubicOut) : Task.CompletedTask;
            await Task.WhenAll(rotate, fade);
            if (_actionsStack != null) _actionsStack.IsVisible = false; if (_overlay != null) _overlay.IsVisible = false;
        }
    }
}