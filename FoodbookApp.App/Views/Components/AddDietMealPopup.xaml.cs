using CommunityToolkit.Maui.Views;
using Foodbook.Models.DTOs;
using FoodbookApp.Interfaces;
using Foodbook.Views;

namespace Foodbook.Views.Components;

public sealed record AddDietMealPopupResult(
    Guid? MealId,
    string MealName,
    string Calories,
    string Protein,
    string Fat,
    string Carbs,
    string Weight);

public partial class AddDietMealPopup : Popup
{
    private readonly IOpenFoodFactsService _openFoodFactsService;
    private readonly TaskCompletionSource<AddDietMealPopupResult?> _tcs = new();
    private bool _isScannerFlowActive;
    private readonly Guid? _editingMealId;

    public static readonly BindableProperty PopupTitleProperty =
        BindableProperty.Create(nameof(PopupTitle), typeof(string), typeof(AddDietMealPopup), "Add meal");

    public string PopupTitle
    {
        get => (string)GetValue(PopupTitleProperty);
        set => SetValue(PopupTitleProperty, value);
    }

    public AddDietMealPopup(DietStatisticsMealDto? editingMeal = null)
    {
        InitializeComponent();
        _openFoodFactsService = IPlatformApplication.Current!.Services.GetRequiredService<IOpenFoodFactsService>();
        Closed += OnPopupClosed;

        if (editingMeal != null)
        {
            _editingMealId = editingMeal.Id;
            PrefillFromMeal(editingMeal);
        }
    }

    public Task<AddDietMealPopupResult?> ResultTask => _tcs.Task;

    private void PrefillFromMeal(DietStatisticsMealDto meal)
    {
        var safeWeight = meal.Weight > 0 ? meal.Weight : 100;
        var multiplier = safeWeight / 100.0;

        MealNameEntry.Text = meal.Name ?? string.Empty;
        WeightEntry.Text = safeWeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        CaloriesEntry.Text = multiplier > 0
            ? (meal.Calories / multiplier).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : meal.Calories.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        ProteinEntry.Text = multiplier > 0
            ? (meal.Protein / multiplier).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : meal.Protein.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        FatEntry.Text = multiplier > 0
            ? (meal.Fat / multiplier).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : meal.Fat.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        CarbsEntry.Text = multiplier > 0
            ? (meal.Carbs / multiplier).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            : meal.Carbs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
    }

    private async void OnSaveClicked(object? sender, EventArgs e) => await OnSaveAsync();
    private async void OnCancelClicked(object? sender, EventArgs e) => await OnCancelAsync();

    private async void OnScannerClicked(object? sender, EventArgs e) => await OnScannerAsync();

    private async Task OnScannerAsync()
    {
        _isScannerFlowActive = true;
        try
        {
            var sp = IPlatformApplication.Current!.Services;
            var scannerPage = sp.GetRequiredService<Foodbook.Views.BarcodeScannerPage>();
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null) return;

            NavigationPage.SetHasNavigationBar(scannerPage, false);
            await page.Navigation.PushModalAsync(new NavigationPage(scannerPage));

            var product = await scannerPage.ScannerTask;
            if (product == null) return;

            if (!string.IsNullOrWhiteSpace(product.Name))
                MealNameEntry.Text = product.Name;

            if (product.Calories100g >= 0)
                CaloriesEntry.Text = product.Calories100g.ToString("F1");
            if (product.Protein100g >= 0)
                ProteinEntry.Text = product.Protein100g.ToString("F1");
            if (product.Fat100g >= 0)
                FatEntry.Text = product.Fat100g.ToString("F1");
            if (product.Carbs100g >= 0)
                CarbsEntry.Text = product.Carbs100g.ToString("F1");

            WeightEntry.Text = "100";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddDietMealPopup] Scanner error: {ex.Message}");
        }
        finally
        {
            _isScannerFlowActive = false;
        }
    }

    private async Task OnSaveAsync()
    {
        var mealName = MealNameEntry.Text ?? string.Empty;
        var calories = CaloriesEntry.Text ?? string.Empty;
        var protein = ProteinEntry.Text ?? string.Empty;
        var fat = FatEntry.Text ?? string.Empty;
        var carbs = CarbsEntry.Text ?? string.Empty;
        var weight = WeightEntry.Text ?? "100";

        System.Diagnostics.Debug.WriteLine($"[AddDietMealPopup] SAVE: name='{mealName}' cal='{calories}' pro='{protein}' fat='{fat}' carbs='{carbs}' weight='{weight}' edit={_editingMealId.HasValue}");

        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(new AddDietMealPopupResult(
                _editingMealId,
                mealName?.Trim() ?? string.Empty,
                calories?.Trim() ?? string.Empty,
                protein?.Trim() ?? string.Empty,
                fat?.Trim() ?? string.Empty,
                carbs?.Trim() ?? string.Empty,
                weight?.Trim() ?? "100"));
            System.Diagnostics.Debug.WriteLine("[AddDietMealPopup] TCS result set — popup will close");
        }

        await CloseAsync();
    }

    private async Task OnCancelAsync()
    {
        if (!_tcs.Task.IsCompleted)
            _tcs.SetResult(null);

        await CloseAsync();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_isScannerFlowActive)
        {
            System.Diagnostics.Debug.WriteLine("[AddDietMealPopup] OnPopupClosed ignored — barcode scanner flow active");
            return;
        }

        Closed -= OnPopupClosed;

        if (!_tcs.Task.IsCompleted)
            _tcs.SetResult(null);
    }
}
