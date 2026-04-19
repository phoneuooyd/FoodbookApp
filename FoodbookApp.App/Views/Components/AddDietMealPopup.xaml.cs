using System.Windows.Input;
using CommunityToolkit.Maui.Views;

namespace Foodbook.Views.Components;

public sealed record AddDietMealPopupResult(string MealName, string Calories);

public partial class AddDietMealPopup : Popup
{
    private readonly TaskCompletionSource<AddDietMealPopupResult?> _tcs = new();

    public AddDietMealPopup()
    {
        InitializeComponent();

        SaveCommand = new Command(async () => await OnSaveAsync());
        CancelCommand = new Command(async () => await OnCancelAsync());

        Closed += OnPopupClosed;

        BindingContext = this;
    }

    public Task<AddDietMealPopupResult?> ResultTask => _tcs.Task;

    public string MealName { get; set; } = string.Empty;

    public string Calories { get; set; } = string.Empty;

    public ICommand SaveCommand { get; }

    public ICommand CancelCommand { get; }

    private async Task OnSaveAsync()
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(new AddDietMealPopupResult(MealName?.Trim() ?? string.Empty, Calories?.Trim() ?? string.Empty));
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
