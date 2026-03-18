using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using Foodbook.Models;
using Microsoft.Maui.Graphics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;
using System.Threading;
using Microsoft.Maui.Layouts;

namespace Foodbook.Views.Components;

public enum SortBy
{
    NameAsc,
    NameDesc,
    CaloriesAsc,
    CaloriesDesc,
    ProteinAsc,
    ProteinDesc,
    CarbsAsc,
    CarbsDesc,
    FatAsc,
    FatDesc
}

public class FilterSortResult
{
    public SortOrder SortOrder { get; set; } = SortOrder.Asc;
    public SortBy SortBy { get; set; } = SortBy.NameAsc;
    public List<Guid> SelectedLabelIds { get; set; } = new();
    public List<string> SelectedIngredientNames { get; set; } = new();
}

public class FilterSortPopup : ContentPage
{
    private static int _openFlag = 0;
    public static bool TryAcquireOpen() => Interlocked.CompareExchange(ref _openFlag, 1, 0) == 0;
    public static void ReleaseOpen() => Interlocked.Exchange(ref _openFlag, 0);

    private readonly bool _showLabels;
    private readonly ObservableCollection<RecipeLabel> _labels;
    private readonly HashSet<Guid> _selected;

    private readonly bool _showIngredients;
    private readonly List<Ingredient> _allIngredients;
    private readonly HashSet<string> _selectedIngredientNames;

    private readonly VerticalStackLayout _sortOptionsHost;
    private readonly Grid _labelsHost;
    private readonly CollectionView _ingredientsList;
    private readonly Entry _ingredientSearchEntry;
    private readonly bool _showApplyButton;
    private readonly bool _useFullScreenSheet;

    private readonly ObservableCollection<Ingredient> _visibleIngredients = new();

    private readonly Dictionary<SortBy, Border> _sortOptionBorders = new();
    private readonly Dictionary<SortBy, Label> _sortOptionTextLabels = new();
    private readonly Dictionary<SortBy, Label> _sortOptionBadgeLabels = new();

    private SortBy _selectedSortBy;

    private Label? _titleLabel;
    private Label? _sortHeaderLabel;
    private Label? _labelsHeaderLabel;
    private Label? _ingredientsHeaderLabel;
    private Button? _okButton;
    private Button? _clearButton;

    private readonly TaskCompletionSource<FilterSortResult?> _tcs = new();
    private bool _isClosing;
    private BoxView? _dimView;
    private Border? _sheetView;
    public Task<FilterSortResult?> ResultTask => _tcs.Task;

    public FilterSortPopup(
        bool showLabels,
        IEnumerable<RecipeLabel>? labels,
        IEnumerable<Guid>? preselectedLabelIds,
        SortOrder sortOrder,
        bool showIngredients = false,
        IEnumerable<Ingredient>? ingredients = null,
        IEnumerable<string>? preselectedIngredientNames = null,
        SortBy? sortBy = null,
        bool showApplyButton = true,
        bool useFullScreenSheet = false)
    {
        _showLabels = showLabels;
        _labels = new ObservableCollection<RecipeLabel>(labels ?? Enumerable.Empty<RecipeLabel>());
        _selected = new HashSet<Guid>(preselectedLabelIds ?? Enumerable.Empty<Guid>());

        _showIngredients = showIngredients;
        _allIngredients = (ingredients ?? Enumerable.Empty<Ingredient>()).ToList();
        _selectedIngredientNames = new HashSet<string>(preselectedIngredientNames ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);

        _showApplyButton = showApplyButton;
        _useFullScreenSheet = useFullScreenSheet;
        _selectedSortBy = sortBy ?? MapOrderToSortBy(sortOrder);

        BackgroundColor = Colors.Transparent;
        Shell.SetNavBarIsVisible(this, false);

        _sortOptionsHost = new VerticalStackLayout { Spacing = 6 };

        _labelsHost = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        _ingredientsList = new CollectionView
        {
            ItemsSource = _visibleIngredients,
            SelectionMode = SelectionMode.None,
            BackgroundColor = Colors.Transparent,
            ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
            VerticalOptions = LayoutOptions.FillAndExpand,
            Margin = new Thickness(20, 0, 20, 0),
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 4 }
        };
        _ingredientsList.ItemTemplate = new DataTemplate(() => new IngredientFilterItemView(this));

