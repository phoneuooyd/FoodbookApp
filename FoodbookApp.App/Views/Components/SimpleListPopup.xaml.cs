using CommunityToolkit.Maui.Views;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;
using Foodbook.Models;
using Foodbook.Utils;
using Foodbook.ViewModels;
using Foodbook.Views;

namespace Foodbook.Views.Components;

public partial class SimpleListPopup : Popup
{
    private readonly TaskCompletionSource<object?> _tcs = new();
    private IThemeService? _themeService;
    private Recipe? _currentRecipe;
    private IEnumerable<object> _visibleItems = Array.Empty<object>();
    private Color _primaryTextColor = Colors.Black;
    private Color _secondaryTextColor = Colors.Gray;

    public Task<object?> ResultTask => _tcs.Task;

    public static readonly BindableProperty TitleTextProperty =
        BindableProperty.Create(nameof(TitleText), typeof(string), typeof(SimpleListPopup), string.Empty, propertyChanged: OnPopupContentChanged);

    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IEnumerable<object>), typeof(SimpleListPopup), null, propertyChanged: OnPopupContentChanged);

    public static readonly BindableProperty IsBulletedProperty =
        BindableProperty.Create(nameof(IsBulleted), typeof(bool), typeof(SimpleListPopup), false, propertyChanged: OnPopupContentChanged);

    public static readonly BindableProperty ShowAddIngredientButtonProperty =
        BindableProperty.Create(nameof(ShowAddIngredientButton), typeof(bool), typeof(SimpleListPopup), false, propertyChanged: OnPopupContentChanged);

    public string TitleText
    {
        get => (string)GetValue(TitleTextProperty);
        set => SetValue(TitleTextProperty, value);
    }

    public IEnumerable<object>? Items
    {
        get => (IEnumerable<object>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public bool IsBulleted
    {
        get => (bool)GetValue(IsBulletedProperty);
        set => SetValue(IsBulletedProperty, value);
    }

    public bool ShowAddIngredientButton
    {
        get => (bool)GetValue(ShowAddIngredientButtonProperty);
        set => SetValue(ShowAddIngredientButtonProperty, value);
    }

    public IEnumerable<object> VisibleItems
    {
        get => _visibleItems;
        private set
        {
            _visibleItems = value;
            OnPropertyChanged(nameof(VisibleItems));
        }
    }

    public Color PrimaryTextColor
    {
        get => _primaryTextColor;
        private set
        {
            _primaryTextColor = value;
            OnPropertyChanged(nameof(PrimaryTextColor));
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand AddIngredientCommand { get; }
    public ICommand EditRecipeCommand { get; }

    public bool ShowEditButton => _currentRecipe != null;

    public class SectionHeader { public string Text { get; set; } = string.Empty; }
    public class MealTitle { public string Text { get; set; } = string.Empty; }
    public class MacroRow
    {
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Fat { get; set; }
        public double Carbs { get; set; }
    }
    public class Description { public string Text { get; set; } = string.Empty; }

    public class MealPreviewBlock
    {
        public PlannedMeal Meal { get; set; } = new PlannedMeal();
        public Recipe Recipe { get; set; } = new Recipe();
        public ICommand? IncreaseCommand { get; set; }
        public ICommand? DecreaseCommand { get; set; }
    }

    public SimpleListPopup()
    {
        CloseCommand = new Command(async () => await CloseWithResultAsync(null));
        AddIngredientCommand = new Command(async () => await OnAddIngredientAsync());
        EditRecipeCommand = new Command(async () => await OnEditRecipeAsync());

        try
        {
            _themeService = MauiProgram.ServiceProvider?.GetService<IThemeService>();
        }
        catch { }

        InitializeComponent();
        BindingContext = this;
        ItemsCollectionView.ItemTemplate = new DataTemplate(() => new PopupItemHostView(this));

        Loaded += (_, __) =>
        {
            DetectContext();
            BuildItems();
            UpdateEditButtonVisibility();
        };
    }

    private static void OnPopupContentChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not SimpleListPopup popup)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                popup.DetectContext();
                popup.BuildItems();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] OnPopupContentChanged error: {ex.Message}");
            }
        });
    }

    private void DetectContext()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            ShowAddIngredientButton = currentPage is Foodbook.Views.AddRecipePage;

            if (Items != null)
            {
                var mealPreview = Items.OfType<MealPreviewBlock>().FirstOrDefault();
                _currentRecipe = mealPreview?.Recipe;
            }
        }
        catch
        {
            ShowAddIngredientButton = false;
            _currentRecipe = null;
        }
    }

    private async Task OnEditRecipeAsync()
    {
        try
        {
            if (_currentRecipe == null)
            {
                System.Diagnostics.Debug.WriteLine("[SimpleListPopup] No recipe to edit");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] Opening edit page for recipe: {_currentRecipe.Name} (ID: {_currentRecipe.Id})");

            var editPage = MauiProgram.ServiceProvider?.GetService<AddRecipePage>();
            if (editPage == null)
            {
                System.Diagnostics.Debug.WriteLine("[SimpleListPopup] Failed to resolve AddRecipePage from DI");
                await Shell.Current.DisplayAlert("Błąd", "Nie można otworzyć formularza edycji przepisu.", "OK");
                return;
            }

            editPage.RecipeId = _currentRecipe.Id;
            await CloseWithResultAsync(null);
            await Task.Delay(100);

            var nav = Application.Current?.MainPage?.Navigation;
            if (nav != null)
            {
                await nav.PushModalAsync(editPage);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SimpleListPopup] Navigation is null");
                await Shell.Current.DisplayAlert("Błąd", "Błąd nawigacji.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] OnEditRecipeAsync error: {ex.Message}");
            await Shell.Current.DisplayAlert("Błąd", $"Nie udało się otworzyć edycji: {ex.Message}", "OK");
        }
    }

    private void UpdateEditButtonVisibility()
    {
        try
        {
            var editBtn = this.FindByName<Button>("EditRecipeButton");
            if (editBtn != null)
            {
                editBtn.IsVisible = _currentRecipe != null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] UpdateEditButtonVisibility error: {ex.Message}");
        }
    }

    private async Task OnAddIngredientAsync()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            if (currentPage == null)
                return;

            var vm = MauiProgram.ServiceProvider?.GetService<IngredientFormViewModel>();
            if (vm == null)
            {
                await Shell.Current.DisplayAlert("Błąd", "Nie można otworzyć formularza składnika.", "OK");
                return;
            }

            vm.Reset();
            System.Diagnostics.Debug.WriteLine("[SimpleListPopup] Reset IngredientFormViewModel for new ingredient");

            var formPage = new IngredientFormPage(vm);

            try
            {
                var app = Application.Current;
                var themeService = MauiProgram.ServiceProvider?.GetService<IThemeService>();
                bool wallpaperEnabled = themeService?.IsWallpaperBackgroundEnabled() == true;

                Color pageBg = Colors.White;
                if (app?.Resources != null && app.Resources.TryGetValue("PageBackgroundColor", out var pb) && pb is Color c)
                    pageBg = c;

                var alpha = wallpaperEnabled ? 1.0f : pageBg.Alpha;
                var overlay = Color.FromRgba(pageBg.Red, pageBg.Green, pageBg.Blue, alpha);

                formPage.Resources["PageBackgroundColor"] = overlay;
                formPage.Resources["PageBackgroundBrush"] = new SolidColorBrush(overlay);
                formPage.BackgroundColor = overlay;

                System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] Applied local PageBackgroundColor override for IngredientFormPage (wallpaperEnabled={wallpaperEnabled})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] Failed to apply local page background override: {ex.Message}");
            }

            var dismissedTcs = new TaskCompletionSource();
            formPage.Disappearing += (_, __) => dismissedTcs.TrySetResult();
            await currentPage.Navigation.PushModalAsync(formPage);
            await dismissedTcs.Task;

            var ingredientService = MauiProgram.ServiceProvider?.GetService<IIngredientService>();
            ingredientService?.InvalidateCache();

            if (currentPage is Foodbook.Views.AddRecipePage arp)
            {
                var recipeVm = arp.BindingContext as Foodbook.ViewModels.AddRecipeViewModel;
                if (recipeVm != null)
                {
                    await recipeVm.LoadAvailableIngredientsAsync();
                }
            }

            BuildItems();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] Error adding ingredient: {ex.Message}");
        }
    }

    private void BuildItems()
    {
        try
        {
            TitleLabel.Text = TitleText ?? string.Empty;

            var data = Items?.ToList() ?? new List<object>();

            var app = Application.Current;
            bool isDark = false;
            if (app != null)
            {
                var user = app.UserAppTheme;
                isDark = user == Microsoft.Maui.ApplicationModel.AppTheme.Dark || (user == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified && app.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark);
            }

            var primaryTextRes = app?.Resources.TryGetValue("PrimaryText", out var v1) == true && v1 is Color c1 ? c1 : Colors.Black;
            var secondaryTextRes = app?.Resources.TryGetValue("SecondaryText", out var v2) == true && v2 is Color c2 ? c2 : Colors.Gray;

            var isColorful = _themeService?.GetIsColorfulBackgroundEnabled() == true;
            var isWallpaper = _themeService?.IsWallpaperBackgroundEnabled() == true;

            var contentPrimary = !isColorful
                ? (isDark ? Colors.White : primaryTextRes)
                : (isDark ? Colors.Black : primaryTextRes);

            var contentSecondary = !isColorful
                ? (isDark ? Color.FromArgb("#858585") : secondaryTextRes)
                : (isDark ? Color.FromArgb("#000000") : secondaryTextRes);

            if (isWallpaper && isDark)
            {
                contentPrimary = Color.FromArgb("#858585");
                contentSecondary = Color.FromArgb("#000000");
            }

            PrimaryTextColor = contentPrimary;
            _secondaryTextColor = contentSecondary;
            TitleLabel.TextColor = contentPrimary;

            bool hasMealPreview = data.OfType<MealPreviewBlock>().Any();
            var visibleItems = new List<object>(data.Count);

            foreach (var obj in data)
            {
                if (IsDuplicateHeaderItem(obj))
                    continue;

                if (hasMealPreview && obj is MacroRow)
                    continue;

                if (obj is IEnumerable<string> groupStr && obj is not string)
                {
                    foreach (var item in groupStr)
                        visibleItems.Add(item);
                    continue;
                }

                if (obj is IEnumerable<object> groupObj && obj is not string)
                {
                    foreach (var item in groupObj)
                        visibleItems.Add(GetDisplayText(item));
                    continue;
                }

                visibleItems.Add(obj);
            }

            VisibleItems = visibleItems;
            UpdateEditButtonVisibility();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error building SimpleListPopup items: {ex.Message}");
        }
    }

    private bool IsDuplicateHeaderItem(object obj)
    {
        var title = TitleText?.Trim();
        if (string.IsNullOrWhiteSpace(title)) return false;

        switch (obj)
        {
            case MealTitle mt when !string.IsNullOrWhiteSpace(mt.Text):
                return string.Equals(mt.Text.Trim(), title, StringComparison.CurrentCultureIgnoreCase);
            case string s when !string.IsNullOrWhiteSpace(s):
                return string.Equals(s.Trim(), title, StringComparison.CurrentCultureIgnoreCase);
            case SectionHeader sh when !string.IsNullOrWhiteSpace(sh.Text):
                return string.Equals(sh.Text.Trim(), title, StringComparison.CurrentCultureIgnoreCase);
            default:
                return false;
        }
    }

    private View CreateViewForItem(object obj, Color primaryText, Color secondaryText)
    {
        switch (obj)
        {
            case SectionHeader header:
                return new Label
                {
                    Text = header.Text,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = primaryText,
                    Margin = new Thickness(0, 16, 0, 8)
                };

            case MealTitle title:
                return new Label
                {
                    Text = title.Text,
                    FontSize = 17,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = primaryText,
                    Margin = new Thickness(0, 4, 0, 4),
                    LineBreakMode = LineBreakMode.WordWrap
                };

            case MacroRow macros:
            {
                var layout = new HorizontalStackLayout
                {
                    Spacing = 20,
                    Margin = new Thickness(0, 2, 0, 6)
                };

                layout.Children.Add(new Label { Text = $"K: {macros.Calories:F0} kcal", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText });
                layout.Children.Add(new Label { Text = $"B: {macros.Protein:F1}g", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText });
                layout.Children.Add(new Label { Text = $"T: {macros.Fat:F1}g", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText });
                layout.Children.Add(new Label { Text = $"W: {macros.Carbs:F1}g", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText });
                return layout;
            }

            case Description desc:
                return new Label
                {
                    Text = desc.Text,
                    FontSize = 14,
                    TextColor = secondaryText,
                    LineBreakMode = LineBreakMode.WordWrap,
                    Margin = new Thickness(0, 4, 0, 16)
                };

            case MealPreviewBlock mpb:
                return CreateMealPreviewView(mpb, primaryText, secondaryText);

            case string s:
                return CreateRow(s, primaryText);

            default:
                return CreateRow(GetDisplayText(obj), primaryText);
        }
    }

    private static string GetDisplayText(object? obj)
    {
        if (obj is null) return string.Empty;
        if (obj is Enum e)
        {
            var shortName = e.GetDisplayShortName();
            return string.IsNullOrWhiteSpace(shortName) ? e.GetDisplayName() : shortName;
        }
        return obj.ToString() ?? string.Empty;
    }

    private View CreateRow(string text, Color primaryText)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new BoxView
            {
                HeightRequest = 8,
                Opacity = 0
            };
        }

        if (IsBulleted)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition{ Width = GridLength.Auto },
                    new ColumnDefinition{ Width = GridLength.Star }
                },
                ColumnSpacing = 12
            };

            var bullet = new Label { Text = "•", TextColor = primaryText, FontSize = 16, VerticalOptions = LayoutOptions.Center };
            var textLabel = new Label { Text = text, TextColor = primaryText, FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
            Grid.SetColumn(textLabel, 1);
            grid.Add(bullet);
            grid.Add(textLabel);
            return grid;
        }

        return new Label
        {
            Text = text,
            TextColor = primaryText,
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 6)
        };
    }

    private View CreateMealPreviewView(MealPreviewBlock mpb, Color primaryText, Color secondaryText)
    {
        var wrapper = new VerticalStackLayout { Spacing = 8 };

        int displayPortions = mpb.Meal.Portions > 0 ? mpb.Meal.Portions : Math.Max(mpb.Recipe.IloscPorcji, 1);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition{ Width = GridLength.Star },
                new ColumnDefinition{ Width = GridLength.Auto },
                new ColumnDefinition{ Width = GridLength.Auto }
            },
            ColumnSpacing = 6
        };

        var titleLabel = new Label
        {
            Text = $"{mpb.Recipe.Name} ({displayPortions} porcji)",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = primaryText,
            LineBreakMode = LineBreakMode.WordWrap
        };
        row.Add(titleLabel);

        var minus = new Button
        {
            Text = "➖",
            WidthRequest = 26,
            HeightRequest = 26,
            CornerRadius = 13,
            FontSize = 14,
            Padding = 0,
            BackgroundColor = Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark ? Color.FromArgb("#555555") : Color.FromArgb("#E0E0E0"),
            TextColor = Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark ? Colors.White : Colors.Black,
            IsEnabled = displayPortions > 1
        };
        Grid.SetColumn(minus, 1);
        row.Add(minus);

        var plus = new Button
        {
            Text = "➕",
            WidthRequest = 26,
            HeightRequest = 26,
            CornerRadius = 13,
            FontSize = 14,
            Padding = 0,
            BackgroundColor = (Color)Application.Current?.Resources["Primary"],
            TextColor = (Color)Application.Current?.Resources["ButtonPrimaryText"],
            IsEnabled = displayPortions < 20
        };
        Grid.SetColumn(plus, 2);
        row.Add(plus);

        wrapper.Children.Add(row);

        var macroLayout = new HorizontalStackLayout
        {
            Spacing = 20,
            Margin = new Thickness(0, 2, 0, 6)
        };
        var kcalLabel = new Label { Text = "K: - kcal", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText };
        var pLabel = new Label { Text = "B: - g", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText };
        var fLabel = new Label { Text = "T: - g", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText };
        var cLabel = new Label { Text = "W: - g", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = secondaryText };
        macroLayout.Children.Add(kcalLabel);
        macroLayout.Children.Add(pLabel);
        macroLayout.Children.Add(fLabel);
        macroLayout.Children.Add(cLabel);

        wrapper.Children.Add(macroLayout);

        var list = new VerticalStackLayout { Spacing = 4 };

        void rebuildIngredients(Recipe recipe)
        {
            list.Children.Clear();
            var multiplier = (double)displayPortions;
            if (recipe.Ingredients != null)
            {
                foreach (var ing in recipe.Ingredients)
                {
                    var adjusted = ing.Quantity * multiplier;
                    list.Children.Add(new Label
                    {
                        Text = $"• {ing.Name}: {adjusted:F1} {GetUnitText(ing.Unit)}",
                        FontSize = 14,
                        TextColor = secondaryText
                    });
                }
            }
        }

        if (mpb.Recipe != null)
        {
            kcalLabel.Text = $"K: {mpb.Recipe.Calories:F0} kcal";
            pLabel.Text = $"B: {mpb.Recipe.Protein:F1}g";
            fLabel.Text = $"T: {mpb.Recipe.Fat:F1}g";
            cLabel.Text = $"W: {mpb.Recipe.Carbs:F1}g";
        }

        _ = RefreshRecipeDisplayAsync(mpb, titleLabel, kcalLabel, pLabel, fLabel, cLabel, list, secondaryText);

        minus.Clicked += (s, e) =>
        {
            if (displayPortions <= 1) return;
            displayPortions--;
            titleLabel.Text = $"{mpb.Recipe.Name} ({displayPortions} porcji)";
            rebuildIngredients(mpb.Recipe);
            minus.IsEnabled = displayPortions > 1;
            plus.IsEnabled = displayPortions < 20;
        };

        plus.Clicked += (s, e) =>
        {
            if (displayPortions >= 20) return;
            displayPortions++;
            titleLabel.Text = $"{mpb.Recipe.Name} ({displayPortions} porcji)";
            rebuildIngredients(mpb.Recipe);
            minus.IsEnabled = displayPortions > 1;
            plus.IsEnabled = displayPortions < 20;
        };

        wrapper.Children.Add(list);
        return wrapper;
    }

    private async Task RefreshRecipeDisplayAsync(MealPreviewBlock mpb, Label titleLabel, Label kcalLabel, Label pLabel, Label fLabel, Label cLabel, VerticalStackLayout list, Color ingredientTextColor)
    {
        try
        {
            var recipeService = MauiProgram.ServiceProvider?.GetService<IRecipeService>();
            if (recipeService == null) return;

            var id = mpb.Recipe?.Id ?? Guid.Empty;
            if (id == Guid.Empty) return;

            var fresh = await recipeService.GetRecipeAsync(id);
            if (fresh == null) return;

            mpb.Recipe = fresh;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    var text = titleLabel.Text ?? string.Empty;
                    var idx = text.IndexOf('(');
                    var portionsPart = idx >= 0 ? text.Substring(idx) : string.Empty;
                    titleLabel.Text = $"{fresh.Name} {portionsPart}".Trim();

                    kcalLabel.Text = $"K: {fresh.Calories:F0} kcal";
                    pLabel.Text = $"B: {fresh.Protein:F1}g";
                    fLabel.Text = $"T: {fresh.Fat:F1}g";
                    cLabel.Text = $"W: {fresh.Carbs:F1}g";

                    list.Children.Clear();
                    int currentPortions = mpb.Meal.Portions > 0 ? mpb.Meal.Portions : Math.Max(fresh.IloscPorcji, 1);
                    var multiplier = (double)currentPortions;
                    if (fresh.Ingredients != null)
                    {
                        foreach (var ing in fresh.Ingredients)
                        {
                            var adjusted = ing.Quantity * multiplier;
                            list.Children.Add(new Label
                            {
                                Text = $"• {ing.Name}: {adjusted:F1} {GetUnitText(ing.Unit)}",
                                FontSize = 14,
                                TextColor = ingredientTextColor
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] RefreshRecipeDisplayAsync UI update failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimpleListPopup] Failed to refresh recipe from DB: {ex.Message}");
        }
    }

    private static string GetUnitText(Unit unit)
    {
        return unit.GetDisplayShortName();
    }

    private async Task CloseWithResultAsync(object? result)
    {
        try
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.SetResult(result);
            await CloseAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error closing SimpleListPopup: {ex.Message}");
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? SimpleListPopup: Attempting to handle popup blocking");
                try
                {
                    if (!_tcs.Task.IsCompleted)
                        _tcs.SetResult(null);
                    if (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"?? SimpleListPopup: Could not handle popup blocking: {modalEx.Message}");
                }
            }
        }
    }

    private sealed class PopupItemHostView : ContentView
    {
        private readonly SimpleListPopup _owner;

        public PopupItemHostView(SimpleListPopup owner)
        {
            _owner = owner;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (BindingContext is object item)
            {
                Content = _owner.CreateViewForItem(item, _owner.PrimaryTextColor, _owner._secondaryTextColor);
            }
            else
            {
                Content = new BoxView { HeightRequest = 0, Opacity = 0 };
            }
        }
    }
}
