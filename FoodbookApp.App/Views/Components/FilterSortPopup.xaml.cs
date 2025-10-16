using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using Foodbook.Models;
using Microsoft.Maui.Graphics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Shapes;

namespace Foodbook.Views.Components;

public class FilterSortResult
{
    public bool SortByName { get; set; }
    public List<int> SelectedLabelIds { get; set; } = new();
}

public class FilterSortPopup : Popup
{
    private readonly bool _showLabels;
    private readonly ObservableCollection<RecipeLabel> _labels;
    private readonly HashSet<int> _selected;

    private readonly Switch _sortSwitch;
    private readonly VerticalStackLayout _labelsHost;

    private readonly TaskCompletionSource<FilterSortResult?> _tcs = new();
    public Task<FilterSortResult?> ResultTask => _tcs.Task;

    public FilterSortPopup(bool showLabels, IEnumerable<RecipeLabel>? labels, IEnumerable<int>? preselectedLabelIds, bool sortByName)
    {
        _showLabels = showLabels;
        _labels = new ObservableCollection<RecipeLabel>(labels ?? Enumerable.Empty<RecipeLabel>());
        _selected = new HashSet<int>(preselectedLabelIds ?? Enumerable.Empty<int>());

        CanBeDismissedByTappingOutsideOfPopup = true;

        _sortSwitch = new Switch { IsToggled = sortByName };
        _labelsHost = new VerticalStackLayout { Spacing = 6 };

        Content = BuildContent();

        if (_showLabels)
        {
            BuildLabels();
        }
    }

    private View BuildContent()
    {
        var title = new Label
        {
            Text = "Sortowanie i filtrowanie",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        };
        title.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        var sortRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition{ Width = GridLength.Star },
                new ColumnDefinition{ Width = GridLength.Auto }
            },
            Margin = new Thickness(0,8)
        };
        sortRow.Add(new Label { Text = "Sortuj alfabetycznie (A?Z)", VerticalOptions = LayoutOptions.Center });
        sortRow.Add(_sortSwitch,1,0);

        var labelsHeader = new Label
        {
            Text = "Filtruj po etykietach",
            FontSize = 16,
            Margin = new Thickness(0,12,0,6),
            IsVisible = _showLabels
        };
        labelsHeader.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        var scroll = new ScrollView { Content = _labelsHost, IsVisible = _showLabels, HeightRequest = 260 };

        var ok = new Button { Text = "Zastosuj" };
        ok.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
        ok.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");
        ok.Clicked += async (_, __) =>
        {
            try
            {
                var result = new FilterSortResult
                {
                    SortByName = _sortSwitch.IsToggled,
                    SelectedLabelIds = _selected.ToList()
                };
                if (!_tcs.Task.IsCompleted)
                    _tcs.SetResult(result);
            }
            finally
            {
                await CloseAsync();
            }
        };

        var clear = new Button { Text = "Wyczyœæ", BackgroundColor = Color.FromArgb("#E0E0E0") };
        clear.SetDynamicResource(Button.TextColorProperty, "PrimaryText");
        clear.Clicked += (_, __) =>
        {
            _sortSwitch.IsToggled = false;
            _selected.Clear();
            // update visuals
            foreach (var chip in _labelsHost.Children.OfType<Border>())
            {
                chip.Stroke = Colors.Transparent;
            }
        };

        var buttons = new HorizontalStackLayout
        {
            Spacing = 12,
            Children = { clear, ok }
        };

        var body = new VerticalStackLayout
        {
            Spacing = 6,
            Children = { title, sortRow, labelsHeader, scroll, buttons }
        };

        var outer = new Border
        {
            Padding = 16,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = body
        };
        outer.SetDynamicResource(Border.BackgroundColorProperty, "PageBackgroundColor");
        outer.SetDynamicResource(Border.StrokeProperty, "Secondary");

        return outer;
    }

    private void BuildLabels()
    {
        _labelsHost.Children.Clear();
        foreach (var lbl in _labels)
        {
            var chip = BuildLabelChip(lbl);
            _labelsHost.Children.Add(chip);
        }
    }

    private View BuildLabelChip(RecipeLabel label)
    {
        var border = new Border
        {
            StrokeThickness = 2,
            Padding = new Thickness(10,6),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Margin = new Thickness(0,2)
        };
        var isSelected = _selected.Contains(label.Id);
        border.Stroke = isSelected ? GetPrimary() : Colors.Transparent;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition{ Width = GridLength.Auto },
                new ColumnDefinition{ Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };
        var dot = new Border
        {
            WidthRequest = 14,
            HeightRequest = 14,
            BackgroundColor = Color.FromArgb(label.ColorHex ?? "#757575"),
            StrokeShape = new RoundRectangle { CornerRadius = 7 },
            StrokeThickness = 0
        };
        var name = new Label { Text = label.Name, VerticalOptions = LayoutOptions.Center };
        name.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        row.Add(dot,0,0);
        row.Add(name,1,0);
        border.Content = row;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            if (_selected.Contains(label.Id))
            {
                _selected.Remove(label.Id);
                border.Stroke = Colors.Transparent;
            }
            else
            {
                _selected.Add(label.Id);
                border.Stroke = GetPrimary();
            }
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private Color GetPrimary()
    {
        if (Application.Current?.Resources.TryGetValue("Primary", out var v) == true && v is Color c)
            return c;
        return Color.FromArgb("#512BD4");
    }
}
