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

        public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public string ButtonText { get => (string)GetValue(ButtonTextProperty); set => SetValue(ButtonTextProperty, value); }
        public new bool IsVisible { get => (bool)GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }
        public bool IsExpandable { get => (bool)GetValue(IsExpandableProperty); set => SetValue(IsExpandableProperty, value); }
        public ObservableCollection<FabActionItem> Actions { get => (ObservableCollection<FabActionItem>)GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

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
            if (ActionsStack == null) return;
            ActionsStack.Children.Clear();
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
                    Style = (Style)Application.Current?.Resources["FabActionButton"]
                };

                // Dynamic theme resources (will update when theme/colors change)
                btn.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
                btn.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");

                // Optional pressed visual feedback using VisualStateManager
                var states = new VisualStateGroupList();
                var common = new VisualStateGroup { Name = "CommonStates" };
                var normal = new VisualState { Name = "Normal" };
                var pressed = new VisualState { Name = "Pressed" };
                pressed.Setters.Add(new Setter { Property = Button.ScaleProperty, Value = 0.97 });
                pressed.Setters.Add(new Setter { Property = Button.BackgroundColorProperty, Value = Application.Current?.Resources["Secondary"] });
                common.States.Add(normal);
                common.States.Add(pressed);
                VisualStateManager.SetVisualStateGroups(btn, states);
                states.Add(common);

                if (!string.IsNullOrWhiteSpace(action.Icon))
                    btn.ImageSource = ImageSource.FromFile(action.Icon);
                ActionsStack.Children.Add(btn);
            }
        }

        private async void OnMainFabClicked(object? sender, EventArgs e)
        {
            // If not expandable, rely on the Button.Command bound in XAML to execute once.
            if (!IsExpandable)
            {
                return;
            }
            if (_isExpanded) await CollapseAsync(); else await ExpandAsync();
        }
        private async void OnOverlayTapped(object? sender, TappedEventArgs e) { if (_isExpanded) await CollapseAsync(); }

        private async Task ExpandAsync()
        {
            _isExpanded = true; if (Overlay != null) Overlay.IsVisible = true; if (ActionsStack != null) ActionsStack.IsVisible = true;
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
            if (ActionsStack != null) ActionsStack.IsVisible = false; if (Overlay != null) Overlay.IsVisible = false;
        }
    }
}