using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using Foodbook.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace Foodbook.ViewModels;

public class IngredientFormViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private Ingredient? _ingredient;

    // Event raised after a successful save; subscribers can await it
    public event Func<Task>? SavedAsync;

    // Tab management
    private int _selectedTabIndex = 0;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            try
            {
                _selectedTabIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBasicInfoTabSelected));
                OnPropertyChanged(nameof(IsNutritionTabSelected));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting SelectedTabIndex: {ex.Message}");
            }
        }
    }
    
    public bool IsBasicInfoTabSelected => SelectedTabIndex == 0;
    public bool IsNutritionTabSelected => SelectedTabIndex == 1;
    
    public ICommand SelectTabCommand { get; }

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); ValidateInput(); } }
    private string _name = string.Empty;

    public string Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); ValidateInput(); } }
    private string _quantity = "100";  // Default value

    public string UnitWeight
    {
        get => _unitWeight;
        set
        {
            _unitWeight = value;
            OnPropertyChanged();
            ValidateInput();
        }
    }
    private string _unitWeight = "1.0";

    public bool IsUnitWeightVisible => SelectedUnit == Unit.Piece;

    // ? CRITICAL: Immediately save to database on unit change in EDIT mode
    public Unit SelectedUnit 
    { 
        get => _unit; 
        set 
        { 
            if (_unit != value) 
            { 
                var oldUnit = _unit;
                _unit = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsUnitWeightVisible)); 
                ValidateInput();
                
                // ? CRITICAL: In edit mode, immediately persist to database to prevent reverting
                if (_ingredient != null && _ingredient.Id > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] SelectedUnit changed in EDIT mode: {oldUnit} -> {value} - saving to DB immediately");
                    _ = SaveUnitChangeToDatabase(value); // Fire and forget
                }
            }
        } 
    }
    private Unit _unit = Unit.Gram;  // Default value

    /// <summary>
    /// ? NEW: Immediately save unit change to database in edit mode to prevent VM refresh from overwriting
    /// </summary>
    private async Task SaveUnitChangeToDatabase(Unit newUnit)
    {
        if (_ingredient == null || _ingredient.Id <= 0)
        {
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] SaveUnitChangeToDatabase: No ingredient loaded, skipping");
            return;
        }

        try
        {
            // Update the ingredient entity directly
            _ingredient.Unit = newUnit;
            
            // Immediately persist to database
            await _service.UpdateIngredientAsync(_ingredient);
            
            System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] ? Unit change saved to database: {_ingredient.Name} -> {newUnit}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] ? Failed to save unit change: {ex.Message}");
            // Optionally show user feedback
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlert("B³¹d", $"Nie uda³o siê zapisaæ zmiany jednostki: {ex.Message}", "OK");
            });
        }
    }

    // Nutritional information fields
    public string Calories { get => _calories; set { _calories = value; OnPropertyChanged(); ValidateInput(); } }
    private string _calories = "0";

    public string Protein { get => _protein; set { _protein = value; OnPropertyChanged(); ValidateInput(); } }
    private string _protein = "0";

    public string Fat { get => _fat; set { _fat = value; OnPropertyChanged(); ValidateInput(); } }
    private string _fat = "0";

    public string Carbs { get => _carbs; set { _carbs = value; OnPropertyChanged(); ValidateInput(); } }
    private string _carbs = "0";

    public string Title => _ingredient == null 
        ? FoodbookApp.Localization.ButtonResources.NewIngredient 
        : FoodbookApp.Localization.ButtonResources.EditIngredient;
    
    public string SaveButtonText => _ingredient == null 
        ? FoodbookApp.Localization.ButtonResources.AddIngredient 
        : FoodbookApp.Localization.ButtonResources.SaveChanges;

    public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; OnPropertyChanged(); } }
    private string _validationMessage = string.Empty;

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

    public bool IsPartOfRecipe => _ingredient?.RecipeId.HasValue == true;
    
    public string RecipeInfo => IsPartOfRecipe ? "Ten sk³adnik jest czêœci¹ przepisu" : string.Empty;

    // Status weryfikacji OpenFoodFacts
    public string VerificationStatus { get => _verificationStatus; set { _verificationStatus = value; OnPropertyChanged(); } }
    private string _verificationStatus = string.Empty;

    public bool IsVerifying { get => _isVerifying; set { _isVerifying = value; OnPropertyChanged(); } }
    private bool _isVerifying = false;

    // Use a concrete list for ItemsSource so picker can match items reliably
    public IList<Unit> Units { get; } = Enum.GetValues(typeof(Unit)).Cast<Unit>().ToList();

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand VerifyNutritionCommand { get; }

    public IngredientFormViewModel(IIngredientService service)
    {
        _service = service;
        SaveCommand = new Command(async () => await SaveAsync(), CanSave);
        CancelCommand = new Command(async () => await CancelAsync());
        VerifyNutritionCommand = new Command(async () => await VerifyNutritionAsync(), () => !string.IsNullOrWhiteSpace(Name) && !IsVerifying);
        SelectTabCommand = new Command<object>(SelectTab);
        ValidateInput();
    }

    private void SelectTab(object parameter)
    {
        try
        {
            int tabIndex = 0;
            
            if (parameter is int intParam)
            {
                tabIndex = intParam;
            }
            else if (parameter is string stringParam && int.TryParse(stringParam, out int parsedIndex))
            {
                tabIndex = parsedIndex;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Invalid tab parameter: {parameter}");
                return;
            }

            // Validate tab index is within bounds
            if (tabIndex >= 0 && tabIndex <= 1)
            {
                SelectedTabIndex = tabIndex;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Tab index out of bounds: {tabIndex}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in SelectTab: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the form to default values for adding a new ingredient
    /// </summary>
    public void Reset()
    {
        try
        {
            _ingredient = null;
            Name = string.Empty;
            Quantity = "100";
            SelectedUnit = Unit.Gram;
            UnitWeight = "1.0";
            Calories = "0";
            Protein = "0";
            Fat = "0";
            Carbs = "0";
            ValidationMessage = string.Empty;
            VerificationStatus = string.Empty;
            IsVerifying = false;
            SelectedTabIndex = 0;

            // Notify UI about property changes
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(IsPartOfRecipe));
            OnPropertyChanged(nameof(RecipeInfo));
            OnPropertyChanged(nameof(IsUnitWeightVisible));
            
            ValidateInput();
            
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form reset to defaults");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in Reset: {ex.Message}");
        }
    }

    public async Task LoadAsync(int id)
    {
        try
        {
            var ing = await _service.GetIngredientAsync(id);
            if (ing != null)
            {
                _ingredient = ing;
                Name = ing.Name;
                Quantity = ing.Quantity.ToString("F2");
                SelectedUnit = ing.Unit;
                Calories = ing.Calories.ToString("F1");
                Protein = ing.Protein.ToString("F1");
                Fat = ing.Fat.ToString("F1");
                Carbs = ing.Carbs.ToString("F1");
                UnitWeight = ing.UnitWeight.ToString("F2");
                // Reset to first tab when loading
                SelectedTabIndex = 0;
                // Notify UI about property changes
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(IsPartOfRecipe));
                OnPropertyChanged(nameof(RecipeInfo));
                OnPropertyChanged(nameof(IsUnitWeightVisible));
                ValidateInput();
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = $"B³¹d podczas ³adowania sk³adnika: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error in LoadAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Weryfikuje wartoœci od¿ywcze sk³adnika z OpenFoodFacts
    /// </summary>
    private async Task VerifyNutritionAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                VerificationStatus = "WprowadŸ nazwê sk³adnika przed weryfikacj¹";
                return;
            }

            IsVerifying = true;
            VerificationStatus = "Weryfikujê dane w OpenFoodFacts...";
            ((Command)VerifyNutritionCommand).ChangeCanExecute();

            // Zapisz oryginalne wartoœci
            var originalCalories = double.TryParse(Calories, out var cal) ? cal : 0;
            var originalProtein = double.TryParse(Protein, out var prot) ? prot : 0;
            var originalFat = double.TryParse(Fat, out var fat) ? fat : 0;
            var originalCarbs = double.TryParse(Carbs, out var carbs) ? carbs : 0;

            // Stwórz tymczasowy sk³adnik do weryfikacji
            var tempIngredient = new Ingredient
            {
                Name = Name.Trim(),
                Calories = originalCalories,
                Protein = originalProtein,
                Fat = originalFat,
                Carbs = originalCarbs
            };

            // U¿yj publicznej metody z SeedData
            bool wasUpdated = await Data.SeedData.UpdateIngredientWithOpenFoodFactsAsync(tempIngredient);

            if (wasUpdated)
            {
                // Aktualizuj pola w ViewModelu
                Calories = tempIngredient.Calories.ToString("F1");
                Protein = tempIngredient.Protein.ToString("F1");
                Fat = tempIngredient.Fat.ToString("F1");
                Carbs = tempIngredient.Carbs.ToString("F1");

                VerificationStatus = $"? Zaktualizowano dane dla '{Name}'";
                
                await Shell.Current.DisplayAlert(
                    "Weryfikacja sk³adnika", 
                    $"Zaktualizowano dane dla '{Name}':\n" +
                    $"Kalorie: {originalCalories:F1} ? {tempIngredient.Calories:F1}\n" +
                    $"Bia³ko: {originalProtein:F1} ? {tempIngredient.Protein:F1}g\n" +
                    $"T³uszcze: {originalFat:F1} ? {tempIngredient.Fat:F1}g\n" +
                    $"Wêglowodany: {originalCarbs:F1} ? {tempIngredient.Carbs:F1}g", 
                    "OK");
            }
            else
            {
                VerificationStatus = $"?? Nie znaleziono produktu '{Name}' w OpenFoodFacts";
                
                await Shell.Current.DisplayAlert(
                    "Weryfikacja sk³adnika", 
                    $"Nie znaleziono produktu '{Name}' w bazie OpenFoodFacts lub dane s¹ identyczne z obecnymi.\n\n" +
                    "Spróbuj u¿yæ innej nazwy sk³adnika (np. po angielsku) lub wprowadŸ dane rêcznie.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            VerificationStatus = $"? B³¹d weryfikacji: {ex.Message}";
            await Shell.Current.DisplayAlert(
                "B³¹d weryfikacji", 
                $"Wyst¹pi³ b³¹d podczas weryfikacji sk³adnika '{Name}': {ex.Message}", 
                "OK");
            System.Diagnostics.Debug.WriteLine($"Error in VerifyNutritionAsync: {ex.Message}");
        }
        finally
        {
            IsVerifying = false;
            ((Command)VerifyNutritionCommand).ChangeCanExecute();
        }
    }

    private bool CanSave()
    {
        try
        {
            return !string.IsNullOrWhiteSpace(Name) && 
                   IsValidDouble(Quantity) && 
                   ParseDoubleValue(Quantity) > 0 &&
                   IsValidDouble(Calories) &&
                   IsValidDouble(Protein) &&
                   IsValidDouble(Fat) &&
                   IsValidDouble(Carbs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CanSave: {ex.Message}");
            return false;
        }
    }

    private void ValidateInput()
    {
        try
        {
            ValidationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationMessage = "Nazwa sk³adnika jest wymagana";
            }
            else if (!IsValidDouble(Quantity))
            {
                ValidationMessage = "Iloœæ musi byæ liczb¹";
            }
            else if (ParseDoubleValue(Quantity) <= 0)
            {
                ValidationMessage = "Iloœæ musi byæ wiêksza od zera";
            }
            else if (!IsValidDouble(Calories))
            {
                ValidationMessage = "Kalorie musz¹ byæ liczb¹";
            }
            else if (!IsValidDouble(Protein))
            {
                ValidationMessage = "Bia³ko musi byæ liczb¹";
            }
            else if (!IsValidDouble(Fat))
            {
                ValidationMessage = "T³uszcze musz¹ byæ liczb¹";
            }
            else if (!IsValidDouble(Carbs))
            {
                ValidationMessage = "Wêglowodany musz¹ byæ liczb¹";
            }

            OnPropertyChanged(nameof(HasValidationError));
            ((Command)SaveCommand).ChangeCanExecute();
            ((Command)VerifyNutritionCommand).ChangeCanExecute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ValidateInput: {ex.Message}");
            ValidationMessage = "B³¹d walidacji danych";
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!CanSave())
            {
                return;
            }

            var qty = ParseDoubleValue(Quantity);
            var cal = ParseDoubleValue(Calories);
            var prot = ParseDoubleValue(Protein);
            var fat = ParseDoubleValue(Fat);
            var carbs = ParseDoubleValue(Carbs);
            var unitWeight = ParseDoubleValue(UnitWeight);
            if (unitWeight <= 0) unitWeight = 1.0;
            
            bool isNewIngredient = _ingredient == null;
            int savedId = 0;
            
            if (_ingredient == null)
            {
                // Creating new ingredient
                var newIng = new Ingredient 
                { 
                    Name = Name.Trim(), 
                    Quantity = qty, 
                    Unit = SelectedUnit,
                    Calories = cal,
                    Protein = prot,
                    Fat = fat,
                    Carbs = carbs,
                    UnitWeight = unitWeight,
                    RecipeId = null  // Explicitly set to null for standalone ingredients
                };
                
                await _service.AddIngredientAsync(newIng);
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Added new ingredient: {Name}, {qty}, {SelectedUnit}, {cal} cal");
                
                // Try to get the saved ingredient's ID
                try
                {
                    var list = await _service.GetIngredientsAsync();
                    var match = list.FirstOrDefault(i => i.Name == Name.Trim());
                    if (match != null) savedId = match.Id;
                }
                catch { }
            }
            else
            {
                // Updating existing ingredient
                _ingredient.Name = Name.Trim();
                _ingredient.Quantity = qty;
                _ingredient.Unit = SelectedUnit;
                _ingredient.Calories = cal;
                _ingredient.Protein = prot;
                _ingredient.Fat = fat;
                _ingredient.Carbs = carbs;
                _ingredient.UnitWeight = unitWeight;
                // Preserve existing RecipeId and Recipe navigation property
                await _service.UpdateIngredientAsync(_ingredient);
                savedId = _ingredient.Id;
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Updated ingredient: {Name}, {qty}, {SelectedUnit}, {cal} cal");
            }
            
            // ? CRITICAL: Invalidate cache FIRST before any reload attempts
            try { _service.InvalidateCache(); } catch { }
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Service cache invalidated");

            // ? CRITICAL: Raise events and wait for all handlers to complete BEFORE closing
            try 
            { 
                AppEvents.RaiseIngredientSaved(savedId);
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Event IngredientSaved raised for ID: {savedId}");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error raising IngredientSaved: {ex.Message}");
            }

            try 
            { 
                await AppEvents.RaiseIngredientsChangedAsync();
                System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Event IngredientsChangedAsync raised and awaited");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error raising IngredientsChangedAsync: {ex.Message}");
            }

            // ? CRITICAL: Force hard reload of IngredientsViewModel if accessible
            try
            {
                var ingVm = Application.Current?.Handler?.MauiContext?.Services?.GetService<IngredientsViewModel>();
                if (ingVm == null)
                {
                    ingVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IngredientsViewModel>();
                }
                if (ingVm != null)
                {
                    System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Forcing hard reload of IngredientsViewModel...");
                    await ingVm.HardReloadAsync();
                    System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] IngredientsViewModel reloaded successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error reloading IngredientsViewModel: {ex.Message}");
            }

            // ? CRITICAL: Force reload of IngredientsPage if it's alive
            try 
            { 
                if (Foodbook.Views.IngredientsPage.Current != null)
                {
                    System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Forcing reload of IngredientsPage...");
                    await Foodbook.Views.IngredientsPage.Current.ForceReloadAsync();
                    System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] IngredientsPage reloaded successfully");
                }
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error reloading IngredientsPage: {ex.Message}");
            }

            // ? Invalidate AddRecipeViewModel cache if available
            try
            {
                var addRecipeVm = Application.Current?.Handler?.MauiContext?.Services?.GetService<AddRecipeViewModel>();
                if (addRecipeVm == null)
                {
                    addRecipeVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<AddRecipeViewModel>();
                }
                if (addRecipeVm != null)
                {
                    addRecipeVm.InvalidateIngredientsCache();
                    System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] AddRecipeViewModel cache invalidated");
                }
            }
            catch { }

            // ? Raise SavedAsync for subscribers (popup) so they know save completed
            if (SavedAsync != null)
            {
                try
                {
                    var handlers = SavedAsync.GetInvocationList().Cast<Func<Task>>();
                    var tasks = handlers.Select(h => h());
                    await Task.WhenAll(tasks);
                    System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] SavedAsync event handlers completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error raising SavedAsync: {ex.Message}");
                }
            }

            // ? Reset form after saving a new ingredient (before closing)
            if (isNewIngredient)
            {
                Reset();
                System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form reset after adding new ingredient");
            }

            // ? Small delay to ensure UI has time to update before modal closes
            await Task.Delay(100);
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Closing form...");

            // ? Close appropriately depending on how the page was opened
            var nav = Shell.Current?.Navigation;
            if (nav?.ModalStack?.Count > 0)
                await nav.PopModalAsync();
            else
                await Shell.Current.GoToAsync("..");
                
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form closed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error saving ingredient: {ex.Message}");
            ValidationMessage = $"Nie uda³o siê zapisaæ sk³adnika: {ex.Message}";
        }
    }

    private async Task CancelAsync()
    {
        try
        {
            var nav = Shell.Current?.Navigation;
            if (nav?.ModalStack?.Count > 0)
                await nav.PopModalAsync();
            else
                await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CancelAsync: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null)
    {
        try
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnPropertyChanged: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates if a string represents a valid double number
    /// </summary>
    private static bool IsValidDouble(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            
            // Handle both decimal comma and dot
            string normalizedValue = value.Replace(',', '.');
            return double.TryParse(normalizedValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) && result >= 0;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Parses a double value from string, handling both comma and dot decimal separators
    /// </summary>
    private static double ParseDoubleValue(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
                
            // Handle both decimal comma and dot
            string normalizedValue = value.Replace(',', '.');
            return double.TryParse(normalizedValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
        }
        catch
        {
            return 0;
        }
    }
}
