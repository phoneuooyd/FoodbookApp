using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Views;
using System.Windows.Input;

namespace Foodbook.Views.Components;

public partial class SearchablePickerPopup : Popup, INotifyPropertyChanged
{
    private readonly List<string> _allItems;
    private readonly TaskCompletionSource<object?> _tcs = new();

    public Task<object?> ResultTask => _tcs.Task;

    // Expose a CloseCommand for X button in XAML
    public ICommand CloseCommand { get; }

    // Clear search command for ModernSearchBarComponent
    public ICommand ClearSearchCommand { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            ApplySearch();
        }
    }

    public SearchablePickerPopup(List<string> allItems, string? selected)
    {
        _allItems = allItems ?? new List<string>();
        CloseCommand = new Command(async () => await CloseWithResultAsync(null));
        ClearSearchCommand = new Command(() => SearchText = string.Empty);
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

    // Replaces Entry.TextChanged handler using two-way bound SearchText
    private void ApplySearch()
    {
        var query = SearchText?.Trim() ?? string.Empty;
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

    // Kept for backward compatibility; no longer wired in XAML
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        SearchText = e.NewTextValue;
    }

    private async Task CloseWithResultAsync(object? result)
    {
        if (!_tcs.Task.IsCompleted)
            _tcs.SetResult(result);
        await CloseAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
