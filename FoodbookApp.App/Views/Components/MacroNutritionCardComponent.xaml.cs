using Foodbook.ViewModels;
using Microsoft.Maui.Graphics;

namespace Foodbook.Views.Components;

public partial class MacroNutritionCardComponent : ContentView
{
    public static readonly BindableProperty DataProperty =
        BindableProperty.Create(
            nameof(Data),
            typeof(MacroNutritionCardData),
            typeof(MacroNutritionCardComponent),
            MacroNutritionCardData.Empty,
            propertyChanged: OnDataChanged);

    private bool _isExpanded = true;

    public MacroNutritionCardComponent()
    {
        InitializeComponent();
    }

    public MacroNutritionCardData Data
    {
        get => (MacroNutritionCardData)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    private static void OnDataChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MacroNutritionCardComponent component)
        {
            component.RefreshCharts();
        }
    }

    private void RefreshCharts()
    {
        var data = Data ?? MacroNutritionCardData.Empty;
        var (recommendedCarbs, recommendedFat, recommendedProtein) = ResolveRecommendedPercentages(data);
        var (actualCarbs, actualFat, actualProtein) = ResolveActualPercentages(data);

        RecommendedChart.Drawable = new PieChartDrawable
        {
            Percentages =
            [
                (float)recommendedCarbs,
                (float)recommendedFat,
                (float)recommendedProtein
            ],
            SegmentColors =
            [
                Color.FromArgb("#00C9A7"),
                Color.FromArgb("#FFB347"),
                Color.FromArgb("#8B72FF")
            ],
            IsDonut = true,
            HoleColor = GetHoleColor()
        };

        ActualChart.Drawable = new PieChartDrawable
        {
            Percentages =
            [
                (float)actualCarbs,
                (float)actualFat,
                (float)actualProtein
            ],
            SegmentColors =
            [
                Color.FromArgb("#00C9A7"),
                Color.FromArgb("#FFB347"),
                Color.FromArgb("#8B72FF")
            ],
            IsDonut = true,
            HoleColor = GetHoleColor()
        };

        RecommendedChart.Invalidate();
        ActualChart.Invalidate();
    }

    private static (double carbs, double fat, double protein) ResolveRecommendedPercentages(MacroNutritionCardData data)
    {
        var sum = data.RecommendedCarbsPercent + data.RecommendedFatPercent + data.RecommendedProteinPercent;
        if (sum <= 0)
        {
            return (55, 20, 25);
        }

        return (data.RecommendedCarbsPercent, data.RecommendedFatPercent, data.RecommendedProteinPercent);
    }

    private static (double carbs, double fat, double protein) ResolveActualPercentages(MacroNutritionCardData data)
    {
        var sum = data.ActualCarbsPercent + data.ActualFatPercent + data.ActualProteinPercent;
        if (sum > 0)
        {
            return (data.ActualCarbsPercent, data.ActualFatPercent, data.ActualProteinPercent);
        }

        var macroTotal = data.ConsumedCarbs + data.ConsumedFat + data.ConsumedProtein;
        if (macroTotal <= 0)
        {
            return (0, 0, 0);
        }

        var carbsPercent = Math.Round(data.ConsumedCarbs / macroTotal * 100, 1);
        var fatPercent = Math.Round(data.ConsumedFat / macroTotal * 100, 1);
        var proteinPercent = Math.Round(data.ConsumedProtein / macroTotal * 100, 1);
        return (carbsPercent, fatPercent, proteinPercent);
    }

    private static Color GetHoleColor() =>
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1E1E2E")
            : Colors.White;

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
}
