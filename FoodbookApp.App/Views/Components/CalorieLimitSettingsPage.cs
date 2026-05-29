using System.Globalization;
using Foodbook.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace Foodbook.Views.Components;

public sealed class CalorieLimitSettingsPage : ContentPage
{
    private readonly DietStatisticsViewModel _viewModel;
    private readonly Entry _dailyLimitEntry;
    private readonly Entry _lowerPercentEntry;
    private readonly Entry _upperPercentEntry;

    public CalorieLimitSettingsPage(DietStatisticsViewModel viewModel)
    {
        _viewModel = viewModel;
        BackgroundColor = Color.FromRgba(0, 0, 0, 0.45f);
        Padding = new Thickness(16, 24);

        _dailyLimitEntry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            Text = _viewModel.BaseDailyCalorieLimit.ToString("F0", CultureInfo.InvariantCulture),
            Placeholder = "2000"
        };

        _lowerPercentEntry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            Text = (_viewModel.TargetRangeLowerRatio * 100).ToString("F0", CultureInfo.InvariantCulture),
            Placeholder = "90"
        };

        _upperPercentEntry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            Text = (_viewModel.TargetRangeUpperRatio * 100).ToString("F0", CultureInfo.InvariantCulture),
            Placeholder = "110"
        };

        var sheet = new Border
        {
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1F1F30")
                : Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(16),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            Content = BuildSheetContent()
        };

        Content = new Grid
        {
            Children =
            {
                new Grid
                {
                    GestureRecognizers =
                    {
                        new TapGestureRecognizer { Command = new Command(async () => await Navigation.PopModalAsync(true)) }
                    }
                },
                sheet
            }
        };
    }

    private View BuildSheetContent()
    {
        var title = new Label
        {
            Text = "Ustawienia limitu kalorii",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = GetTextColor()
        };

        var subtitle = new Label
        {
            Text = "Limit dzienny będzie mnożony przez wybrany okres (dzień/tydzień/miesiąc/plan/custom).",
            FontSize = 12,
            TextColor = GetSecondaryColor()
        };

        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                title,
                subtitle,
                BuildField("Dzienny limit kcal", _dailyLimitEntry),
                BuildField("Dolny próg celu (%)", _lowerPercentEntry),
                BuildField("Górny próg celu (%)", _upperPercentEntry),
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children =
                    {
                        new Button
                        {
                            Text = "Anuluj",
                            HorizontalOptions = LayoutOptions.Fill,
                            BackgroundColor = Color.FromArgb("#808080"),
                            TextColor = Colors.White,
                            Command = new Command(async () => await Navigation.PopModalAsync(true))
                        },
                        new Button
                        {
                            Text = "Zapisz",
                            HorizontalOptions = LayoutOptions.Fill,
                            BackgroundColor = Color.FromArgb("#8B72FF"),
                            TextColor = Colors.White,
                            Command = new Command(async () => await OnSaveAsync())
                        }
                    }
                }
            }
        };
    }

    private View BuildField(string labelText, Entry entry)
    {
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = labelText,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = GetTextColor()
                },
                new Border
                {
                    StrokeThickness = 1,
                    Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#4A4A62")
                        : Color.FromArgb("#D8D8E3"),
                    BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#2A2A40")
                        : Color.FromArgb("#F0F0F5"),
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = new Thickness(8, 4),
                    Content = entry
                }
            }
        };
    }

    private async Task OnSaveAsync()
    {
        if (!double.TryParse(_dailyLimitEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var daily) || daily <= 0)
        {
            await DisplayAlert("Błąd", "Podaj poprawny dzienny limit kcal.", "OK");
            return;
        }

        if (!double.TryParse(_lowerPercentEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var lowerPercent) ||
            !double.TryParse(_upperPercentEntry.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var upperPercent))
        {
            await DisplayAlert("Błąd", "Podaj poprawne wartości procentowe.", "OK");
            return;
        }

        var lowerRatio = Math.Clamp(lowerPercent / 100.0, 0.5, 1.5);
        var upperRatio = Math.Clamp(upperPercent / 100.0, lowerRatio, 2.0);

        await _viewModel.UpdateCalorieLimitSettingsAsync(daily, lowerRatio, upperRatio);
        await Navigation.PopModalAsync(true);
    }

    private static Color GetTextColor()
        => Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Color.FromArgb("#1E1E2E");

    private static Color GetSecondaryColor()
        => Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#B0B0C0") : Color.FromArgb("#606070");
}
