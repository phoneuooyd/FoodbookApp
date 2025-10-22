using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using Foodbook.Models;
using Microsoft.Maui.Graphics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;

namespace Foodbook.Views.Components;

public class FilterSortResult
{
    public SortOrder SortOrder { get; set; } = SortOrder.Asc;
    public List<int> SelectedLabelIds { get; set; } = new();
    // NEW: multi-ingredient filtering support (by name)
    public List<string> SelectedIngredientNames { get; set; } = new();
}

public class FilterSortPopup : Popup
{
    private readonly bool _showLabels;
    private readonly ObservableCollection<RecipeLabel> _labels;
    private readonly HashSet<int> _selected;

    // NEW: ingredients support
    private readonly bool _showIngredients;
    private readonly List<Ingredient> _allIngredients;
    private readonly HashSet<string> _selectedIngredientNames; // compare by name

    private readonly Picker _sortPicker;
    private readonly Grid _labelsHost;

    // NEW: UI for ingredients
    private readonly VerticalStackLayout _ingredientsHost;
    private readonly Entry _ingredientSearchEntry;

    private readonly TaskCompletionSource<FilterSortResult?> _tcs = new();
    public Task<FilterSortResult?> ResultTask => _tcs.Task;

    public FilterSortPopup(
        bool showLabels,
        IEnumerable<RecipeLabel>? labels,
        IEnumerable<int>? preselectedLabelIds,
        SortOrder sortOrder,
        bool showIngredients = false,
        IEnumerable<Ingredient>? ingredients = null,
        IEnumerable<string>? preselectedIngredientNames = null)
    {
        _showLabels = showLabels;
        _labels = new ObservableCollection<RecipeLabel>(labels ?? Enumerable.Empty<RecipeLabel>());
        _selected = new HashSet<int>(preselectedLabelIds ?? Enumerable.Empty<int>());

        _showIngredients = showIngredients;
        _allIngredients = (ingredients ?? Enumerable.Empty<Ingredient>()).ToList();
        _selectedIngredientNames = new HashSet<string>(preselectedIngredientNames ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);

        CanBeDismissedByTappingOutsideOfPopup = true;
        Padding = 0; // match SimpleListPopup
        Margin = 0;  // match SimpleListPopup

        _sortPicker = new Picker { Title = "Sortuj", ItemsSource = new List<string> { "A-Z", "Z-A" } };
        _sortPicker.SelectedIndex = sortOrder == SortOrder.Desc ? 1 : 0;

        // Labels host as 2-column grid
        _labelsHost = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition{ Width = GridLength.Star },
                new ColumnDefinition{ Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            RowSpacing = 6
        };

        // NEW: ingredients UI containers
        _ingredientsHost = new VerticalStackLayout { Spacing = 6 };
        _ingredientSearchEntry = new Entry
        {
            Placeholder = "Szukaj sk³adników...",
            IsVisible = _showIngredients,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        _ingredientSearchEntry.TextChanged += (_, __) => BuildIngredients();

        Content = BuildContent();

        if (_showLabels)
        {
            BuildLabels();
        }
        if (_showIngredients)
        {
            BuildIngredients();
        }
    }

    private View BuildContent()
    {
        // Calculate popup width similar to SimpleListPopup
        double displayWidth = DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density;
        double popupWidth = Math.Min(displayWidth * 0.92, 560);

        // Top bar with title and close (X)
        var title = new Label
        {
            Text = "Sortowanie i filtrowanie",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        title.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        var closeBtn = new Button
        {
            Text = "X", // use alphanumeric X for cross
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = new Thickness(0),
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        closeBtn.SetDynamicResource(Button.TextColorProperty, "PrimaryText");
        closeBtn.Clicked += async (_, __) =>
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.SetResult(null);
            await CloseAsync();
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition{ Width = GridLength.Star },
                new ColumnDefinition{ Width = GridLength.Auto }
            },
            Padding = new Thickness(0,0,0,6)
        };
        header.Add(title, 0, 0);
        header.Add(closeBtn, 1, 0);

        var sortRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition{ Width = GridLength.Star },
                new ColumnDefinition{ Width = GridLength.Auto }
            },
            Margin = new Thickness(0,8)
        };
        sortRow.Add(new Label { Text = "Sortuj alfabetycznie", VerticalOptions = LayoutOptions.Center }, 0, 0);
        sortRow.Add(_sortPicker,1,0);