        _ingredientSearchEntry = new Entry
        {
            Placeholder = FoodbookApp.Localization.FilterSortPopupResources.IngredientSearchPlaceholder,
            IsVisible = _showIngredients,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
            BackgroundColor = Colors.Transparent,
            Margin = new Thickness(0),
            HorizontalOptions = LayoutOptions.Fill,
            ReturnType = ReturnType.Search
        };
        _ingredientSearchEntry.TextChanged += (_, __) => BuildIngredients();

        Content = BuildContent();

        BuildSortOptions();
        if (_showLabels) BuildLabels();
        if (_showIngredients) BuildIngredients();

        // Subscribe to picker refresh requests from localization service so UI updates when culture changes
        var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService(typeof(FoodbookApp.Interfaces.ILocalizationService)) as FoodbookApp.Interfaces.ILocalizationService;
        if (loc != null)
        {
            loc.PickerRefreshRequested += OnPickerRefreshRequested;
        }

        // Only release the flag once popup is closed
        Disappearing += (_, __) =>
        {
            // Unsubscribe localization event
            try
            {
                if (loc != null)
                    loc.PickerRefreshRequested -= OnPickerRefreshRequested;
            }
            catch { }

            if (!_showApplyButton)
            {
                var result = GetResult();
                if (!_tcs.Task.IsCompleted)
                    _tcs.SetResult(result);
            }

            ReleaseOpen();
        };

