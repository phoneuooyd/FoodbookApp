using Foodbook.Views.Base;

namespace Foodbook.Views.Components;

public partial class MacroNutritionCardComponent : ContentView
{
    public static readonly BindableProperty ConsumedCarbsProperty =
        BindableProperty.Create(nameof(ConsumedCarbs), typeof(double), typeof(MacroNutritionCardComponent), 0d);

    public static readonly BindableProperty ConsumedFatProperty =
        BindableProperty.Create(nameof(ConsumedFat), typeof(double), typeof(MacroNutritionCardComponent), 0d);

    public static readonly BindableProperty ConsumedProteinProperty =
        BindableProperty.Create(nameof(ConsumedProtein), typeof(double), typeof(MacroNutritionCardComponent), 0d);

    public static readonly BindableProperty RecommendedCarbsPercentProperty =
        BindableProperty.Create(nameof(RecommendedCarbsPercent), typeof(double), typeof(MacroNutritionCardComponent), 55d, propertyChanged: OnPercentChanged);

    public static readonly BindableProperty RecommendedFatPercentProperty =
        BindableProperty.Create(nameof(RecommendedFatPercent), typeof(double), typeof(MacroNutritionCardComponent), 20d, propertyChanged: OnPercentChanged);

    public static readonly BindableProperty RecommendedProteinPercentProperty =
        BindableProperty.Create(nameof(RecommendedProteinPercent), typeof(double), typeof(MacroNutritionCardComponent), 25d, propertyChanged: OnPercentChanged);

    public static readonly BindableProperty ActualCarbsPercentProperty =
        BindableProperty.Create(nameof(ActualCarbsPercent), typeof(double), typeof(MacroNutritionCardComponent), 0d, propertyChanged: OnPercentChanged);

    public static readonly BindableProperty ActualFatPercentProperty =
        BindableProperty.Create(nameof(ActualFatPercent), typeof(double), typeof(MacroNutritionCardComponent), 0d, propertyChanged: OnPercentChanged);

    public static readonly BindableProperty ActualProteinPercentProperty =
        BindableProperty.Create(nameof(ActualProteinPercent), typeof(double), typeof(MacroNutritionCardComponent), 0d, propertyChanged: OnPercentChanged);

    private readonly PageThemeHelper _themeHelper;
    private readonly MacroPieChartDrawable _recommendedDrawable;
    private readonly MacroPieChartDrawable _actualDrawable;
    private bool _isExpanded = true;

    public MacroNutritionCardComponent()
    {
        InitializeComponent();
        _themeHelper = new PageThemeHelper();

        _recommendedDrawable = new MacroPieChartDrawable();
        _actualDrawable = new MacroPieChartDrawable();

        BindingContext = this;
        RecommendedChart.Drawable = _recommendedDrawable;
        ActualChart.Drawable = _actualDrawable;

        Loaded += OnComponentLoaded;
        Unloaded += OnComponentUnloaded;
    }

    public double ConsumedCarbs
    {
        get => (double)GetValue(ConsumedCarbsProperty);
        set => SetValue(ConsumedCarbsProperty, value);
    }

    public double ConsumedFat
    {
        get => (double)GetValue(ConsumedFatProperty);
        set => SetValue(ConsumedFatProperty, value);
    }

    public double ConsumedProtein
    {
        get => (double)GetValue(ConsumedProteinProperty);
        set => SetValue(ConsumedProteinProperty, value);
    }

    public double RecommendedCarbsPercent
    {
        get => (double)GetValue(RecommendedCarbsPercentProperty);
        set => SetValue(RecommendedCarbsPercentProperty, value);
    }

    public double RecommendedFatPercent
    {
        get => (double)GetValue(RecommendedFatPercentProperty);
        set => SetValue(RecommendedFatPercentProperty, value);
    }

    public double RecommendedProteinPercent
    {
        get => (double)GetValue(RecommendedProteinPercentProperty);
        set => SetValue(RecommendedProteinPercentProperty, value);
    }

    public double ActualCarbsPercent
    {
        get => (double)GetValue(ActualCarbsPercentProperty);
        set => SetValue(ActualCarbsPercentProperty, value);
    }

    public double ActualFatPercent
    {
        get => (double)GetValue(ActualFatPercentProperty);
        set => SetValue(ActualFatPercentProperty, value);
    }

    public double ActualProteinPercent
    {
        get => (double)GetValue(ActualProteinPercentProperty);
        set => SetValue(ActualProteinPercentProperty, value);
    }

