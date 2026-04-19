using Foodbook.Views.Base;
using System.Globalization;
using System.Resources;

namespace Foodbook.Views.Components;

public partial class CalorieSummaryCardComponent : ContentView
{
    private static readonly ResourceManager ResourceManager =
        new("FoodbookApp.Localization.DietStatisticsPageResources", typeof(CalorieSummaryCardComponent).Assembly);

    public static readonly BindableProperty ConsumedCaloriesProperty =
        BindableProperty.Create(nameof(ConsumedCalories), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty GoalCaloriesProperty =
        BindableProperty.Create(nameof(GoalCalories), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty CaloriesProgressRatioProperty =
        BindableProperty.Create(nameof(CaloriesProgressRatio), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty CaloriesMinProperty =
        BindableProperty.Create(nameof(CaloriesMin), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty CaloriesMaxProperty =
        BindableProperty.Create(nameof(CaloriesMax), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty TargetRangeStartProperty =
        BindableProperty.Create(nameof(TargetRangeStart), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    public static readonly BindableProperty TargetRangeEndProperty =
        BindableProperty.Create(nameof(TargetRangeEnd), typeof(double), typeof(CalorieSummaryCardComponent), 0d, propertyChanged: OnCardValueChanged);

    private readonly PageThemeHelper _themeHelper;
    private readonly CalorieRangeDrawable _drawable;

    public CalorieSummaryCardComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();
        _drawable = new CalorieRangeDrawable();

        BindingContext = this;
        CalorieRangeBar.Drawable = _drawable;

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    public double ConsumedCalories
    {
        get => (double)GetValue(ConsumedCaloriesProperty);
        set => SetValue(ConsumedCaloriesProperty, value);
    }

    public double GoalCalories
    {
        get => (double)GetValue(GoalCaloriesProperty);
        set => SetValue(GoalCaloriesProperty, value);
    }

    public double CaloriesProgressRatio
    {
        get => (double)GetValue(CaloriesProgressRatioProperty);
        set => SetValue(CaloriesProgressRatioProperty, value);
    }

    public double CaloriesMin
    {
        get => (double)GetValue(CaloriesMinProperty);
        set => SetValue(CaloriesMinProperty, value);
    }

    public double CaloriesMax
    {
        get => (double)GetValue(CaloriesMaxProperty);
        set => SetValue(CaloriesMaxProperty, value);
    }

    public double TargetRangeStart
    {
        get => (double)GetValue(TargetRangeStartProperty);
        set => SetValue(TargetRangeStartProperty, value);
    }

    public double TargetRangeEnd
    {
        get => (double)GetValue(TargetRangeEndProperty);
        set => SetValue(TargetRangeEndProperty, value);
    }

    public string TargetRangeText =>
        $"{GetText("CaloriesTargetRange", "Target range")}: {TargetRangeStart:F0} - {TargetRangeEnd:F0} kcal";

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
        UpdateDrawable();
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
    }

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
        _drawable.ProgressRatio = CaloriesProgressRatio;
        _drawable.MinValue = CaloriesMin;
        _drawable.MaxValue = CaloriesMax <= 0 ? 1 : CaloriesMax;
        _drawable.TargetStart = TargetRangeStart;
        _drawable.TargetEnd = TargetRangeEnd;

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
