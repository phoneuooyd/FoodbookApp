using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Linq;
using Foodbook.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Foodbook.ViewModels;

public class IngredientFormViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private Guid _itemId = Guid.Empty;
    private Guid? _loadedRecipeId = null; // preserve RecipeId when editing

    // Dirty tracking
    private bool _isDirty = false;
    private bool _suppressDirtyTracking = false;
    public bool HasUnsavedChanges => _isDirty;

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

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); ValidateInput(); if (!_suppressDirtyTracking) MarkDirty(); } }
    private string _name = string.Empty;

    public string Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); ValidateInput(); if (!_suppressDirtyTracking) MarkDirty(); } }
    private string _quantity = "100";  // Default value

    public string UnitWeight
    {
        get => _unitWeight;
        set
        {
            _unitWeight = value;
            OnPropertyChanged();
            ValidateInput();
            if (!_suppressDirtyTracking) MarkDirty();
        }
    }
    private string _unitWeight = "1.0";

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
                ValidateInput();
                if (!_suppressDirtyTracking) MarkDirty();
                
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] SelectedUnit changed: {oldUnit} -> {value} (will save on explicit Save)");
            } 
        } 
    }
    private Unit _unit = Unit.Gram;  // Default value

    // Nutritional information fields
    public string Calories { get => _calories; set { _calories = value; OnPropertyChanged(); ValidateInput(); if (!_suppressDirtyTracking) MarkDirty(); } }
    private string _calories = "0";

    public string Protein { get => _protein; set { _protein = value; OnPropertyChanged(); ValidateInput(); if (!_suppressDirtyTracking) MarkDirty(); } }
    private string _protein = "0";

    public string Fat { get => _fat; set { _fat = value; OnPropertyChanged(); ValidateInput(); if (!_suppressDirtyTracking) MarkDirty(); } }
    private string _fat = "0";

    public string Carbs { get => _carbs; set { _carbs = value; OnPropertyChanged(); ValidateInput(); if (!_suppressDirtyTracking) MarkDirty(); } }
    private string _carbs = "0";

    public string Title => _itemId == Guid.Empty 
        ? FoodbookApp.Localization.ButtonResources.NewIngredient 
        : FoodbookApp.Localization.ButtonResources.EditIngredient;
    
    public string SaveButtonText => _itemId == Guid.Empty 
        ? FoodbookApp.Localization.ButtonResources.AddIngredient 
        : FoodbookApp.Localization.ButtonResources.SaveChanges;

    public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; OnPropertyChanged(); } }
    private string _validationMessage = string.Empty;

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

    public bool IsPartOfRecipe => _loadedRecipeId.HasValue;
    
    public string RecipeInfo => IsPartOfRecipe
        ? I("RecipeInfoPartOfRecipe", "This ingredient is part of a recipe")
        : string.Empty;

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
            _suppressDirtyTracking = true;
            _itemId = Guid.Empty;
            _loadedRecipeId = null;
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
            
            ValidateInput();

            _isDirty = false;
            _suppressDirtyTracking = false;
            
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form reset to defaults");
        }
        catch (Exception ex)
        {
            _suppressDirtyTracking = false;
            System.Diagnostics.Debug.WriteLine($"Error in Reset: {ex.Message}");
        }
    }

    public async Task LoadAsync(Guid id)
    {
        try
        {
            // ? CRITICAL: Suppress dirty tracking during load
            _suppressDirtyTracking = true;
            
            var ing = await _service.GetIngredientAsync(id);
            if (ing != null)
            {
                _itemId = ing.Id;
                _loadedRecipeId = ing.RecipeId;
                Name = ing.Name;
                Quantity = ing.Quantity.ToString("F2");
                SelectedUnit = ing.Unit;
                Calories = ing.Calories.ToString("F1");
                Protein = ing.Protein.ToString("F1");
                Fat = ing.Fat.ToString("F1");
                Carbs = ing.Carbs.ToString("F1");
                // ? KRYTYCZNE: Normalizuj UnitWeight zawsze jako string z formatowaniem
                UnitWeight = ing.UnitWeight.ToString("F2");
                // Reset to first tab when loading
                SelectedTabIndex = 0;
                // Notify UI about property changes
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(IsPartOfRecipe));
                OnPropertyChanged(nameof(RecipeInfo));
                ValidateInput();
            }
            
            // ? CRITICAL: Reset dirty flag after loading and re-enable tracking
            _isDirty = false;
            _suppressDirtyTracking = false;
            
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Ingredient loaded, dirty tracking reset");
        }
        catch (Exception ex)
        {
            _suppressDirtyTracking = false;
            ValidationMessage = string.Format(
                CultureInfo.CurrentUICulture,
                I("LoadIngredientErrorMessageFormat", "Error loading ingredient: {0}"),
                ex.Message);
            System.Diagnostics.Debug.WriteLine($"Error in LoadAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Weryfikuje warto�ci od�ywcze sk�adnika z OpenFoodFacts
    /// </summary>
    private async Task VerifyNutritionAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                VerificationStatus = I("VerifyNameRequiredStatus", "Enter ingredient name before verification");
                return;
            }

            IsVerifying = true;
            VerificationStatus = I("VerifyCheckingStatus", "Verifying data in OpenFoodFacts...");
            ((Command)VerifyNutritionCommand).ChangeCanExecute();

            // Zapisz oryginalne warto�ci
            var originalCalories = double.TryParse(Calories, out var cal) ? cal : 0;
            var originalProtein = double.TryParse(Protein, out var prot) ? prot : 0;
            var originalFat = double.TryParse(Fat, out var fat) ? fat : 0;
            var originalCarbs = double.TryParse(Carbs, out var carbs) ? carbs : 0;

            // Stw�rz tymczasowy sk�adnik do weryfikacji
            var tempIngredient = new Ingredient
            {
                Name = Name.Trim(),
                Calories = originalCalories,
                Protein = originalProtein,
                Fat = originalFat,
                Carbs = originalCarbs
            };

            // U�yj publicznej metody z SeedData
            bool wasUpdated = await Data.SeedData.UpdateIngredientWithOpenFoodFactsAsync(tempIngredient);

            if (wasUpdated)
            {
                // Aktualizuj pola w ViewModelu
                Calories = tempIngredient.Calories.ToString("F1");
                Protein = tempIngredient.Protein.ToString("F1");
                Fat = tempIngredient.Fat.ToString("F1");
                Carbs = tempIngredient.Carbs.ToString("F1");

                VerificationStatus = string.Format(
                    CultureInfo.CurrentUICulture,
                    I("VerifyUpdatedStatusFormat", "Updated data for '{0}'"),
                    Name);
                
                await Shell.Current.DisplayAlert(
                    I("VerifyDialogTitle", "Ingredient verification"),
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        I("VerifyUpdatedDialogMessageFormat", "Updated data for '{0}':{1}Calories: {2:F1} -> {3:F1}{1}Protein: {4:F1} -> {5:F1}g{1}Fat: {6:F1} -> {7:F1}g{1}Carbohydrates: {8:F1} -> {9:F1}g"),
                        Name,
                        Environment.NewLine,
                        originalCalories,
                        tempIngredient.Calories,
                        originalProtein,
                        tempIngredient.Protein,
                        originalFat,
                        tempIngredient.Fat,
                        originalCarbs,
                        tempIngredient.Carbs),
                    ButtonResources.OK);
            }
            else
            {
                VerificationStatus = string.Format(
                    CultureInfo.CurrentUICulture,
                    I("VerifyNotFoundStatusFormat", "Product '{0}' not found in OpenFoodFacts"),
                    Name);
                
                await Shell.Current.DisplayAlert(
                    I("VerifyDialogTitle", "Ingredient verification"),
                    string.Format(
                        CultureInfo.CurrentUICulture,
                        I("VerifyNotFoundDialogMessageFormat", "Product '{0}' was not found in OpenFoodFacts or the values are unchanged.{1}{1}Try another ingredient name (e.g. in English) or enter values manually."),
                        Name,
                        Environment.NewLine),
                    ButtonResources.OK);
            }
        }
        catch (Exception ex)
        {
            VerificationStatus = string.Format(
                CultureInfo.CurrentUICulture,
                I("VerifyErrorStatusFormat", "Verification error: {0}"),
                ex.Message);
            await Shell.Current.DisplayAlert(
                I("VerifyErrorTitle", "Verification error"),
                string.Format(
                    CultureInfo.CurrentUICulture,
                    I("VerifyErrorDialogMessageFormat", "An error occurred while verifying ingredient '{0}': {1}"),
                    Name,
                    ex.Message),
                ButtonResources.OK);
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
                ValidationMessage = I("ValidationNameRequired", "Ingredient name is required");
            }
            else if (!IsValidDouble(Quantity))
            {
                ValidationMessage = I("ValidationQuantityNumeric", "Quantity must be a number");
            }
            else if (ParseDoubleValue(Quantity) <= 0)
            {
                ValidationMessage = I("ValidationQuantityPositive", "Quantity must be greater than zero");
            }
            else if (!IsValidDouble(Calories))
            {
                ValidationMessage = I("ValidationCaloriesNumeric", "Calories must be a number");
            }
            else if (!IsValidDouble(Protein))
            {
                ValidationMessage = I("ValidationProteinNumeric", "Protein must be a number");
            }
            else if (!IsValidDouble(Fat))
            {
                ValidationMessage = I("ValidationFatNumeric", "Fat must be a number");
            }
            else if (!IsValidDouble(Carbs))
            {
                ValidationMessage = I("ValidationCarbsNumeric", "Carbohydrates must be a number");
            }
            else if (!IsValidDouble(UnitWeight))
            {
                ValidationMessage = I("ValidationUnitWeightNumeric", "Unit weight must be a number");
            }
            else if (ParseDoubleValue(UnitWeight) <= 0)
            {
                ValidationMessage = I("ValidationUnitWeightPositive", "Unit weight must be greater than zero");
            }

            OnPropertyChanged(nameof(HasValidationError));
            ((Command)SaveCommand).ChangeCanExecute();
            ((Command)VerifyNutritionCommand).ChangeCanExecute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ValidateInput: {ex.Message}");
            ValidationMessage = I("ValidationGeneralError", "Validation error");
        }
    }

    /// <summary>
    /// Builds or updates an Ingredient instance from current VM state.
    /// Does not persist; only prepares the entity to be saved by the service.
    /// </summary>
    private Ingredient BuildIngredientFromVm()
    {
        var qty = ParseDoubleValue(Quantity);
        var cal = ParseDoubleValue(Calories);
        var prot = ParseDoubleValue(Protein);
        var fat = ParseDoubleValue(Fat);
        var carbs = ParseDoubleValue(Carbs);
        var unitWeight = ParseDoubleValue(UnitWeight);
        if (unitWeight <= 0) unitWeight = 1.0;

        return new Ingredient
        {
            Id = _itemId,
            Name = Name.Trim(),
            Quantity = qty,
            Unit = SelectedUnit,
            Calories = cal,
            Protein = prot,
            Fat = fat,
            Carbs = carbs,
            UnitWeight = unitWeight,
            RecipeId = null
        };
    }

    private async Task SaveAsync()
    {
        try
        {
            if (!CanSave())
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine("[SaveAsync] START - preparing ingredient payload from VM");

            bool isNewIngredient = _itemId == Guid.Empty;
            var toSave = BuildIngredientFromVm();
            Guid savedId = Guid.Empty;

            if (isNewIngredient)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveAsync] NEW INGREDIENT payload: {JsonConvert.SerializeObject(new { toSave.Id, toSave.Name, toSave.Quantity, toSave.Unit, toSave.UnitWeight })}");
                await _service.AddIngredientAsync(toSave);
                savedId = toSave.Id;
                System.Diagnostics.Debug.WriteLine($"[SaveAsync] ? Added new ingredient: {toSave.Name}, {toSave.Quantity}, {toSave.Unit}, {toSave.UnitWeight}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SaveAsync] UPDATE INGREDIENT payload: {JsonConvert.SerializeObject(new { toSave.Id, toSave.Name, toSave.Quantity, toSave.Unit, toSave.UnitWeight })}");
                await _service.UpdateIngredientAsync(toSave);
                savedId = toSave.Id;
                System.Diagnostics.Debug.WriteLine($"[SaveAsync] ? Updated ingredient: {toSave.Name}, {toSave.Quantity}, {toSave.Unit}, {toSave.UnitWeight}");
            }

            // Reset dirty flag immediately after successful persistence
            _isDirty = false;
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Dirty flag reset after successful save");

            // Close form first to avoid any subscribers reloading while form is still active
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Preparing to close form...");
            var nav = Shell.Current?.Navigation;
            if (nav?.ModalStack?.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Closing modal...");
                await nav.PopModalAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Navigating back...");
                await Shell.Current.GoToAsync("..");
            }
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form closed successfully");

            // AFTER navigation completes: invalidate caches and notify other VMs/pages
            try { _service.InvalidateCache(); } catch { }
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Service cache invalidated (post-close)");

            try
            {
                AppEvents.RaiseIngredientSaved(savedId);
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Event IngredientSaved raised for ID: {savedId} (post-close)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error raising IngredientSaved: {ex.Message}");
            }

            try
            {
                await AppEvents.RaiseIngredientsChangedAsync();
                System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Event IngredientsChangedAsync raised and awaited (post-close)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error raising IngredientsChangedAsync: {ex.Message}");
            }

            // Reset VM for new-ingredient case after navigation
            if (isNewIngredient)
            {
                await Task.Delay(100);
                Reset();
                System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form reset after adding new ingredient and navigation complete");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error saving ingredient: {ex.Message}\n{ex.StackTrace}");
            ValidationMessage = string.Format(
                CultureInfo.CurrentUICulture,
                I("SaveIngredientErrorMessageFormat", "Could not save ingredient: {0}"),
                ex.Message);
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

    private static string I(string key, string fallback)
    {
        var value = IngredientFormPageResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    void OnPropertyChanged([CallerMemberName] string? name = null)
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

    /// <summary>
    /// Discards any unsaved changes and resets dirty flag
    /// </summary>
    public void DiscardChanges()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Discarding changes");
            _suppressDirtyTracking = true;
            Reset();
            _isDirty = false;
            _suppressDirtyTracking = false;
            System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Changes discarded, dirty flag reset");
        }
        catch (Exception ex)
        {
            _suppressDirtyTracking = false;
            System.Diagnostics.Debug.WriteLine($"[IngredientFormViewModel] Error in DiscardChanges: {ex.Message}");
        }
    }

    /// <summary>
    /// Marks the form as having unsaved changes
    /// </summary>
    private void MarkDirty()
    {
        if (_suppressDirtyTracking) return;
        _isDirty = true;
        System.Diagnostics.Debug.WriteLine("[IngredientFormViewModel] Form marked as dirty");
    }
}
