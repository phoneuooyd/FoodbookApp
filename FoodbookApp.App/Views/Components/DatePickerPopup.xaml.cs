using CommunityToolkit.Maui.Views;

namespace Foodbook.Views.Components;

public partial class DatePickerPopup : Popup
{
    private readonly TaskCompletionSource<DateTime?> _tcs = new();

    public Task<DateTime?> ResultTask => _tcs.Task;

    public string TitleText
    {
        get => TitleLabel.Text ?? string.Empty;
        set => TitleLabel.Text = value;
    }

    public string DateLabelText
    {
        get => DateLabel.Text ?? string.Empty;
        set => DateLabel.Text = value;
    }

    public string ConfirmText
    {
        get => ConfirmButton.Text ?? string.Empty;
        set => ConfirmButton.Text = value;
    }

    public string CancelText
    {
        get => CancelButton.Text ?? string.Empty;
        set => CancelButton.Text = value;
    }

    public DateTime SelectedDate
    {
        get => DatePicker.Date;
        set => DatePicker.Date = value;
    }

    public DatePickerPopup()
    {
        InitializeComponent();

        ConfirmButton.Clicked += OnConfirmClicked;
        CancelButton.Clicked += OnCancelClicked;
        Closed += OnPopupClosed;
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(DatePicker.Date);
        }

        await CloseAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
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