        var labelsHeader = new Label
        {
            Text = "Filtruj po etykietach",
            FontSize = 16,
            Margin = new Thickness(0,12,0,6),
            IsVisible = _showLabels
        };
        labelsHeader.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        // Reduce labels area; same scroll behavior as ingredients
        var labelsScroll = new ScrollView { Content = _labelsHost, IsVisible = _showLabels, HeightRequest = 120 };

        // NEW: ingredients header + search + list (increase area)
        var ingredientsHeader = new Label
        {
            Text = "Filtruj po sk³adnikach",
            FontSize = 16,
            Margin = new Thickness(0,12,0,6),
            IsVisible = _showIngredients
        };
        ingredientsHeader.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        var ingredientsScroll = new ScrollView { Content = _ingredientsHost, IsVisible = _showIngredients, HeightRequest = 280 };

        var ok = new Button { Text = "Zastosuj" };
        ok.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
        ok.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");
        ok.Clicked += async (_, __) =>
        {
            try
            {
                var result = new FilterSortResult
                {
                    SortOrder = _sortPicker.SelectedIndex == 1 ? SortOrder.Desc : SortOrder.Asc,
                    SelectedLabelIds = _selected.ToList(),
                    SelectedIngredientNames = _selectedIngredientNames.ToList()
                };
                if (!_tcs.Task.IsCompleted)
                    _tcs.SetResult(result);
            }
            finally
            {
                await CloseAsync();
            }
        };

        var clear = new Button { Text = "Wyczyœæ" };
        clear.StyleClass = new List<string> { "Secondary" };
        clear.Clicked += (_, __) =>
        {
            _sortPicker.SelectedIndex = 0;
            _selected.Clear();
            _selectedIngredientNames.Clear();
            _ingredientSearchEntry.Text = string.Empty;

            // update visuals
            foreach (var chip in _labelsHost.Children.OfType<Border>())
            {
                chip.Stroke = Colors.Transparent;
            }
            foreach (var chip in _ingredientsHost.Children.OfType<Border>())
            {
                chip.Stroke = Colors.Transparent;
            }
        };

        var buttons = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End, // align buttons group to the right
            Children = { clear, ok }
        };

        var body = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                header,
                sortRow,
                labelsHeader,
                labelsScroll,
                ingredientsHeader,
                _ingredientSearchEntry,
                ingredientsScroll,
                buttons
            }
        };

        var outer = new Border
        {
            Padding = 16,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = body,
            WidthRequest = popupWidth,
            MaximumWidthRequest = 560,
            MaximumHeightRequest = 560
        };
        outer.SetDynamicResource(Border.BackgroundColorProperty, "PageBackgroundColor");
        outer.SetDynamicResource(Border.StrokeProperty, "Secondary");

        return outer;
    }

    private void BuildLabels()
    {
        _labelsHost.Children.Clear();
        _labelsHost.RowDefinitions.Clear();

        // two chips per row
        int count = _labels.Count;
        int rows = (count + 1) / 2;
        for (int r = 0; r < rows; r++)
            _labelsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < count; i++)
        {
            var lbl = _labels[i];
            var chip = BuildLabelChip(lbl);
            int row = i / 2;
            int col = i % 2;
            _labelsHost.Add(chip, col, row);
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

    // NEW: Build ingredients chips with search support
    private void BuildIngredients()
    {
        _ingredientsHost.Children.Clear();
        IEnumerable<Ingredient> source = _allIngredients;
        var query = _ingredientSearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(i => i.Name?.Contains(query, System.StringComparison.OrdinalIgnoreCase) == true);
        }

        foreach (var ing in source.OrderBy(i => i.Name, System.StringComparer.CurrentCultureIgnoreCase))
        {
            var chip = BuildIngredientChip(ing);
            _ingredientsHost.Children.Add(chip);
        }
    }

    private View BuildIngredientChip(Ingredient ingredient)
    {
        var border = new Border
        {
            StrokeThickness = 2,
            Padding = new Thickness(10,6),
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Margin = new Thickness(0,2)
        };
        var isSelected = _selectedIngredientNames.Contains(ingredient.Name);
        border.Stroke = isSelected ? GetPrimary() : Colors.Transparent;

        var name = new Label { Text = ingredient.Name, VerticalOptions = LayoutOptions.Center };
        name.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
        border.Content = name;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            if (_selectedIngredientNames.Contains(ingredient.Name))
            {
                _selectedIngredientNames.Remove(ingredient.Name);
                border.Stroke = Colors.Transparent;
            }
            else
            {
                _selectedIngredientNames.Add(ingredient.Name);
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
