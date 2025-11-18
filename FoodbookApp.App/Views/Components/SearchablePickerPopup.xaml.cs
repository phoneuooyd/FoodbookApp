using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Maui.Views;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;
using Foodbook.ViewModels;
using Foodbook.Views;
using Foodbook.Services;

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

    // New: context-aware add ingredient capability
    public bool ShowAddIngredientButton { get; private set; }
    public ICommand AddIngredientCommand { get; }

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
        AddIngredientCommand = new Command(async () => await OnAddIngredientAsync());
        InitializeComponent();
        DetectContext();
        Populate(_allItems);
    }

    private void DetectContext()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            ShowAddIngredientButton = currentPage is Foodbook.Views.AddRecipePage;
            OnPropertyChanged(nameof(ShowAddIngredientButton));
        }
        catch
        {
            ShowAddIngredientButton = false;
        }
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

        // First row: add ingredient action in AddRecipe context
        if (ShowAddIngredientButton)
        {
            var addRow = new HorizontalStackLayout
            {
                Spacing = 12,
                Padding = new Thickness(4, 0, 4, 8),
                VerticalOptions = LayoutOptions.Center
            };

            var plusButton = new Button
            {
                Text = "+",
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            plusButton.StyleClass = new[] { "Secondary" };
            plusButton.Clicked += async (_, __) => await OnAddIngredientAsync();

            var textLabel = new Label
            {
                Text = "Dodaj sk³adnik",
                TextColor = primaryText,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, __) => await OnAddIngredientAsync();
            textLabel.GestureRecognizers.Add(tap);

            addRow.Children.Add(plusButton);
            addRow.Children.Add(textLabel);
            host.Children.Add(addRow);
        }

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

    private async Task OnAddIngredientAsync()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            if (currentPage == null)
                return;

            var vm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IngredientFormViewModel>();
            if (vm == null)
            {
                await Shell.Current.DisplayAlert("B³¹d", "Nie mo¿na otworzyæ formularza sk³adnika.", "OK");
                return;
            }

            // ? CRITICAL: Reset form to clear previous values
            vm.Reset();
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormViewModel reset before opening");

            var formPage = new IngredientFormPage(vm);

            // ? Show the page modally and await its dismissal
            var dismissedTcs = new TaskCompletionSource();
            formPage.Disappearing += (_, __) => dismissedTcs.TrySetResult();
            
            await currentPage.Navigation.PushModalAsync(formPage);
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormPage opened modally");
            
            // ? Wait for form to be dismissed (user saved or cancelled)
            await dismissedTcs.Task;
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormPage dismissed");

            // ? CRITICAL: Small delay to ensure all SaveAsync operations completed
            // (events, reloads, etc. that happen BEFORE modal closes)
            await Task.Delay(200);

            // ? Now reload fresh data and update popup list
            try
            {
                var ingredientService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IIngredientService>();
                if (ingredientService != null)
                {
                    System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Fetching fresh ingredients from service...");
                    
                    // Force cache invalidation
                    try { ingredientService.InvalidateCache(); } catch { }
                    
                    var fresh = await ingredientService.GetIngredientsAsync();
                    System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Fetched {fresh.Count} ingredients");

                    // ? Update AddRecipeViewModel list if we're in AddRecipePage context
                    if (currentPage is Foodbook.Views.AddRecipePage arp)
                    {
                        var recipeVm = arp.BindingContext as Foodbook.ViewModels.AddRecipeViewModel;
                        if (recipeVm != null)
                        {
                            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Updating AddRecipeViewModel.AvailableIngredientNames...");
                            
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                recipeVm.AvailableIngredientNames.Clear();
                                foreach (var ing in fresh)
                                    recipeVm.AvailableIngredientNames.Add(ing.Name);
                            });
                            
                            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] AddRecipeViewModel updated with {fresh.Count} ingredients");
                        }
                    }

                    // ? Update local popup list (this popup's _allItems)
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Updating popup list...");
                        _allItems.Clear();
                        foreach (var ing in fresh)
                            _allItems.Add(ing.Name);
                        ApplySearch();
                        System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Popup list updated with {_allItems.Count} items");
                    });

                    // ? Force reload IngredientsPage if it's alive (should already be done by events, but double-check)
                    try 
                    { 
                        if (IngredientsPage.Current != null)
                        {
                            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Force reloading IngredientsPage...");
                            await IngredientsPage.Current.ForceReloadAsync();
                            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientsPage reloaded successfully");
                        }
                    } 
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] IngredientsPage reload error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Post-save refresh failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Error adding ingredient: {ex.Message}");
        }
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
