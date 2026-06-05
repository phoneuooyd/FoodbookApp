using CommunityToolkit.Maui.Views;

namespace Foodbook.Views.Components;

public sealed record AddDietMealPopupResult(string MealName, string Calories, string Protein, string Fat, string Carbs);

public partial class AddDietMealPopup : Popup
{
    private readonly TaskCompletionSource<AddDietMealPopupResult?> _tcs = new();

    public AddDietMealPopup()
    {
        InitializeComponent();

        Closed += OnPopupClosed;
    }

    public Task<AddDietMealPopupResult?> ResultTask => _tcs.Task;

    public string MealName { get; set; } = string.Empty;

    public string Calories { get; set; } = string.Empty;

    public string Protein { get; set; } = string.Empty;

    public string Fat { get; set; } = string.Empty;

    public string Carbs { get; set; } = string.Empty;

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        await OnSaveAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await OnCancelAsync();
    }

    private async Task OnSaveAsync()
    {
        MealName = MealNameEntry.Text ?? string.Empty;
        Calories = CaloriesEntry.Text ?? string.Empty;
        Protein = ProteinEntry.Text ?? string.Empty;
        Fat = FatEntry.Text ?? string.Empty;
        Carbs = CarbsEntry.Text ?? string.Empty;

        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(new AddDietMealPopupResult(
                MealName?.Trim() ?? string.Empty,
                Calories?.Trim() ?? string.Empty,
                Protein?.Trim() ?? string.Empty,
                Fat?.Trim() ?? string.Empty,
                Carbs?.Trim() ?? string.Empty));
        }

        await CloseAsync();
    }

    private async Task OnCancelAsync()
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(null);
        }

        await CloseAsync();
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(null);
        }

        Closed -= OnPopupClosed;
    }
}