    private void OnComponentLoaded(object? sender, EventArgs e)
    {
        _themeHelper.Initialize();
        UpdateCharts();
    }

    private void OnComponentUnloaded(object? sender, EventArgs e)
    {
        _themeHelper.Cleanup();
    }

    private static void OnPercentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MacroNutritionCardComponent component)
        {
            component.UpdateCharts();
        }
    }

    private void UpdateCharts()
    {
        var carbsColor = TryGetColor("DietStatsCarbsColor", Color.FromArgb("#00C9A7"));
        var fatColor = TryGetColor("DietStatsFatColor", Color.FromArgb("#FFB347"));
        var proteinColor = TryGetColor("DietStatsProteinColor", Color.FromArgb("#8B72FF"));

        _recommendedDrawable.CarbsPercent = RecommendedCarbsPercent;
        _recommendedDrawable.FatPercent = RecommendedFatPercent;
        _recommendedDrawable.ProteinPercent = RecommendedProteinPercent;
        _recommendedDrawable.CarbsColor = carbsColor;
        _recommendedDrawable.FatColor = fatColor;
        _recommendedDrawable.ProteinColor = proteinColor;

        _actualDrawable.CarbsPercent = ActualCarbsPercent;
        _actualDrawable.FatPercent = ActualFatPercent;
        _actualDrawable.ProteinPercent = ActualProteinPercent;
        _actualDrawable.CarbsColor = carbsColor;
        _actualDrawable.FatColor = fatColor;
        _actualDrawable.ProteinColor = proteinColor;

        RecommendedChart.Invalidate();
        ActualChart.Invalidate();
    }

    private async void OnToggleTapped(object? sender, TappedEventArgs e)
    {
        _isExpanded = !_isExpanded;

        if (_isExpanded)
        {
            MacroBody.IsVisible = true;
            MacroBody.Opacity = 0;
            await MacroBody.FadeTo(1, 160, Easing.CubicOut);
        }
        else
        {
            await MacroBody.FadeTo(0, 120, Easing.CubicIn);
            MacroBody.IsVisible = false;
        }
    }

    private static Color TryGetColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color)
        {
            return color;
        }

        return fallback;
    }
}

internal sealed class MacroPieChartDrawable : IDrawable
{
    public double CarbsPercent { get; set; }

    public double FatPercent { get; set; }

    public double ProteinPercent { get; set; }

    public Color CarbsColor { get; set; } = Color.FromArgb("#00C9A7");

    public Color FatColor { get; set; } = Color.FromArgb("#FFB347");

    public Color ProteinColor { get; set; } = Color.FromArgb("#8B72FF");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var total = Math.Max(0.1, CarbsPercent + FatPercent + ProteinPercent);
        var centerX = dirtyRect.Center.X;
        var centerY = dirtyRect.Center.Y;
        var radius = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - 4f;
        var stroke = radius;

        DrawSlice(canvas, centerX, centerY, radius, stroke, 0, (float)(360 * (CarbsPercent / total)), CarbsColor, (float)CarbsPercent);

        var secondStart = (float)(360 * (CarbsPercent / total));
        DrawSlice(canvas, centerX, centerY, radius, stroke, secondStart, (float)(360 * (FatPercent / total)), FatColor, (float)FatPercent);

        var thirdStart = secondStart + (float)(360 * (FatPercent / total));
        DrawSlice(canvas, centerX, centerY, radius, stroke, thirdStart, (float)(360 * (ProteinPercent / total)), ProteinColor, (float)ProteinPercent);
    }

    private static void DrawSlice(ICanvas canvas, float centerX, float centerY, float radius, float strokeSize, float startAngle, float sweepAngle, Color color, float labelValue)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = strokeSize;

        var rect = new RectF(centerX - radius, centerY - radius, radius * 2, radius * 2);
        canvas.DrawArc(rect, startAngle - 90, sweepAngle, false, false);

        if (sweepAngle <= 16)
        {
            return;
        }

        var angle = (startAngle + (sweepAngle / 2f) - 90) * Math.PI / 180f;
        var labelRadius = radius * 0.55f;
        var x = centerX + (float)(Math.Cos(angle) * labelRadius);
        var y = centerY + (float)(Math.Sin(angle) * labelRadius);

        canvas.FontColor = Colors.Black;
        canvas.FontSize = 12;
        canvas.DrawString($"{labelValue:F0}%", x - 16, y - 8, 32, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