        // Ensure initial localization is consistent (in case culture changed before popup created)
        UpdateLocalizedStrings();
    }

    private void OnPickerRefreshRequested(object? sender, EventArgs e)
    {
        try
        {
            // Ensure execution on UI thread
            MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateLocalizedStrings();
                return Task.CompletedTask;
            });
        }
        catch { }
    }

    private void UpdateLocalizedStrings()
    {
        try
        {
            if (_titleLabel != null) _titleLabel.Text = FoodbookApp.Localization.FilterSortPopupResources.Title;
            if (_sortHeaderLabel != null) _sortHeaderLabel.Text = FoodbookApp.Localization.FilterSortPopupResources.SortLabel;
            if (_labelsHeaderLabel != null) _labelsHeaderLabel.Text = FoodbookApp.Localization.FilterSortPopupResources.LabelsHeader;
            if (_ingredientsHeaderLabel != null) _ingredientsHeaderLabel.Text = FoodbookApp.Localization.FilterSortPopupResources.IngredientsHeader;
            if (_ingredientSearchEntry != null) _ingredientSearchEntry.Placeholder = FoodbookApp.Localization.FilterSortPopupResources.IngredientSearchPlaceholder;
            if (_okButton != null) _okButton.Text = FoodbookApp.Localization.FilterSortPopupResources.ApplyButton;
            if (_clearButton != null) _clearButton.Text = FoodbookApp.Localization.FilterSortPopupResources.ClearButton;

            foreach (var sort in Enum.GetValues<SortBy>())
            {
                if (_sortOptionTextLabels.TryGetValue(sort, out var text))
                    text.Text = GetSortOptionText(sort);
                if (_sortOptionBadgeLabels.TryGetValue(sort, out var badge))
                    badge.Text = GetSortOptionBadge(sort);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FilterSortPopup] UpdateLocalizedStrings error: {ex.Message}");
        }
    }

    private static SortBy MapOrderToSortBy(SortOrder order) => order == SortOrder.Desc ? SortBy.NameDesc : SortBy.NameAsc;

    private View BuildContent()
    {
        double displayWidth = DeviceDisplay.Current.MainDisplayInfo.Width / DeviceDisplay.Current.MainDisplayInfo.Density;
        double displayHeight = DeviceDisplay.Current.MainDisplayInfo.Height / DeviceDisplay.Current.MainDisplayInfo.Density;
        double sheetRatio = (_showIngredients || _useFullScreenSheet) ? 0.94 : 0.82;
        double sheetMaxHeight = Math.Min(displayHeight * sheetRatio, displayHeight - 12);
        double sortMaxHeight = Math.Min(_showIngredients ? displayHeight * 0.32 : displayHeight * 0.56, _showIngredients ? 280 : 420);
        double labelsMaxHeight = Math.Min(displayHeight * 0.16, 136);

        var dim = new BoxView
        {
            BackgroundColor = Color.FromRgba(10, 10, 24, 0.48),
            InputTransparent = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        _dimView = dim;
        var dimTap = new TapGestureRecognizer();
        dimTap.Tapped += async (_, __) => await CloseWithResultAsync(null);
        dim.GestureRecognizers.Add(dimTap);

        var handle = new Border
        {
            WidthRequest = 36,
            HeightRequest = 4,
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            BackgroundColor = Color.FromRgba(255, 255, 255, 0.16),
            StrokeShape = new RoundRectangle { CornerRadius = 2 },
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        _titleLabel = new Label
        {
            Text = FoodbookApp.Localization.FilterSortPopupResources.Title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        _titleLabel.SetDynamicResource(Label.TextColorProperty, "PrimaryText");

        var closeBtn = new Button
        {
            Text = "×",
            WidthRequest = 32,
            HeightRequest = 32,
            Padding = new Thickness(0),
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        closeBtn.SetDynamicResource(Button.TextColorProperty, "SecondaryText");
        closeBtn.Clicked += async (_, __) => await SubmitAndCloseAsync();

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Padding = new Thickness(20, 14, 20, 12)
        };
        header.Children.Add(_titleLabel);
        Grid.SetColumn(_titleLabel, 0);
        Grid.SetRow(_titleLabel, 0);

        header.Children.Add(closeBtn);
        Grid.SetColumn(closeBtn, 1);
        Grid.SetRow(closeBtn, 0);

        _sortHeaderLabel = BuildSectionHeader(FoodbookApp.Localization.FilterSortPopupResources.SortLabel);
        _labelsHeaderLabel = BuildSectionHeader(FoodbookApp.Localization.FilterSortPopupResources.LabelsHeader);
        _labelsHeaderLabel.IsVisible = _showLabels;
        _ingredientsHeaderLabel = BuildSectionHeader(FoodbookApp.Localization.FilterSortPopupResources.IngredientsHeader);
        _ingredientsHeaderLabel.IsVisible = _showIngredients;

        var sortScroll = new ScrollView
        {
            Content = _sortOptionsHost,
            MaximumHeightRequest = sortMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };

        var labelsScroll = new ScrollView
        {
            Content = _labelsHost,
            IsVisible = _showLabels,
            MaximumHeightRequest = labelsMaxHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };

        var ingredientsSearchGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };
        var searchIcon = new Label
        {
            Text = "?",
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 14,
            Opacity = 0.85
        };
        ingredientsSearchGrid.Children.Add(searchIcon);
        Grid.SetColumn(searchIcon, 0);
        Grid.SetRow(searchIcon, 0);

        ingredientsSearchGrid.Children.Add(_ingredientSearchEntry);
        Grid.SetColumn(_ingredientSearchEntry, 1);
        Grid.SetRow(_ingredientSearchEntry, 0);

        var ingredientsSearchContainer = new Border
        {
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 0, 0, 6),
            IsVisible = _showIngredients,
            Content = ingredientsSearchGrid
        };
        ingredientsSearchContainer.SetDynamicResource(Border.BackgroundColorProperty, "OverhaulCardBackgroundColor");
        ingredientsSearchContainer.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");

        var topSections = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = new Thickness(20, 16, 20, 12),
            Children =
            {
                _sortHeaderLabel,
                sortScroll,
                _labelsHeaderLabel,
                labelsScroll
            }
        };

        if (_showIngredients)
        {
            topSections.Children.Add(_ingredientsHeaderLabel);
            topSections.Children.Add(ingredientsSearchContainer);
        }

        var bodyGrid = new Grid();
        if (_showIngredients)
        {
            bodyGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            };

            bodyGrid.Children.Add(topSections);
            Grid.SetRow(topSections, 0);
            bodyGrid.Children.Add(_ingredientsList);
            Grid.SetRow(_ingredientsList, 1);
        }
        else
        {
            bodyGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Star }
            };

            var topScroll = new ScrollView
            {
                Content = topSections,
                VerticalScrollBarVisibility = ScrollBarVisibility.Never
            };

            bodyGrid.Children.Add(topScroll);
            Grid.SetRow(topScroll, 0);
        }

        _okButton = new Button
        {
            Text = FoodbookApp.Localization.FilterSortPopupResources.ApplyButton,
            HeightRequest = 46,
            CornerRadius = 12,
            IsVisible = _showApplyButton
        };
        _okButton.SetDynamicResource(Button.BackgroundColorProperty, "Primary");
        _okButton.SetDynamicResource(Button.TextColorProperty, "ButtonPrimaryText");
        _okButton.Clicked += async (_, __) => await SubmitAndCloseAsync();

        _clearButton = new Button
        {
            Text = FoodbookApp.Localization.FilterSortPopupResources.ClearButton,
            HeightRequest = 38,
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill
        };
        _clearButton.SetDynamicResource(Button.TextColorProperty, "SecondaryText");
        _clearButton.Clicked += async (_, __) => await ResetSelectionAsync();

        var footer = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(20, 0, 20, 24),
            Children = { _okButton, _clearButton }
        };

        var sheetGrid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };
        sheetGrid.Children.Add(handle);
        Grid.SetColumn(handle, 0);
        Grid.SetRow(handle, 0);

        sheetGrid.Children.Add(header);
        Grid.SetColumn(header, 0);
        Grid.SetRow(header, 1);

        sheetGrid.Children.Add(bodyGrid);
        Grid.SetColumn(bodyGrid, 0);
        Grid.SetRow(bodyGrid, 2);

        sheetGrid.Children.Add(footer);
        Grid.SetColumn(footer, 0);
        Grid.SetRow(footer, 3);

        var sheet = new Border
        {
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28, 28, 0, 0) },
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            MaximumHeightRequest = sheetMaxHeight,
            Content = sheetGrid
        };
        sheet.BackgroundColor = ResolvePopupBackgroundColor();
        _sheetView = sheet;

        var root = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Padding = new Thickness(0)
        };

        root.Children.Add(dim);
        Grid.SetRow(dim, 0);
        Grid.SetRowSpan(dim, 2);

        root.Children.Add(sheet);
        Grid.SetRow(sheet, 1);

        return root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            activity?.Window?.SetBackgroundDrawable(new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.Transparent));
        }
        catch { }
