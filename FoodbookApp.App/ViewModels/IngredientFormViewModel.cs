using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class IngredientFormViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private Ingredient? _ingredient;

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

    public Unit SelectedUnit { get => _unit; set { _unit = value; OnPropertyChanged(); } }
    private Unit _unit = Unit.Gram;  // Default value

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
    
    public string RecipeInfo => IsPartOfRecipe ? "Ten sk�adnik jest cz�ci� przepisu" : string.Empty;

    // Status weryfikacji OpenFoodFacts
    public string VerificationStatus { get => _verificationStatus; set { _verificationStatus = value; OnPropertyChanged(); } }
    private string _verificationStatus = string.Empty;

    public bool IsVerifying { get => _isVerifying; set { _isVerifying = value; OnPropertyChanged(); } }
    private bool _isVerifying = false;

    public IEnumerable<Unit> Units { get; } = Enum.GetValues(typeof(Unit)).Cast<Unit>();

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand VerifyNutritionCommand { get; } // Nowa komenda

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
                
                // Reset to first tab when loading
                SelectedTabIndex = 0;
                
                // Notify UI about property changes
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(SaveButtonText));
                OnPropertyChanged(nameof(IsPartOfRecipe));
                OnPropertyChanged(nameof(RecipeInfo));
                
                ValidateInput();
            }
        }
        catch (Exception ex)
        {
            ValidationMessage = $"B��d podczas �adowania sk�adnika: {ex.Message}";
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
                VerificationStatus = "Wprowad� nazw� sk�adnika przed weryfikacj�";
                return;
            }

            IsVerifying = true;
            VerificationStatus = "Weryfikuj� dane w OpenFoodFacts...";
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

                VerificationStatus = $"? Zaktualizowano dane dla '{Name}'";
                
                await Shell.Current.DisplayAlert(
                    "Weryfikacja sk�adnika", 
                    $"Zaktualizowano dane dla '{Name}':\n" +
                    $"Kalorie: {originalCalories:F1} ? {tempIngredient.Calories:F1}\n" +
                    $"Bia�ko: {originalProtein:F1} ? {tempIngredient.Protein:F1}g\n" +
                    $"T�uszcze: {originalFat:F1} ? {tempIngredient.Fat:F1}g\n" +
                    $"W�glowodany: {originalCarbs:F1} ? {tempIngredient.Carbs:F1}g", 
                    "OK");
            }
            else
            {
                VerificationStatus = $"? Nie znaleziono produktu '{Name}' w OpenFoodFacts";
                
                await Shell.Current.DisplayAlert(
                    "Weryfikacja sk�adnika", 
                    $"Nie znaleziono produktu '{Name}' w bazie OpenFoodFacts lub dane s� identyczne z obecnymi.\n\n" +
                    "Spr�buj u�y� innej nazwy sk�adnika (np. po angielsku) lub wprowad� dane r�cznie.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            VerificationStatus = $"? B��d weryfikacji: {ex.Message}";
            await Shell.Current.DisplayAlert(
                "B��d weryfikacji", 
                $"Wyst�pi� b��d podczas weryfikacji sk�adnika '{Name}': {ex.Message}", 
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
                ValidationMessage = "Nazwa sk�adnika jest wymagana";
            }
            else if (!IsValidDouble(Quantity))
            {
                ValidationMessage = "Ilo�� musi by� liczb�";
            }
            else if (ParseDoubleValue(Quantity) <= 0)
            {
                ValidationMessage = "Ilo�� musi by� wi�ksza od zera";
            }
            else if (!IsValidDouble(Calories))
            {
                ValidationMessage = "Kalorie musz� by� liczb�";
            }
            else if (!IsValidDouble(Protein))
            {
                ValidationMessage = "Bia�ko musi by� liczb�";
            }
            else if (!IsValidDouble(Fat))
            {
                ValidationMessage = "T�uszcze musz� by� liczb�";
            }
            else if (!IsValidDouble(Carbs))
            {
                ValidationMessage = "W�glowodany musz� by� liczb�";
            }

            OnPropertyChanged(nameof(HasValidationError));
            ((Command)SaveCommand).ChangeCanExecute();
            ((Command)VerifyNutritionCommand).ChangeCanExecute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in ValidateInput: {ex.Message}");
            ValidationMessage = "B��d walidacji danych";
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
                    RecipeId = null  // Explicitly set to null for standalone ingredients
                };
                
                await _service.AddIngredientAsync(newIng);
                System.Diagnostics.Debug.WriteLine($"Added new ingredient: {Name}, {qty}, {SelectedUnit}, {cal} cal");
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
                // Preserve existing RecipeId and Recipe navigation property
                await _service.UpdateIngredientAsync(_ingredient);
                System.Diagnostics.Debug.WriteLine($"Updated ingredient: {Name}, {qty}, {SelectedUnit}, {cal} cal");
            }
            
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving ingredient: {ex.Message}");
            ValidationMessage = $"Nie uda�o si� zapisa� sk�adnika: {ex.Message}";
        }
    }

    private async Task CancelAsync()
    {
        try
        {
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
