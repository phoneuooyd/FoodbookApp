using System.ComponentModel;
using Foodbook.Models;
using Foodbook.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "PlanId")]
public partial class FoodbookPage : ContentPage
{
    private readonly FoodbookViewModel _vm;
    private string? _planId;
    private bool _initialized;
    private Guid? _initializedPlanId;

    private static readonly string[] Emojis = ["\U0001F4CB", "\U0001F957", "\U0001F373", "\U0001F969", "\U0001F32E", "\U0001F35C", "\U0001F966", "\U0001F371", "\U0001F951"];
    private static readonly string[] Swatches = ["#5B3FE8", "#10B981", "#F59E0B", "#F43F5E", "#8B5CF6", "#3B82F6", "#EC4899", "#0EA5E9"];

    public string? PlanId
    {
        get => _planId;
        set
        {
            if (_planId == value) return;
            _planId = value;
            _initialized = false;
            _initializedPlanId = null;
        }
    }

    public FoodbookPage(FoodbookViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        BuildEmojiRow();
        BuildSwatchRow();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        Guid? id = null;
        if (!string.IsNullOrWhiteSpace(_planId) && Guid.TryParse(_planId, out var parsed))
            id = parsed;

        if (!_initialized || _initializedPlanId != id)
        {
            _initialized = true;
            _initializedPlanId = id;
            await _vm.InitializeAsync(id);
        }
        else if (!_vm.RecipesLoaded)
        {
            await _vm.LoadRecipesAsync();
        }

        UpdateEmojiSelection();
        UpdateSwatchSelection();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    private void BuildEmojiRow()
    {
        foreach (var emoji in Emojis)
        {
            var border = new Border
            {
                WidthRequest = 53,
                HeightRequest = 53,
                StrokeThickness = 2.2,
                Stroke = Colors.Transparent,
                BackgroundColor = GetResource<Color>("FrameBackgroundColor") ?? Colors.White,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Content = new Label
                {
                    Text = emoji,
                    FontSize = 26,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };
            var tap = new TapGestureRecognizer { CommandParameter = emoji };
            tap.Tapped += OnEmojiTapped;
            border.GestureRecognizers.Add(tap);
            EmojiRow.Children.Add(border);
        }
    }

    private void BuildSwatchRow()
    {
        foreach (var hex in Swatches)
        {
            var border = new Border
            {
                WidthRequest = 41,
                HeightRequest = 41,
                StrokeThickness = 2.8,
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb(hex),
                StrokeShape = new RoundRectangle { CornerRadius = 12 }
            };
            var tap = new TapGestureRecognizer { CommandParameter = hex };
            tap.Tapped += OnSwatchTapped;
            border.GestureRecognizers.Add(tap);
            SwatchRow.Children.Add(border);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FoodbookViewModel.Emoji))
            MainThread.BeginInvokeOnMainThread(UpdateEmojiSelection);
        else if (e.PropertyName == nameof(FoodbookViewModel.AccentColor))
            MainThread.BeginInvokeOnMainThread(UpdateSwatchSelection);
    }

    private void OnEmojiTapped(object? sender, EventArgs e)
    {
        if (sender is not Border border) return;
        var tapped = border.GestureRecognizers
            .OfType<TapGestureRecognizer>()
            .FirstOrDefault()?.CommandParameter as string;

        if (!string.IsNullOrEmpty(tapped))
        {
            _vm.Emoji = tapped;
            UpdateEmojiSelection();
        }
    }

    private void UpdateEmojiSelection()
    {
        var primary = GetResource<Color>("Primary") ?? Colors.Purple;
        var secondary = GetResource<Color>("Secondary") ?? Color.FromArgb("#EDE9FF");
        var frameBg = GetResource<Color>("FrameBackgroundColor") ?? Colors.White;

        foreach (var child in EmojiRow.Children)
        {
            if (child is not Border b) continue;
            var param = b.GestureRecognizers
                .OfType<TapGestureRecognizer>()
                .FirstOrDefault()?.CommandParameter as string;

            var isSelected = param == _vm.Emoji;
            b.Stroke = isSelected ? primary : Colors.Transparent;
            b.BackgroundColor = isSelected ? secondary : frameBg;
        }
    }

    private void OnSwatchTapped(object? sender, EventArgs e)
    {
        if (sender is not Border border) return;
        var tapped = border.GestureRecognizers
            .OfType<TapGestureRecognizer>()
            .FirstOrDefault()?.CommandParameter as string;

        if (!string.IsNullOrEmpty(tapped))
        {
            _vm.AccentColor = tapped;
            UpdateSwatchSelection();
        }
    }

    private void UpdateSwatchSelection()
    {
        var textColor = GetResource<Color>("FrameTextColor") ?? Colors.Black;

        foreach (var child in SwatchRow.Children)
        {
            if (child is not Border b) continue;
            var param = b.GestureRecognizers
                .OfType<TapGestureRecognizer>()
                .FirstOrDefault()?.CommandParameter as string;

            b.Stroke = param == _vm.AccentColor ? textColor : Colors.Transparent;
        }
    }

    private void OnIncreasePortionsClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is PlannedMeal meal)
            _vm.IncreasePortionsCommand.Execute(meal);
    }

    private void OnDecreasePortionsClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is PlannedMeal meal)
            _vm.DecreasePortionsCommand.Execute(meal);
    }

    private void OnRemoveMealClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is PlannedMeal meal)
            _vm.RemoveMealCommand.Execute(meal);
    }

    private static T? GetResource<T>(string key) where T : class
    {
        if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is T typed)
            return typed;
        return null;
    }
}
