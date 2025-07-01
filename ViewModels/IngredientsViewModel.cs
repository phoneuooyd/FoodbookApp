using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;
using Foodbook.Views;
using Foodbook.Data;

namespace Foodbook.ViewModels;

public class IngredientsViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private bool _isLoading;
    private bool _isRefreshing;
    private string _searchText = string.Empty;
    private List<Ingredient> _allIngredients = new();

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

    public ICommand BulkVerifyCommand { get; } // Nowa komenda


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
    }

    /// <summary>

    /// Masowa weryfikacja wszystkich sk�adnik�w z OpenFoodFacts

    /// </summary>
    private async Task BulkVerifyIngredientsAsync()
    {
        if (IsBulkVerifying || Ingredients.Count == 0) return;

        bool confirm = await Shell.Current.DisplayAlert(

            "Masowa weryfikacja składników",
            $"Czy chcesz zweryfikować wszystkie {Ingredients.Count} sk�adnik�w z OpenFoodFacts?\n\nMo�e to potrwa� kilka minut.",

            "Tak, weryfikuj",
            "Anuluj");

        if (!confirm) return;

        try
        {
            IsBulkVerifying = true;
            var updatedCount = 0;
            var totalCount = Ingredients.Count;
            var failedCount = 0;


            BulkVerificationStatus = $"Weryfikuję składniki: 0/{totalCount}";


            for (int i = 0; i < Ingredients.Count; i++)
            {
                var ingredient = Ingredients[i];
                
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

                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    System.Diagnostics.Debug.WriteLine($"? B��d weryfikacji {ingredient.Name}: {ex.Message}");
                }
            }

            // Poka� wyniki
            var successMessage = $"Weryfikacja zako�czona!\n\n" +
                               $"? Zaktualizowano: {updatedCount} sk�adnik�w\n" +
                               $"?? Bez zmian: {totalCount - updatedCount - failedCount} sk�adnik�w\n" +
                               (failedCount > 0 ? $"? B��dy/nie znaleziono: {failedCount} sk�adnik�w" : "");

            BulkVerificationStatus = $"? Zako�czono - zaktualizowano {updatedCount}/{totalCount} sk�adnik�w";

            await Shell.Current.DisplayAlert(
                "Masowa weryfikacja zako�czona",
                successMessage,
                "OK");

            // Od�wie� list�

            await ReloadAsync();
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
            var list = await _service.GetIngredientsAsync();
            _allIngredients = list;
            
            // Clear and add in batches to improve UI responsiveness
            Ingredients.Clear();
            
            // Add items in smaller batches to prevent UI blocking
            const int batchSize = 50;
            for (int i = 0; i < list.Count; i += batchSize)
            {
                var batch = list.Skip(i).Take(batchSize);
                foreach (var ingredient in batch)
                {
                    Ingredients.Add(ingredient);
                }
                
                // Allow UI to update between batches
                if (i + batchSize < list.Count)
                {
                    await Task.Delay(1);
                }
            }
            
            FilterIngredients();
            ((Command)BulkVerifyCommand).ChangeCanExecute(); // Refresh command state

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading ingredients: {ex.Message}");
            // Could show user-friendly error message here
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
            await LoadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void FilterIngredients()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // If no search text, show all ingredients
            if (Ingredients.Count != _allIngredients.Count)
            {
                Ingredients.Clear();
                foreach (var ingredient in _allIngredients)
                {
                    Ingredients.Add(ingredient);
                }
            }
        }
        else
        {
            // Filter ingredients based on search text
            var filtered = _allIngredients
                .Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            Ingredients.Clear();
            foreach (var ingredient in filtered)
            {
                Ingredients.Add(ingredient);
            }
        }

        
        ((Command)BulkVerifyCommand).ChangeCanExecute(); // Refresh command state when filter changes

    }

    private async Task DeleteIngredientAsync(Ingredient? ing)
    {
        if (ing == null) return;
        
        try
        {
            await _service.DeleteIngredientAsync(ing.Id);
            Ingredients.Remove(ing);
            _allIngredients.Remove(ing);
            ((Command)BulkVerifyCommand).ChangeCanExecute(); // Refresh command state

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting ingredient: {ex.Message}");
            // Could show user-friendly error message here
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
