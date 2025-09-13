using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace Foodbook.Views.Components;

public partial class SearchablePickerPopup : Popup
{
    private readonly List<string> _allItems;
    private readonly TaskCompletionSource<object?> _tcs = new();

    public Task<object?> ResultTask => _tcs.Task;

    public SearchablePickerPopup(List<string> allItems, string? selected)
    {
        _allItems = allItems ?? new List<string>();
        InitializeComponent();
        Populate(_allItems);
    }

    private void Populate(IEnumerable<string> items)
    {
        ItemsHost.Children.Clear();
        foreach (var item in items)
        {
            var btn = new Button
            {
                Text = item,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.Transparent,
                TextColor = (Color)Application.Current.Resources["PrimaryText"],
                BorderColor = (Color)Application.Current.Resources["Secondary"],
                BorderWidth = 1,
                CornerRadius = 8,
                Padding = new Thickness(12, 10),
                Margin = new Thickness(2, 3)
            };
            btn.Clicked += async (_, __) => await CloseWithResultAsync(item);
            ItemsHost.Children.Add(btn);
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
