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
using Foodbook.Views.Components;

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
        set
        {
            var mappedSortBy = value == SortOrder.Desc ? SortBy.NameDesc : SortBy.NameAsc;
            if (_sortOrder == value && _currentSortBy == mappedSortBy) return;

            _sortOrder = value;
            _currentSortBy = mappedSortBy;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentSortBy));
            FilterIngredients();
        }
    }

    private SortBy? _currentSortBy = SortBy.NameAsc;
    public SortBy? CurrentSortBy
    {
        get => _currentSortBy;
        set
        {
            if (_currentSortBy == value) return;

            _currentSortBy = value;
            if (value.HasValue)
            {
                _sortOrder = MapSortByToSortOrder(value.Value);
                OnPropertyChanged(nameof(SortOrder));
            }

            OnPropertyChanged();
            FilterIngredients();
        }
    }

    public void ApplySorting(SortBy sortBy)
    {
        CurrentSortBy = sortBy;
    }

    public event EventHandler? DataLoaded; // Raised when all data finished loading

    // W�a�ciwo�ci dla masowej weryfikacji
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
            // No dispatcher available (e.g., unit tests) � run inline
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
            // No dispatcher available � run inline
            try { action(); } catch { }
            await Task.CompletedTask;
        }
    }

    private async void OnIngredientSaved(Guid id)
    {
        System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] OnIngredientSaved triggered for ID: {id}");
        // Ensure next fetch hits DB, then refresh
        try 
        { 
            _service.InvalidateCache();
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Service cache invalidated");
        } 
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] Error invalidating cache: {ex.Message}");
        }
        
        await HardReloadAsync();
        System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] OnIngredientSaved completed for ID: {id}");
    }

    private async Task OnIngredientsChangedSignal()
    {
        System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] OnIngredientsChangedSignal triggered");
        try 
        { 
            _service.InvalidateCache();
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Service cache invalidated");
        } 
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] Error invalidating cache: {ex.Message}");
        }
        
        await HardReloadAsync();
        System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] OnIngredientsChangedSignal completed");
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
        System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] FetchIngredientsAsync started");
        
        var list = await _service.GetIngredientsAsync();
        _allIngredients = list;
        
        System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] Fetched {list.Count} ingredients from service");

        // Update observable collection safely
        await RunOnUiThreadAsync(() =>
        {
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Updating Ingredients collection on UI thread");
            Ingredients.Clear();
            foreach (var ingredient in list)
            {
                Ingredients.Add(ingredient);
            }
            System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] Ingredients collection updated with {Ingredients.Count} items");
        });

        // Apply current filter and sort
        await RunOnUiThreadAsync(() =>
        {
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Applying filter and sort");
            FilterIngredients();
            ((Command)BulkVerifyCommand).ChangeCanExecute(); // Refresh command state
        });

        // Signal that all data required by the view is ready
        RaiseDataLoaded();
        System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] FetchIngredientsAsync completed, DataLoaded event raised");
    }

    /// <summary>
    /// Hard reload: invalidate service cache and reload from DB.
    /// </summary>
    public async Task HardReloadAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] HardReloadAsync called (IsRefreshing: {IsRefreshing})");
        
        if (IsRefreshing)
        {
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Already refreshing, skipping this reload request");
            return;
        }
        
        try 
        { 
            _service.InvalidateCache();
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Cache invalidated in HardReloadAsync");
        } 
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] Error invalidating cache: {ex.Message}");
        }
        
        await ReloadAsync();
        System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] HardReloadAsync completed");
    }

    /// <summary>
    /// Masowa weryfikacja wszystkich sk�adnik�w z OpenFoodFacts
    /// </summary>
    private async Task BulkVerifyIngredientsAsync()
    {
        if (IsBulkVerifying || Ingredients.Count == 0) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Masowa weryfikacja sk�adnik�w",
            $"Czy chcesz zweryfikowa� wszystkie {Ingredients.Count} sk�adnik�w z OpenFoodFacts?\n\nMo�e to potrwa� kilka minut.",
            "Tak, weryfikuj",
            "Anuluj");

        if (!confirm) return;

        try
        {
            IsBulkVerifying = true;
            var updatedCount = 0;
            var totalCount = Ingredients.Count;
            var failedCount = 0;

            BulkVerificationStatus = $"Weryfikuj� sk�adniki: 0/{totalCount}";

            const int batchSize = 5; // Mniejsze batche dla API
            for (int i = 0; i < Ingredients.Count; i += batchSize)
            {
                var batch = Ingredients.Skip(i).Take(batchSize).ToList();
                
                // Process batch
                foreach (var ingredient in batch)
                {
                    try
                    {
                        BulkVerificationStatus = $"Weryfikuj� sk�adniki: {i + 1}/{totalCount} - {ingredient.Name}";

                        // Skopiuj sk�adnik do weryfikacji
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
                            // Aktualizuj sk�adnik w bazie danych
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
                        System.Diagnostics.Debug.WriteLine($"? B��d weryfikacji {ingredient.Name}: {ex.Message}");
                    }
                }

                // Throttling mi�dzy batchami
                if (i + batchSize < Ingredients.Count)
                {
                    await Task.Delay(1000); // 1 sekunda pauzy mi�dzy batchami
                }
            }

            // Poka� wyniki
            var successMessage =
                "Weryfikacja zako�czona!\n\n" +
                $"? Zaktualizowano: {updatedCount} sk�adnik�w\n" +
                $"?? Bez zmian: {totalCount - updatedCount - failedCount} sk�adnik�w\n" +
                (failedCount > 0 ? $"? B��dy/nie znaleziono: {failedCount} sk�adnik�w" : "");

            BulkVerificationStatus = $"? Zako�czono - zaktualizowano {updatedCount}/{totalCount} sk�adnik�w";

            await Shell.Current.DisplayAlert(
                "Masowa weryfikacja zako�czona",
                successMessage,
                "OK");

            // Od�wie� list�
            await HardReloadAsync();
        }
        catch (Exception ex)
        {
            BulkVerificationStatus = $"? B��d masowej weryfikacji: {ex.Message}";
            
            await Shell.Current.DisplayAlert(
                "B��d weryfikacji",
                $"Wyst�pi� b��d podczas masowej weryfikacji sk�adnik�w:\n{ex.Message}",
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
        System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] ReloadAsync called (IsRefreshing: {IsRefreshing})");
        
        if (IsRefreshing)
        {
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Already refreshing, skipping");
            return;
        }
        
        IsRefreshing = true;
        System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] IsRefreshing set to TRUE");
        
        try
        {
            var fetchTask = FetchIngredientsAsync();
            var timeoutTask = Task.Delay(15000); // 15s hard timeout
            var completed = await Task.WhenAny(fetchTask, timeoutTask);
            if (completed == fetchTask)
            {
                await fetchTask; // propagate exceptions if any
                System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] Fetch completed successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] ?? Refresh timeout reached - stopping spinner");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientsViewModel] ? Reload error: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
            System.Diagnostics.Debug.WriteLine("[IngredientsViewModel] IsRefreshing set to FALSE");
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

        if (CurrentSortBy.HasValue)
        {
            source = CurrentSortBy.Value switch
            {
                SortBy.NameAsc => source.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.NameDesc => source.OrderByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.CaloriesAsc => source.OrderBy(i => i.Calories).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.CaloriesDesc => source.OrderByDescending(i => i.Calories).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.ProteinAsc => source.OrderBy(i => i.Protein).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.ProteinDesc => source.OrderByDescending(i => i.Protein).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.CarbsAsc => source.OrderBy(i => i.Carbs).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.CarbsDesc => source.OrderByDescending(i => i.Carbs).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.FatAsc => source.OrderBy(i => i.Fat).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                SortBy.FatDesc => source.OrderByDescending(i => i.Fat).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
                _ => source.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            };
        }
        else
        {
            source = SortOrder == SortOrder.Asc
                ? source.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : source.OrderByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase);
        }

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
            "Usuwanie sk�adnika", 
            $"Czy na pewno chcesz usun�� sk�adnik '{ing.Name}'?", 
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

    private static SortOrder MapSortByToSortOrder(SortBy sortBy)
        => sortBy is SortBy.NameDesc
            or SortBy.CaloriesDesc
            or SortBy.ProteinDesc
            or SortBy.CarbsDesc
            or SortBy.FatDesc
            ? SortOrder.Desc
            : SortOrder.Asc;
}
