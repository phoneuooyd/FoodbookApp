using Foodbook.ViewModels;
using System.Globalization;
using System.Resources;
using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace Foodbook.Views.Components;

public partial class CalorieSummaryCardComponent : ContentView
{
    private static readonly ResourceManager ResourceManager =
        new("FoodbookApp.Localization.DietStatisticsPageResources", typeof(CalorieSummaryCardComponent).Assembly);

    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(
            nameof(Data),
            typeof(CalorieSummaryCardData),
            typeof(CalorieSummaryCardComponent),
            CalorieSummaryCardData.Empty,
            propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty OpenSettingsCommandProperty =
        BindableProperty.Create(nameof(OpenSettingsCommand), typeof(ICommand), typeof(CalorieSummaryCardComponent));

    private readonly CalorieRangeDrawable _drawable;

    public CalorieSummaryCardComponent()
    {
        InitializeComponent();
        _drawable = new CalorieRangeDrawable();
        CalorieRangeBar.Drawable = _drawable;
    }

    public CalorieSummaryCardData Data
    {
        get => (CalorieSummaryCardData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public ICommand? OpenSettingsCommand
    {
        get => (ICommand?)GetValue(OpenSettingsCommandProperty);
        set => SetValue(OpenSettingsCommandProperty, value);
    }

    public string TargetRangeText =>
        $"{GetText("CaloriesTargetRange", "Target range")}: {(Data?.TargetRangeStart ?? 0):F0} - {(Data?.TargetRangeEnd ?? 0):F0} kcal";

    private static void OnCardValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not CalorieSummaryCardComponent card)
        {
            return;
        }

        card.UpdateDrawable();
        card.OnPropertyChanged(nameof(card.TargetRangeText));
    }

    private void UpdateDrawable()
    {
        var data = Data ?? CalorieSummaryCardData.Empty;

        var ratio = data.CaloriesProgressRatio;
        if (ratio <= 0 && data.GoalCalories > 0 && data.ConsumedCalories > 0)
        {
            ratio = data.ConsumedCalories / data.GoalCalories;
        }

        _drawable.ProgressRatio = ratio;
        _drawable.MinValue = data.CaloriesMin;
        _drawable.MaxValue = data.CaloriesMax <= 0 ? 1 : data.CaloriesMax;
        _drawable.TargetStart = data.TargetRangeStart;
        _drawable.TargetEnd = data.TargetRangeEnd;

        _drawable.ProgressColor = TryGetColor("DietStatsCarbsColor", Color.FromArgb("#00C9A7"));
        _drawable.TrackColor = TryGetColor("DietStatsTrackColor", Color.FromArgb("#30303A"));
        _drawable.TargetRangeColor = TryGetColor("DietStatsTargetRangeColor", Color.FromRgba(255, 255, 255, 0.30f));

        CalorieRangeBar.Invalidate();
    }

    private static Color TryGetColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    private static string GetText(string key, string fallback)
        => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
}

internal sealed class CalorieRangeDrawable : IDrawable
{
    public double ProgressRatio { get; set; }

    public double MinValue { get; set; }

    public double MaxValue { get; set; } = 1;

    public double TargetStart { get; set; }

    public double TargetEnd { get; set; }

    public Color ProgressColor { get; set; } = Color.FromArgb("#00C9A7");

    public Color TrackColor { get; set; } = Color.FromArgb("#30303A");

    public Color TargetRangeColor { get; set; } = Color.FromRgba(255, 255, 255, 0.30f);

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var corner = dirtyRect.Height / 2f;
        var barRect = new RectF(dirtyRect.X, dirtyRect.Y + 4, dirtyRect.Width, dirtyRect.Height - 8);

        canvas.FillColor = TrackColor;
        canvas.FillRoundedRectangle(barRect, corner);

        var startRatio = (float)Math.Clamp((TargetStart - MinValue) / Math.Max(1, MaxValue - MinValue), 0, 1);
        var endRatio = (float)Math.Clamp((TargetEnd - MinValue) / Math.Max(1, MaxValue - MinValue), 0, 1);
        var targetRect = new RectF(
            barRect.X + barRect.Width * startRatio,
            barRect.Y,
            barRect.Width * Math.Max(0.01f, endRatio - startRatio),
            barRect.Height);

        canvas.FillColor = TargetRangeColor;
        canvas.FillRoundedRectangle(targetRect, corner);

        var progressWidth = (float)(barRect.Width * Math.Clamp(ProgressRatio, 0, 1));
        if (progressWidth > 0)
        {
            canvas.FillColor = ProgressColor;
            canvas.FillRoundedRectangle(new RectF(barRect.X, barRect.Y, progressWidth, barRect.Height), corner);
        }
    }
}