#endif

        try
        {
            if (_dimView != null)
                _dimView.Opacity = 0;

            if (_sheetView != null)
            {
                _sheetView.Opacity = 0;
                _sheetView.TranslationY = 42;
            }

            var dimAnim = _dimView?.FadeTo(1, 180, Easing.CubicOut) ?? Task.CompletedTask;
            var sheetFade = _sheetView?.FadeTo(1, 200, Easing.CubicOut) ?? Task.CompletedTask;
            var sheetSlide = _sheetView?.TranslateTo(0, 0, 240, Easing.CubicOut) ?? Task.CompletedTask;
            await Task.WhenAll(dimAnim, sheetFade, sheetSlide);
        }
        catch { }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseWithResultAsync(null);
        return true;
    }

    private Label BuildSectionHeader(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 1.2,
            TextTransform = TextTransform.Uppercase,
            Margin = new Thickness(0, 4, 0, 2)
        };
        label.SetDynamicResource(Label.TextColorProperty, "OverhaulSectionCaptionColor");
        return label;
    }

    private void BuildSortOptions()
    {
        _sortOptionsHost.Children.Clear();
        _sortOptionBorders.Clear();
        _sortOptionTextLabels.Clear();
        _sortOptionBadgeLabels.Clear();

        foreach (var sort in Enum.GetValues<SortBy>())
        {
            var row = BuildSortOptionRow(sort);
            _sortOptionsHost.Children.Add(row);
        }

        RefreshSortOptionSelection();
    }

    private Border BuildSortOptionRow(SortBy sort)
    {
        var radioInner = new Border
        {
            WidthRequest = 8,
            HeightRequest = 8,
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            BackgroundColor = Colors.White,
            Opacity = 0
        };

        var radio = new Border
        {
            WidthRequest = 20,
            HeightRequest = 20,
            StrokeThickness = 2,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = 0,
            Content = radioInner,
            VerticalOptions = LayoutOptions.Center
        };

        var text = new Label
        {
            Text = GetSortOptionText(sort),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.StartAndExpand
        };

        var badge = new Label
        {
            Text = GetSortOptionBadge(sort),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(8, 3),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        badge.LineBreakMode = LineBreakMode.NoWrap;

        var badgeContainer = new Border
        {
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = badge,
            VerticalOptions = LayoutOptions.Center
        };
        badgeContainer.SetDynamicResource(Border.BackgroundColorProperty, "WizardToggleContainerColor");

        var sortRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };
        sortRow.Children.Add(radio);
        Grid.SetColumn(radio, 0);
        Grid.SetRow(radio, 0);

        sortRow.Children.Add(text);
        Grid.SetColumn(text, 1);
        Grid.SetRow(text, 0);

        sortRow.Children.Add(badgeContainer);
        Grid.SetColumn(badgeContainer, 2);
        Grid.SetRow(badgeContainer, 0);

        var border = new Border
        {
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(14, 12),
            Content = sortRow
        };
        border.SetDynamicResource(Border.BackgroundColorProperty, "OverhaulCardBackgroundColor");
        border.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            _selectedSortBy = sort;
            RefreshSortOptionSelection();
        };
        border.GestureRecognizers.Add(tap);

        border.BindingContext = (radio, radioInner, badgeContainer);
        _sortOptionBorders[sort] = border;
        _sortOptionTextLabels[sort] = text;
        _sortOptionBadgeLabels[sort] = badge;

        return border;
    }

    private void RefreshSortOptionSelection()
    {
        foreach (var kv in _sortOptionBorders)
        {
            var isSelected = kv.Key == _selectedSortBy;
            var border = kv.Value;
            if (border.BindingContext is ValueTuple<Border, Border, Border> tuple)
            {
                var (radio, radioInner, badgeContainer) = tuple;

                if (isSelected)
                {
                    border.Stroke = GetPrimary();
                    border.BackgroundColor = Color.FromRgba(GetPrimary().Red, GetPrimary().Green, GetPrimary().Blue, 0.11f);
                    radio.Stroke = GetPrimary();
                    radio.BackgroundColor = GetPrimary();
                    radioInner.Opacity = 1;
                    badgeContainer.BackgroundColor = Color.FromRgba(GetPrimary().Red, GetPrimary().Green, GetPrimary().Blue, 0.20f);
                }
                else
                {
                    border.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");
                    border.SetDynamicResource(Border.BackgroundColorProperty, "OverhaulCardBackgroundColor");
                    radio.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");
                    radio.BackgroundColor = Colors.Transparent;
                    radioInner.Opacity = 0;
                    badgeContainer.SetDynamicResource(Border.BackgroundColorProperty, "WizardToggleContainerColor");
                }
            }

            if (_sortOptionTextLabels.TryGetValue(kv.Key, out var textLabel))
            {
                if (isSelected)
                    textLabel.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
                else
                    textLabel.SetDynamicResource(Label.TextColorProperty, "SecondaryText");
            }

            if (_sortOptionBadgeLabels.TryGetValue(kv.Key, out var badgeLabel))
            {
                if (isSelected)
                    badgeLabel.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
                else
                    badgeLabel.SetDynamicResource(Label.TextColorProperty, "SecondaryText");
            }
        }
    }

    private string GetSortOptionText(SortBy sort) => sort switch
    {
        SortBy.NameAsc => FoodbookApp.Localization.FilterSortPopupResources.SortAZ,
        SortBy.NameDesc => FoodbookApp.Localization.FilterSortPopupResources.SortZA,
        SortBy.CaloriesAsc => FoodbookApp.Localization.FilterSortPopupResources.SortCaloriesAsc,
        SortBy.CaloriesDesc => FoodbookApp.Localization.FilterSortPopupResources.SortCaloriesDesc,
        SortBy.ProteinAsc => FoodbookApp.Localization.FilterSortPopupResources.SortProteinAsc,
        SortBy.ProteinDesc => FoodbookApp.Localization.FilterSortPopupResources.SortProteinDesc,
        SortBy.CarbsAsc => FoodbookApp.Localization.FilterSortPopupResources.SortCarbsAsc,
        SortBy.CarbsDesc => FoodbookApp.Localization.FilterSortPopupResources.SortCarbsDesc,
        SortBy.FatAsc => FoodbookApp.Localization.FilterSortPopupResources.SortFatAsc,
        SortBy.FatDesc => FoodbookApp.Localization.FilterSortPopupResources.SortFatDesc,
        _ => FoodbookApp.Localization.FilterSortPopupResources.SortAZ
    };

    private static string GetSortOptionBadge(SortBy sort) => sort switch
    {
        SortBy.NameAsc => "A-Z",
        SortBy.NameDesc => "Z-A",
        SortBy.CaloriesAsc => "+ kcal",
        SortBy.CaloriesDesc => "- kcal",
        SortBy.ProteinAsc => "+ P",
        SortBy.ProteinDesc => "- P",
        SortBy.CarbsAsc => "+ C",
        SortBy.CarbsDesc => "- C",
        SortBy.FatAsc => "+ F",
        SortBy.FatDesc => "- F",
        _ => "A-Z"
    };

    private void BuildLabels()
    {
        _labelsHost.Children.Clear();
        _labelsHost.RowDefinitions.Clear();

        int count = _labels.Count;
        int rows = (count + 1) / 2;
        for (int r = 0; r < rows; r++)
            _labelsHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < count; i++)
        {
            var chip = BuildLabelChip(_labels[i]);
            _labelsHost.Children.Add(chip);
            Grid.SetColumn(chip, i % 2);
            Grid.SetRow(chip, i / 2);
        }
    }

    private View BuildLabelChip(RecipeLabel label)
    {
        var dot = new Border
        {
            WidthRequest = 9,
            HeightRequest = 9,
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            BackgroundColor = Color.FromArgb(label.ColorHex ?? "#757575"),
            StrokeShape = new RoundRectangle { CornerRadius = 4.5 },
            VerticalOptions = LayoutOptions.Center
        };

        var text = new Label
        {
            Text = label.Name,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };

        var row = new HorizontalStackLayout
        {
            Spacing = 6,
            Children = { dot, text }
        };

        var chip = new Border
        {
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(12, 7),
            Margin = new Thickness(0, 0, 0, 0),
            Content = row
        };

        void ApplyStyle(bool selected)
        {
            if (selected)
            {
                chip.Stroke = GetPrimary();
                chip.BackgroundColor = Color.FromRgba(GetPrimary().Red, GetPrimary().Green, GetPrimary().Blue, 0.12f);
                text.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
            }
            else
            {
                chip.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");
                chip.SetDynamicResource(Border.BackgroundColorProperty, "OverhaulCardBackgroundColor");
                text.SetDynamicResource(Label.TextColorProperty, "SecondaryText");
            }
        }

        ApplyStyle(_selected.Contains(label.Id));

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, __) =>
        {
            if (_selected.Contains(label.Id)) _selected.Remove(label.Id);
            else _selected.Add(label.Id);
            ApplyStyle(_selected.Contains(label.Id));
        };
        chip.GestureRecognizers.Add(tap);

        return chip;
    }

    private void BuildIngredients()
    {
        IEnumerable<Ingredient> source = _allIngredients;
        var query = _ingredientSearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
            source = source.Where(i => i.Name?.Contains(query, System.StringComparison.OrdinalIgnoreCase) == true);

        var filtered = source
            .OrderBy(i => i.Name, System.StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        _visibleIngredients.Clear();
        foreach (var ing in filtered)
            _visibleIngredients.Add(ing);
    }

    private async Task ResetSelectionAsync()
    {
        _selectedSortBy = SortBy.NameAsc;
        _selected.Clear();
        _selectedIngredientNames.Clear();
        _ingredientSearchEntry.Text = string.Empty;

        RefreshSortOptionSelection();
        BuildLabels();
        BuildIngredients();

        if (!_showApplyButton)
        {
            await SubmitAndCloseAsync();
        }
    }

    private async Task SubmitAndCloseAsync()
    {
        await CloseWithResultAsync(GetResult());
    }

    private async Task CloseWithResultAsync(FilterSortResult? result)
    {
        if (_isClosing) return;
        _isClosing = true;

        try
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.SetResult(result);

            if (Navigation?.ModalStack?.Contains(this) == true)
                await Navigation.PopModalAsync(true);
        }
        finally
        {
            _isClosing = false;
        }
    }

    private FilterSortResult GetResult()
    {
        var chosen = _selectedSortBy;
        return new FilterSortResult
        {
            SortBy = chosen,
            SortOrder = chosen == SortBy.NameDesc ? SortOrder.Desc : SortOrder.Asc,
            SelectedLabelIds = _selected.ToList(),
            SelectedIngredientNames = _selectedIngredientNames.ToList()
        };
    }

    private Color GetPrimary()
    {
        if (Application.Current?.Resources.TryGetValue("Primary", out var v) == true && v is Color c)
            return c;
        return Color.FromArgb("#512BD4");
    }

    private static Color ResolvePopupBackgroundColor()
    {
        if (Application.Current?.Resources.TryGetValue("PopupBackgroundColor", out var popupBg) == true && popupBg is Color popupColor)
            return popupColor;
        return Colors.White;
    }

    private sealed class IngredientFilterItemView : ContentView
    {
        private readonly FilterSortPopup _owner;
        private readonly Border _row;
        private readonly Border _checkbox;
        private readonly Label _checkboxText;
        private readonly Label _name;
        private Ingredient? _ingredient;

        public IngredientFilterItemView(FilterSortPopup owner)
        {
            _owner = owner;

            _checkboxText = new Label
            {
                Text = "?",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Opacity = 0,
                TextColor = Colors.White
            };

            _checkbox = new Border
            {
                WidthRequest = 20,
                HeightRequest = 20,
                StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                BackgroundColor = Colors.Transparent,
                Content = _checkboxText,
                VerticalOptions = LayoutOptions.Center
            };
            _checkbox.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");

            _name = new Label
            {
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center
            };

            var content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };
            content.Children.Add(_checkbox);
            Grid.SetColumn(_checkbox, 0);
            content.Children.Add(_name);
            Grid.SetColumn(_name, 1);

            _row = new Border
            {
                StrokeThickness = 0,
                Stroke = Colors.Transparent,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(12, 10),
                Content = content
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, __) => ToggleSelection();
            _row.GestureRecognizers.Add(tap);

            Content = _row;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            _ingredient = BindingContext as Ingredient;
            _name.Text = _ingredient?.Name ?? string.Empty;
            ApplyStyle();
        }

        private void ToggleSelection()
        {
            if (_ingredient == null || string.IsNullOrWhiteSpace(_ingredient.Name))
                return;

            if (_owner._selectedIngredientNames.Contains(_ingredient.Name))
                _owner._selectedIngredientNames.Remove(_ingredient.Name);
            else
                _owner._selectedIngredientNames.Add(_ingredient.Name);

            ApplyStyle();
        }

        private void ApplyStyle()
        {
            var selected = _ingredient != null && !string.IsNullOrWhiteSpace(_ingredient.Name) && _owner._selectedIngredientNames.Contains(_ingredient.Name);
            if (selected)
            {
                var primary = _owner.GetPrimary();
                _row.BackgroundColor = Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.10f);
                _checkbox.BackgroundColor = primary;
                _checkbox.Stroke = primary;
                _checkboxText.Opacity = 1;
                _name.SetDynamicResource(Label.TextColorProperty, "PrimaryText");
            }
            else
            {
                _row.BackgroundColor = Colors.Transparent;
                _checkbox.BackgroundColor = Colors.Transparent;
                _checkbox.SetDynamicResource(Border.StrokeProperty, "OverhaulCardStrokeColor");
                _checkboxText.Opacity = 0;
                _name.SetDynamicResource(Label.TextColorProperty, "SecondaryText");
            }
        }
    }
}
