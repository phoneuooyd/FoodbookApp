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

    // Prevent duplicate UI items by versioning population runs
    private int _populateVersion = 0;

    public SearchablePickerPopup(List<string> allItems, string? selected)
    {
        _allItems = allItems ?? new List<string>();
        CloseCommand = new Command(async () => await CloseWithResultAsync(null));
        ClearSearchCommand = new Command(() => SearchText = string.Empty);
        AddIngredientCommand = new Command(async () => await OnAddIngredientAsync());
        InitializeComponent();
        DetectContext();
        
        // ✅ Szybko: Schedule na backgroundzie
        _ = PopulateAsync(_allItems);
    }

    /// <summary>
    /// ✅ ULTRA ZOPTYMALIZOWANA: Twórz buttony w tle i dodawaj bez bloku UI
    /// Dodatkowo: wersjonowanie, aby uniknąć duplikatów przy wielu wywołaniach.
    /// </summary>
    private async Task PopulateAsync(IEnumerable<string> items)
    {
        try
        {
            // Capture a new version and invalidate any previous scheduled additions
            var myVersion = ++_populateVersion;

            _filteredItems = items.ToList();
            var totalCount = _filteredItems.Count;

            // Wyczyść UI (await jest OK tutaj bo to jednorazowa szybka operacja)
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // If a newer population has started since scheduling, skip clearing/adds
                if (myVersion != _populateVersion) return;

                var host = ItemsHost;
                if (host == null) return;
                host.Children.Clear();

                if (ShowAddIngredientButton)
                    host.Children.Add(CreateAddIngredientRow());
            });

            // ✅ KLUCZOWA OPTYMALIZACJA: Ładuj w batchach BEZ await na dodawanie
            const int batchSize = 100; // Zwiększam do 100 bo teraz nie blokujemy
            
            // Zacznij ładować w tle
            _ = Task.Run(async () =>
            {
                for (int i = 0; i < totalCount; i += batchSize)
                {
                    // If a newer population started, stop scheduling further batches
                    if (myVersion != _populateVersion)
                        break;

                    var batch = _filteredItems.Skip(i).Take(batchSize).ToList();
                    
                    // Twórz buttony w tle
                    var buttons = batch.Select(item => CreateItemButton(item)).ToList();

                    // ✅ CRITICAL: BeginInvokeOnMainThread (NIE await!) - schedule i kontynuuj
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Drop if stale
                        if (myVersion != _populateVersion) return;

                        var host = ItemsHost;
                        if (host == null) return;

                        foreach (var button in buttons)
                            host.Children.Add(button);
                    });

                    // Daj UI chwilę na odświeżenie między batchami
                    if (i + batchSize < totalCount)
                        await Task.Delay(5);
                }

                System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] All {totalCount} items scheduled for loading (version={myVersion})");
            });

            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Started loading {totalCount} items in background (version={myVersion})");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] PopulateAsync error: {ex.Message}");
        }
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
    /// ✅ NOWA METODA: Tworzenie przycisku "Dodaj składnik"
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
            Text = "Dodaj składnik",
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
    /// ✅ ZOPTYMALIZOWANA: Lekka metoda tworzenia przycisku elementu
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

        // ✅ OPTYMALIZACJA: Binding kolorów bez convertera
        button.SetAppThemeColor(Button.TextColorProperty,
            (Color)Application.Current!.Resources["PrimaryText"],
            (Color)Application.Current!.Resources["PrimaryText"]);

        button.SetAppThemeColor(Button.BorderColorProperty,
            (Color)Application.Current!.Resources["Secondary"],
            (Color)Application.Current!.Resources["Secondary"]);

        // ✅ KRYTYCZNA OPTYMALIZACJA: Async event handler bez blokowania UI
        button.Clicked += async (_, __) => await CloseWithResultAsync(item);

        return button;
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
                await Shell.Current.DisplayAlert("Błąd", "Nie można otworzyć formularza składnika.", "OK");
                return;
            }

            // ✅ CRITICAL: Reset form to clear previous values
            vm.Reset();
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormViewModel reset before opening");

            var formPage = new IngredientFormPage(vm);

            // ✅ NEW: Notify SearchablePickerComponent listeners that a modal is opening
            // This allows AddRecipePage to adjust popup background overlay for wallpaper mode
            try
            {
                SearchablePickerComponent.RaiseGlobalPopupStateChanged(this, true);
                System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Notified GlobalPopupStateChanged: opening modal");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Failed to notify GlobalPopupStateChanged: {ex.Message}");
            }

            // ✅ Show the page modally and await its dismissal
            var dismissedTcs = new TaskCompletionSource();
            formPage.Disappearing += (_, __) => 
            {
                try
                {
                    dismissedTcs.TrySetResult();
                    
                    // ✅ NEW: Notify that modal is closing
                    SearchablePickerComponent.RaiseGlobalPopupStateChanged(this, false);
                    System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Notified GlobalPopupStateChanged: closing modal");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Error in modal closing notification: {ex.Message}");
                }
            };
            
            await currentPage.Navigation.PushModalAsync(formPage);
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormPage opened modally");
            
            // ✅ Wait for form to be dismissed (user saved or cancelled)
            await dismissedTcs.Task;
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormPage dismissed");

            // ✅ CRITICAL: Small delay to ensure all SaveAsync operations completed
            await Task.Delay(200);

            // ✅ Now reload fresh data and update popup list
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

                    // ✅ Update AddRecipeViewModel list if we're in AddRecipePage context
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

                    // ✅ Update local popup list (this popup's _allItems)
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Updating popup list...");
                        _allItems.Clear();
                        foreach (var ing in fresh)
                            _allItems.Add(ing.Name);
                        ApplySearch();
                        System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Popup list updated with {_allItems.Count} items");
                    });

                    // ✅ Force reload IngredientsPage if it's alive
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
            
            // ✅ Ensure we reset popup state even on error
            try
            {
                SearchablePickerComponent.RaiseGlobalPopupStateChanged(this, false);
            }
            catch { }
        }
    }

    private async Task CloseWithResultAsync(object? result)
    {
        if (!_tcs.Task.IsCompleted)
            _tcs.SetResult(result);
        await CloseAsync();
    }

    /// <summary>
    /// ✅ ZOPTYMALIZOWANA: Szybkie filtrowanie i resetowanie widoku
    /// </summary>
    private void ApplySearch()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(query))
        {
            _ = PopulateAsync(_allItems);
            return;
        }

        var filtered = _allItems
            .Where(x => x?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        _ = PopulateAsync(filtered);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
