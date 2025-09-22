using CommunityToolkit.Maui.Views;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection; // for GetService
using FoodbookApp; // to access MauiProgram.ServiceProvider

namespace Foodbook.Views.Components;

public partial class SimpleListPopup : Popup
{
    private readonly TaskCompletionSource<object?> _tcs = new();
    private IThemeService? _themeService;
    public Task<object?> ResultTask => _tcs.Task;

    public static readonly BindableProperty TitleTextProperty =
        BindableProperty.Create(nameof(TitleText), typeof(string), typeof(SimpleListPopup), "");

    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IEnumerable<object>), typeof(SimpleListPopup), null);

    public static readonly BindableProperty IsBulletedProperty =
        BindableProperty.Create(nameof(IsBulleted), typeof(bool), typeof(SimpleListPopup), false);

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

    public ICommand CloseCommand { get; }

    // Structured item models for richer rendering
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

    public SimpleListPopup()
    {
        CloseCommand = new Command(async () => await CloseWithResultAsync(null));

        // Resolve theme service from DI if available
        try
        {
            _themeService = MauiProgram.ServiceProvider?.GetService<IThemeService>();
        }
        catch { /* ignore and keep null -> we'll fallback in BuildItems */ }

        InitializeComponent();
        Loaded += (_, __) => BuildItems();
    }

    private void BuildItems()
    {
        try
        {
            TitleLabel.Text = TitleText ?? string.Empty;
            var host = ItemsHost;
            if (host == null) return;
            host.Children.Clear();

            var data = Items?.ToList() ?? new List<object>();

            // Determine effective colors with better contrast in dark mode on tinted backgrounds
            var app = Application.Current;
            bool isDark = false;
            if (app != null)
            {
                var user = app.UserAppTheme;
                isDark = user == AppTheme.Dark || (user == AppTheme.Unspecified && app.RequestedTheme == AppTheme.Dark);
            }

            var primaryTextRes = app?.Resources.TryGetValue("PrimaryText", out var v1) == true && v1 is Color c1 ? c1 : Colors.Black;
            var secondaryTextRes = app?.Resources.TryGetValue("SecondaryText", out var v2) == true && v2 is Color c2 ? c2 : Colors.Gray;

            // proper font colour setting for darkmode and tint mode (null-safe when _themeService is not yet resolved)
            var isColorful = _themeService?.GetIsColorfulBackgroundEnabled() == true;

            var contentPrimary = !isColorful
                ? (isDark ? Colors.White : primaryTextRes)
                : (isDark ? Colors.Black : primaryTextRes);

            var contentSecondary = !isColorful
                ? (isDark ? Color.FromArgb("#858585") : secondaryTextRes)
                : (isDark ? Color.FromArgb("#000000") : secondaryTextRes);

            foreach (var obj in data)
            {
                // Skip duplicating the header (day + date) inside the item list
                if (IsDuplicateHeaderItem(obj))
                    continue;

                var view = CreateViewForItem(obj, contentPrimary, contentSecondary);
                if (view != null)
                    host.Children.Add(view);
            }

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

    private View? CreateViewForItem(object obj, Color primaryText, Color secondaryText)
    {
        switch (obj)
        {
            case SectionHeader header:
                return new Label
                {
                    Text = header.Text,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = primaryText, // use readable primary text instead of accent color
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

            case string s:
                return CreateRow(s, primaryText);

            case IEnumerable<string> group:
                var container = new VerticalStackLayout { Spacing = 6 };
                foreach (var s2 in group)
                    container.Children.Add(CreateRow(s2, primaryText));
                return container;

            default:
                return CreateRow(obj?.ToString() ?? string.Empty, primaryText);
        }
    }

    private View CreateRow(string text, Color primaryText)
    {
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
        else
        {
            return new Label
            {
                Text = text,
                TextColor = primaryText,
                FontSize = 15,
                LineBreakMode = LineBreakMode.WordWrap,
                Margin = new Thickness(0, 6)
            };
        }
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
            
            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? SimpleListPopup: Attempting to handle popup blocking");
                
                try
                {
                    // Try to clear the result and force close
                    if (!_tcs.Task.IsCompleted)
                        _tcs.SetResult(null);
                        
                    // Try to dismiss any existing modal pages
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
}
