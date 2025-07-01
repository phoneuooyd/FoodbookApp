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

    public string Title => _ingredient == null ? "Nowy sk�adnik" : "Edytuj sk�adnik";
    
    public string SaveButtonText => _ingredient == null ? "Dodaj sk�adnik" : "Zapisz zmiany";

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
        ValidateInput();
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
        }
    }

    /// <summary>
    /// Weryfikuje warto�ci od�ywcze sk�adnika z OpenFoodFacts
    /// </summary>
    private async Task VerifyNutritionAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            VerificationStatus = "Wprowad� nazw� sk�adnika przed weryfikacj�";
            return;
        }

        try
        {
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
                VerificationStatus = $"? Nie znaleziono danych dla '{Name}' w OpenFoodFacts";
            }
        }
        catch (Exception ex)
        {
            VerificationStatus = $"? B��d weryfikacji: {ex.Message}";
            await Shell.Current.DisplayAlert(
                "B��d weryfikacji", 
                $"Wyst�pi� b��d podczas weryfikacji sk�adnika '{Name}': {ex.Message}", 
                "OK");
        }
        finally
        {
            IsVerifying = false;
            ((Command)VerifyNutritionCommand).ChangeCanExecute();
        }
    }

    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(Name) && 
               double.TryParse(Quantity, out var qty) && 
               qty > 0 &&
               double.TryParse(Calories, out _) &&
               double.TryParse(Protein, out _) &&
               double.TryParse(Fat, out _) &&
               double.TryParse(Carbs, out _);
    }

    private void ValidateInput()
    {
        ValidationMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = "Nazwa sk�adnika jest wymagana";
        }
        else if (!double.TryParse(Quantity, out var qty))
        {
            ValidationMessage = "Ilo�� musi by� liczb�";
        }
        else if (qty <= 0)
        {
            ValidationMessage = "Ilo�� musi by� wi�ksza od zera";
        }
        else if (!double.TryParse(Calories, out _))
        {
            ValidationMessage = "Kalorie musz� by� liczb�";
        }
        else if (!double.TryParse(Protein, out _))
        {
            ValidationMessage = "Bia�ko musi by� liczb�";
        }
        else if (!double.TryParse(Fat, out _))
        {
            ValidationMessage = "T�uszcze musz� by� liczb�";
        }
        else if (!double.TryParse(Carbs, out _))
        {
            ValidationMessage = "W�glowodany musz� by� liczb�";
        }

        OnPropertyChanged(nameof(HasValidationError));
        ((Command)SaveCommand).ChangeCanExecute();
        ((Command)VerifyNutritionCommand).ChangeCanExecute();
    }

    private async Task SaveAsync()
    {
        if (!CanSave())
        {
            return;
        }

        var qty = double.Parse(Quantity);
        var cal = double.Parse(Calories);
        var prot = double.Parse(Protein);
        var fat = double.Parse(Fat);
        var carbs = double.Parse(Carbs);
        
        try
        {
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
        await Shell.Current.GoToAsync("..");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
