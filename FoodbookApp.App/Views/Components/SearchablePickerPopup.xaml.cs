using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace Foodbook.Views.Components;

public partial class SearchablePickerPopup : Popup
{
    private readonly List<string> _allItems;
    private readonly TaskCompletionSource<object?> _tcs = new();

    public Task<object?> ResultTask => _tcs.Task;

    // Expose a CloseCommand for X and Cancel buttons in XAML
    public ICommand CloseCommand { get; }

    public SearchablePickerPopup(List<string> allItems, string? selected)
    {
        _allItems = allItems ?? new List<string>();
        CloseCommand = new Command(async () => await CloseWithResultAsync(null));
        InitializeComponent();
        Populate(_allItems);
    }

    private void Populate(IEnumerable<string> items)
    {
        var host = ItemsHost;
        if (host == null)
            return;

        host.Children.Clear();

        var app = Application.Current;
        var primaryText = (app?.Resources?.TryGetValue("PrimaryText", out var v1) == true && v1 is Color c1) ? c1 : Colors.Black;
        var secondary = (app?.Resources?.TryGetValue("Secondary", out var v2) == true && v2 is Color c2) ? c2 : Colors.Gray;

        foreach (var item in items)
        {
            var btn = new Button
            {
                Text = item,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Transparent,
                TextColor = primaryText,
                BorderColor = secondary,
                BorderWidth = 1,
                CornerRadius = 8,
                Padding = new Thickness(12, 10),
                Margin = new Thickness(2, 3)
            };
            btn.Clicked += async (_, __) => await CloseWithResultAsync(item);
            host.Children.Add(btn);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            Populate(_allItems);
            return;
        }
        var filtered = _allItems
            .Where(x => x?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();
        Populate(filtered);
    }

    private async Task CloseWithResultAsync(object? result)
    {
        if (!_tcs.Task.IsCompleted)
            _tcs.SetResult(result);
        await CloseAsync();
    }
}
