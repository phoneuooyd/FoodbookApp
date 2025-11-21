using CommunityToolkit.Maui.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Views;
using Foodbook.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;
using System.Collections.ObjectModel;

namespace Foodbook.Views.Components;

public partial class SearchablePickerPopup : Popup, INotifyPropertyChanged
{
    private readonly List<string> _allItems;
    private readonly TaskCompletionSource<object?> _tcs = new();
    
    // ? OPTYMALIZACJA: Batch loading state
    private const int INITIAL_BATCH_SIZE = 20;
    private const int INCREMENTAL_BATCH_SIZE = 20;
    private int _currentBatchEnd = 0;
    private bool _isLoadingMore = false;
    private List<string> _filteredItems = new();

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
        
        // ? KRYTYCZNA OPTYMALIZACJA: Asynchroniczne ³adowanie pierwszej partii
        _ = InitialPopulateAsync(_allItems);
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

    /// <summary>
    /// ? NOWA METODA: Asynchroniczne ³adowanie pierwszej partii elementów
    /// </summary>
    private async Task InitialPopulateAsync(IEnumerable<string> items)
    {
        try
        {
            _filteredItems = items.ToList();
            _currentBatchEnd = 0;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var host = ItemsHost;
                if (host == null) return;
                host.Children.Clear();

                // Add "Add ingredient" button first if applicable
                if (ShowAddIngredientButton)
                {
                    host.Children.Add(CreateAddIngredientRow());
                }
            });

            // Load first batch immediately
            await LoadNextBatchAsync();

            // ? OPTYMALIZACJA: Dodaj scroll listener dla infinite scroll
            if (ItemsHost?.Parent is ScrollView scrollView)
            {
                scrollView.Scrolled += OnScrolled;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] InitialPopulateAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// ? NOWA METODA: £adowanie kolejnej partii elementów w tle
    /// </summary>
    private async Task LoadNextBatchAsync()
    {
        if (_isLoadingMore || _currentBatchEnd >= _filteredItems.Count)
            return;

        _isLoadingMore = true;

        try
        {
            var batchSize = _currentBatchEnd == 0 ? INITIAL_BATCH_SIZE : INCREMENTAL_BATCH_SIZE;
            var endIndex = Math.Min(_currentBatchEnd + batchSize, _filteredItems.Count);
            var batch = _filteredItems.Skip(_currentBatchEnd).Take(endIndex - _currentBatchEnd).ToList();

            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Loading batch: {_currentBatchEnd} to {endIndex} of {_filteredItems.Count}");

            // ? KRYTYCZNA OPTYMALIZACJA: Tworzenie kontrolek w tle, dodawanie na UI thread
            var buttons = await Task.Run(() => batch.Select(item => CreateItemButton(item)).ToList());

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var host = ItemsHost;
                if (host == null) return;

                foreach (var button in buttons)
                {
                    host.Children.Add(button);
                }
            });

            _currentBatchEnd = endIndex;
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Batch loaded successfully. Total visible: {_currentBatchEnd}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] LoadNextBatchAsync error: {ex.Message}");
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    /// <summary>
    /// ? NOWA METODA: Infinite scroll handler
    /// </summary>
    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        try
        {
            if (sender is not ScrollView scrollView)
                return;

            // Load more when user scrolls to bottom 20% of content
            var threshold = scrollView.ContentSize.Height * 0.8;
            if (e.ScrollY >= threshold && !_isLoadingMore && _currentBatchEnd < _filteredItems.Count)
            {
                _ = LoadNextBatchAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] OnScrolled error: {ex.Message}");
        }
    }

    /// <summary>
    /// ? ZOPTYMALIZOWANA: Szybkie filtrowanie i resetowanie widoku
    /// </summary>
    private void ApplySearch()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(query))
        {
            _ = InitialPopulateAsync(_allItems);
            return;
        }

        var filtered = _allItems
            .Where(x => x?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        _ = InitialPopulateAsync(filtered);
    }

    /// <summary>
    /// ? NOWA METODA: Tworzenie przycisku "Dodaj sk³adnik"
    /// </summary>
    private View CreateAddIngredientRow()
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
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center
        };
        textLabel.SetAppThemeColor(Label.TextColorProperty, 
            (Color)Application.Current!.Resources["PrimaryText"],
            (Color)Application.Current!.Resources["PrimaryText"]);

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, __) => await OnAddIngredientAsync();
        textLabel.GestureRecognizers.Add(tap);

        addRow.Children.Add(plusButton);
        addRow.Children.Add(textLabel);
        
        return addRow;
    }

    /// <summary>
    /// ? ZOPTYMALIZOWANA: Lekka metoda tworzenia przycisku elementu
    /// </summary>
    private Button CreateItemButton(string item)
    {
        var button = new Button
        {
            Text = item,
            HorizontalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Transparent,
            BorderWidth = 1,
            CornerRadius = 8,
            Padding = new Thickness(12, 10),
            Margin = new Thickness(2, 3),
            FontSize = 15
        };

        // ? OPTYMALIZACJA: Binding kolorów bez convertera
        button.SetAppThemeColor(Button.TextColorProperty,
            (Color)Application.Current!.Resources["PrimaryText"],
            (Color)Application.Current!.Resources["PrimaryText"]);

        button.SetAppThemeColor(Button.BorderColorProperty,
            (Color)Application.Current!.Resources["Secondary"],
            (Color)Application.Current!.Resources["Secondary"]);

        // ? KRYTYCZNA OPTYMALIZACJA: Async event handler bez blokowania UI
        button.Clicked += async (_, __) => await CloseWithResultAsync(item);

        return button;
    }

    /// <summary>
    /// ? STARA METODA: Zachowana dla backward compatibility (ju¿ nieu¿ywana)
    /// </summary>
    [Obsolete("Use InitialPopulateAsync instead for better performance")]
    private void Populate(IEnumerable<string> items)
    {
        // Legacy method - replaced by async batched loading
        _ = InitialPopulateAsync(items);
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

                    // ? Force reload IngredientsPage if it's alive
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

    private async Task CloseWithResultAsync(object? result)
    {
        // ? OPTYMALIZACJA: Cleanup scroll listener
        if (ItemsHost?.Parent is ScrollView scrollView)
        {
            scrollView.Scrolled -= OnScrolled;
        }

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
