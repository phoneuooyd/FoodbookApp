using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Microsoft.Maui.Controls;
using Foodbook.Views;
using Foodbook.Data;
using FoodbookApp.Interfaces;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class IngredientsViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private bool _isLoading;
    private bool _isRefreshing;
    private string _searchText = string.Empty;
    private List<Ingredient> _allIngredients = new();

    // New: sort order
    private SortOrder _sortOrder = SortOrder.Asc;
    public SortOrder SortOrder
    {
        get => _sortOrder;
        set { if (_sortOrder == value) return; _sortOrder = value; OnPropertyChanged(); FilterIngredients(); }
    }

    public event EventHandler? DataLoaded; // Raised when all data finished loading

    // W³aœciwoœci dla masowej weryfikacji
    private bool _isBulkVerifying;
    public bool IsBulkVerifying
    {
        get => _isBulkVerifying;
        set
        {
            if (_isBulkVerifying == value) return;
            _isBulkVerifying = value;
            OnPropertyChanged();
            ((Command)BulkVerifyCommand).ChangeCanExecute();
        }
    }

    private string _bulkVerificationStatus = string.Empty;
    public string BulkVerificationStatus
    {
        get => _bulkVerificationStatus;
        set
        {
            if (_bulkVerificationStatus == value) return;
            _bulkVerificationStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasBulkVerificationStatus));
        }
    }

    public bool HasBulkVerificationStatus => !string.IsNullOrWhiteSpace(BulkVerificationStatus);

    public ObservableCollection<Ingredient> Ingredients { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            FilterIngredients();
        }
    }

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand BulkVerifyCommand { get; } // Nowa komenda
    public ICommand ClearSearchCommand { get; } // Nowa komenda do czyszczenia wyszukiwania

    public IngredientsViewModel(IIngredientService service)
    {
        _service = service;
        AddCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(IngredientFormPage)));
        EditCommand = new Command<Ingredient>(async ing =>
        {
            if (ing != null)
                await Shell.Current.GoToAsync($"{nameof(IngredientFormPage)}?id={ing.Id}");
        });
        DeleteCommand = new Command<Ingredient>(async ing => await DeleteIngredientAsync(ing));
        RefreshCommand = new Command(async () => await ReloadAsync());
        BulkVerifyCommand = new Command(async () => await BulkVerifyIngredientsAsync(), () => !IsBulkVerifying && Ingredients.Count > 0);
        ClearSearchCommand = new Command(() => SearchText = string.Empty); // Komenda do czyszczenia wyszukiwania

        // Subscribe to global ingredient save/change signals so VM stays fresh even off-page
        try
        {
            AppEvents.IngredientSaved += OnIngredientSaved;
            AppEvents.IngredientsChangedAsync += OnIngredientsChangedSignal;
        }
        catch { }
    }

    // Safe dispatcher helpers (avoid COM exceptions in headless unit tests)
    private void RunOnUiThread(Action action)
    {
        try
        {
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(action);
        }
        catch
        {
            // No dispatcher available (e.g., unit tests) – run inline
            try { action(); } catch { }
        }
    }

    private async Task RunOnUiThreadAsync(Action action)
    {
        try
        {
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(action);
        }
        catch
        {
            // No dispatcher available – run inline
            try { action(); } catch { }
            await Task.CompletedTask;
        }
    }

    private async void OnIngredientSaved(int id)
    {
        // Ensure next fetch hits DB, then refresh
        try { _service.InvalidateCache(); } catch { }
        await HardReloadAsync();
    }

    private async Task OnIngredientsChangedSignal()
    {
        try { _service.InvalidateCache(); } catch { }
        await HardReloadAsync();
    }

    private void RaiseDataLoaded()
    {
        try 
        { 
            var handler = DataLoaded; 
            if (handler != null)
            {
                RunOnUiThread(() =>
                {
                    try { handler.Invoke(this, EventArgs.Empty); } catch { }
                });
            }
        } 
        catch { }
    }

    /// <summary>
    /// Core data fetch with UI-thread marshaling for collection updates.
    /// </summary>
    private async Task FetchIngredientsAsync()
    {
        var list = await _service.GetIngredientsAsync();
        _allIngredients = list;

        // Update observable collection safely
        await RunOnUiThreadAsync(() =>
        {
            Ingredients.Clear();
            foreach (var ingredient in list)
            {
                Ingredients.Add(ingredient);
            }
        });

        // Apply current filter and sort
        await RunOnUiThreadAsync(() =>
        {
            FilterIngredients();
            ((Command)BulkVerifyCommand).ChangeCanExecute(); // Refresh command state
        });

        // Signal that all data required by the view is ready
        RaiseDataLoaded();
    }

    /// <summary>
    /// Hard reload: invalidate service cache and reload from DB.
    /// </summary>
    public async Task HardReloadAsync()
    {
        if (IsRefreshing) return;
        try { _service.InvalidateCache(); } catch { }
        await ReloadAsync();
    }

    /// <summary>
    /// Masowa weryfikacja wszystkich sk³adników z OpenFoodFacts
    /// </summary>
    private async Task BulkVerifyIngredientsAsync()
    {
        if (IsBulkVerifying || Ingredients.Count == 0) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Masowa weryfikacja sk³adników",
            $"Czy chcesz zweryfikowaæ wszystkie {Ingredients.Count} sk³adników z OpenFoodFacts?\n\nMo¿e to potrwaæ kilka minut.",
            "Tak, weryfikuj",
            "Anuluj");

        if (!confirm) return;

        try
        {
            IsBulkVerifying = true;
            var updatedCount = 0;
            var totalCount = Ingredients.Count;
            var failedCount = 0;

            BulkVerificationStatus = $"Weryfikujê sk³adniki: 0/{totalCount}";

            const int batchSize = 5; // Mniejsze batche dla API
            for (int i = 0; i < Ingredients.Count; i += batchSize)
            {
                var batch = Ingredients.Skip(i).Take(batchSize).ToList();
                
                // Process batch
                foreach (var ingredient in batch)
                {
                    try
                    {
                        BulkVerificationStatus = $"Weryfikujê sk³adniki: {i + 1}/{totalCount} - {ingredient.Name}";

                        // Skopiuj sk³adnik do weryfikacji
                        var tempIngredient = new Ingredient
                        {
                            Id = ingredient.Id,
                            Name = ingredient.Name,
                            Quantity = ingredient.Quantity,
                            Unit = ingredient.Unit,
                            Calories = ingredient.Calories,
                            Protein = ingredient.Protein,
                            Fat = ingredient.Fat,
                            Carbs = ingredient.Carbs,
                            RecipeId = ingredient.RecipeId
                        };

                        // Weryfikuj z OpenFoodFacts
                        bool wasUpdated = await SeedData.UpdateIngredientWithOpenFoodFactsAsync(tempIngredient);

                        if (wasUpdated)
                        {
                            // Aktualizuj sk³adnik w bazie danych
                            ingredient.Calories = tempIngredient.Calories;
                            ingredient.Protein = tempIngredient.Protein;
                            ingredient.Fat = tempIngredient.Fat;
                            ingredient.Carbs = tempIngredient.Carbs;

                            await _service.UpdateIngredientAsync(ingredient);
                            updatedCount++;
                            
                            System.Diagnostics.Debug.WriteLine($"? Zaktualizowano: {ingredient.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        System.Diagnostics.Debug.WriteLine($"? B³¹d weryfikacji {ingredient.Name}: {ex.Message}");
                    }
                }

                // Throttling miêdzy batchami
                if (i + batchSize < Ingredients.Count)
                {
                    await Task.Delay(1000); // 1 sekunda pauzy miêdzy batchami
                }
            }

            // Poka¿ wyniki
            var successMessage =
                "Weryfikacja zakoñczona!\n\n" +
                $"? Zaktualizowano: {updatedCount} sk³adników\n" +
                $"?? Bez zmian: {totalCount - updatedCount - failedCount} sk³adników\n" +
                (failedCount > 0 ? $"? B³êdy/nie znaleziono: {failedCount} sk³adników" : "");

            BulkVerificationStatus = $"? Zakoñczono - zaktualizowano {updatedCount}/{totalCount} sk³adników";

            await Shell.Current.DisplayAlert(
                "Masowa weryfikacja zakoñczona",
                successMessage,
                "OK");

            // Odœwie¿ listê
            await HardReloadAsync();
        }
        catch (Exception ex)
        {
            BulkVerificationStatus = $"? B³¹d masowej weryfikacji: {ex.Message}";
            
            await Shell.Current.DisplayAlert(
                "B³¹d weryfikacji",
                $"Wyst¹pi³ b³¹d podczas masowej weryfikacji sk³adników:\n{ex.Message}",
                "OK");
        }
        finally
        {
            IsBulkVerifying = false;
        }
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            await FetchIngredientsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading ingredients: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        try
        {
            var fetchTask = FetchIngredientsAsync();
            var timeoutTask = Task.Delay(15000); // 15s hard timeout
            var completed = await Task.WhenAny(fetchTask, timeoutTask);
            if (completed == fetchTask)
            {
                await fetchTask; // propagate exceptions if any
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Refresh timeout reached - stopping spinner");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] Reload error: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void FilterIngredients()
    {
        IEnumerable<Ingredient> source;
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            source = _allIngredients;
        }
        else
        {
            source = _allIngredients
                .Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        source = SortOrder == SortOrder.Asc
            ? source.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            : source.OrderByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase);

        // Update bound collection safely (works without UI thread in tests)
        RunOnUiThread(() =>
        {
            Ingredients.Clear();
            foreach (var ingredient in source)
            {
                Ingredients.Add(ingredient);
            }
            ((Command)BulkVerifyCommand).ChangeCanExecute();
        });
    }

    private async Task DeleteIngredientAsync(Ingredient? ing)
    {
        if (ing == null) return;
        
        // Add confirmation dialog
        bool confirm = await Shell.Current.DisplayAlert(
            "Usuwanie sk³adnika", 
            $"Czy na pewno chcesz usun¹æ sk³adnik '{ing.Name}'?", 
            "Tak", 
            "Nie");
            
        if (!confirm) return;
        
        try
        {
            await _service.DeleteIngredientAsync(ing.Id);
            await HardReloadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting ingredient: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
