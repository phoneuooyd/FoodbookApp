using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Foodbook.ViewModels
{
    public class AddRecipeViewModel : INotifyPropertyChanged
    {
        private Recipe? _editingRecipe;

        // Tryb: true = reczny, false = import z linku
        private bool _isManualMode = true;
        public bool IsManualMode
        {
            get => _isManualMode;
            set
            {
                _isManualMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsImportMode));
            }
        }

        public bool IsImportMode => !IsManualMode;

        // Pola do recznego dodawania
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); ValidateInput(); } }
        private string _name = string.Empty;
        
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        private string _description = string.Empty;
        
        public string IloscPorcji { get => _iloscPorcji; set { _iloscPorcji = value; OnPropertyChanged(); ValidateInput(); } }
        private string _iloscPorcji = "2";
        
        public string Calories { get => _calories; set { _calories = value; OnPropertyChanged(); ValidateInput(); } }
        private string _calories = "0";
        
        public string Protein { get => _protein; set { _protein = value; OnPropertyChanged(); ValidateInput(); } }
        private string _protein = "0";
        
        public string Fat { get => _fat; set { _fat = value; OnPropertyChanged(); ValidateInput(); } }
        private string _fat = "0";
        
        public string Carbs { get => _carbs; set { _carbs = value; OnPropertyChanged(); ValidateInput(); } }
        private string _carbs = "0";

        // Właściwości dla automatycznie obliczanych wartości
        public string CalculatedCalories { get => _calculatedCalories; private set { _calculatedCalories = value; OnPropertyChanged(); } }
        private string _calculatedCalories = "0";
        
        public string CalculatedProtein { get => _calculatedProtein; private set { _calculatedProtein = value; OnPropertyChanged(); } }
        private string _calculatedProtein = "0";
        
        public string CalculatedFat { get => _calculatedFat; private set { _calculatedFat = value; OnPropertyChanged(); } }
        private string _calculatedFat = "0";
        
        public string CalculatedCarbs { get => _calculatedCarbs; private set { _calculatedCarbs = value; OnPropertyChanged(); } }
        private string _calculatedCarbs = "0";

        // Flaga kontrolująca czy używać automatycznych obliczeń
        private bool _useCalculatedValues = true;
        public bool UseCalculatedValues 
        { 
            get => _useCalculatedValues; 
            set 
            { 
                _useCalculatedValues = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseManualValues));
                if (value)
                {
                    // Gdy włączamy automatyczne obliczenia, kopiujemy obliczone wartości
                    Calories = CalculatedCalories;
                    Protein = CalculatedProtein;
                    Fat = CalculatedFat;
                    Carbs = CalculatedCarbs;
                }
            } 
        }
        
        public bool UseManualValues => !UseCalculatedValues;

        public ObservableCollection<Ingredient> Ingredients { get; set; } = new();

        public string Title => _editingRecipe == null ? "Nowy przepis" : "Edytuj przepis";

        public string SaveButtonText => _editingRecipe == null ? "Dodaj przepis" : "Zapisz zmiany";

        public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; OnPropertyChanged(); } }
        private string _validationMessage = string.Empty;

        public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

        // Pola do importu
        public string ImportUrl { get => _importUrl; set { _importUrl = value; OnPropertyChanged(); } }
        private string _importUrl = string.Empty;

        public string ImportStatus { get => _importStatus; set { _importStatus = value; OnPropertyChanged(); } }
        private string _importStatus = string.Empty;

        // Komendy
        public ICommand AddIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand SaveRecipeCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ImportRecipeCommand { get; }
        public ICommand SetManualModeCommand { get; }
        public ICommand SetImportModeCommand { get; }
        public ICommand CopyCalculatedValuesCommand { get; }

        private readonly IRecipeService _recipeService;
        private readonly IIngredientService _ingredientService;
        private readonly RecipeImporter _importer;
        private readonly ILocalizationService _localizationService;

        public AddRecipeViewModel(IRecipeService recipeService, IIngredientService ingredientService, RecipeImporter importer, ILocalizationService localizationService)
        {
            _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
            _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _localizationService = localizationService;
            _localizationService.CultureChanged += OnCultureChanged;

            AddIngredientCommand = new Command(AddIngredient);
            RemoveIngredientCommand = new Command<Ingredient>(RemoveIngredient);
            SaveRecipeCommand = new Command(async () => await SaveRecipeAsync(), CanSave);
            CancelCommand = new Command(async () => await CancelAsync());
            ImportRecipeCommand = new Command(async () => await ImportRecipeAsync());
            SetManualModeCommand = new Command(() => IsManualMode = true);
            SetImportModeCommand = new Command(() => IsManualMode = false);
            CopyCalculatedValuesCommand = new Command(CopyCalculatedValues);

            Ingredients.CollectionChanged += (_, __) => 
            {
                CalculateNutritionalValues();
                ValidateInput();
            };
            ValidateInput();
        }

        public void Reset()
        {
            _editingRecipe = null;
            Name = Description = string.Empty;
            IloscPorcji = "2";
            Calories = Protein = Fat = Carbs = "0";
            CalculatedCalories = CalculatedProtein = CalculatedFat = CalculatedCarbs = "0";
            
            Ingredients.Clear();
            
            ImportUrl = string.Empty;
            ImportStatus = string.Empty;
            UseCalculatedValues = true;
            IsManualMode = true; // Resetuj też tryb na ręczny
            
            // Powiadom o zmianach w tytule i przycisku
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
            
            ValidateInput();
        }

        public bool IsEditMode => _editingRecipe != null;

        public async Task LoadRecipeAsync(int id)
        {
            var recipe = await _recipeService.GetRecipeAsync(id);
            if (recipe == null)
                return;

            _editingRecipe = recipe;
            Name = recipe.Name;
            Description = recipe.Description ?? string.Empty;
            IloscPorcji = recipe.IloscPorcji.ToString();
            Calories = recipe.Calories.ToString("F1");
            Protein = recipe.Protein.ToString("F1");
            Fat = recipe.Fat.ToString("F1");
            Carbs = recipe.Carbs.ToString("F1");
            
            Ingredients.Clear();
            foreach (var ing in recipe.Ingredients)
            {
                var ingredient = new Ingredient 
                { 
                    Id = ing.Id, 
                    Name = ing.Name, 
                    Quantity = ing.Quantity, 
                    Unit = ing.Unit, 
                    RecipeId = ing.RecipeId,
                    Calories = ing.Calories,
                    Protein = ing.Protein,
                    Fat = ing.Fat,
                    Carbs = ing.Carbs
                };
                
                Ingredients.Add(ingredient);
            }
            
            CalculateNutritionalValues();
            
            // Ważne: Powiadom interfejs o zmianach w tytule i przycisku
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
        }

        private async void AddIngredient()
        {
            var name = AvailableIngredientNames.FirstOrDefault() ?? string.Empty;
            var ingredient = new Ingredient 
            { 
                Name = name, 
                Quantity = 1, 
                Unit = Unit.Gram, 
                Calories = 0, 
                Protein = 0, 
                Fat = 0, 
                Carbs = 0 
            };
            
            // Jeśli składnik istnieje w bazie, pobierz jego wartości odżywcze
            if (!string.IsNullOrEmpty(name))
            {
                var existingIngredients = await _ingredientService.GetIngredientsAsync();
                var existingIngredient = existingIngredients.FirstOrDefault(i => i.Name == name);
                if (existingIngredient != null)
                {
                    ingredient.Calories = existingIngredient.Calories;
                    ingredient.Protein = existingIngredient.Protein;
                    ingredient.Fat = existingIngredient.Fat;
                    ingredient.Carbs = existingIngredient.Carbs;
                }
            }
            
            Ingredients.Add(ingredient);
            ValidateInput();
        }

        private void RemoveIngredient(Ingredient ingredient)
        {
            if (Ingredients.Contains(ingredient))
            {
                Ingredients.Remove(ingredient);
            }
            ValidateInput();
        }

        // Publiczna metoda do przeliczania wartości odżywczych (wywołana z code-behind)
        public void RecalculateNutritionalValues()
        {
            CalculateNutritionalValues();
        }

        // Nowa metoda do aktualizacji wartości odżywczych składnika
        public async Task UpdateIngredientNutritionalValuesAsync(Ingredient ingredient)
        {
            if (string.IsNullOrEmpty(ingredient.Name))
                return;

            var existingIngredients = await _ingredientService.GetIngredientsAsync();
            var existingIngredient = existingIngredients.FirstOrDefault(i => i.Name == ingredient.Name);
            
            if (existingIngredient != null)
            {
                ingredient.Calories = existingIngredient.Calories;
                ingredient.Protein = existingIngredient.Protein;
                ingredient.Fat = existingIngredient.Fat;
                ingredient.Carbs = existingIngredient.Carbs;
            }
            else
            {
                // Jeśli składnik nie istnieje w bazie, resetuj wartości
                ingredient.Calories = 0;
                ingredient.Protein = 0;
                ingredient.Fat = 0;
                ingredient.Carbs = 0;
            }

            // Przelicz wartości odżywcze po aktualizacji
            CalculateNutritionalValues();
        }

        private async void CalculateNutritionalValues()
        {
            double totalCalories = 0;
            double totalProtein = 0;
            double totalFat = 0;
            double totalCarbs = 0;

            // Załaduj aktualną listę składników z bazy danych
            var existingIngredients = await _ingredientService.GetIngredientsAsync();

            foreach (var ingredient in Ingredients)
            {
                // Znajdź składnik w bazie danych i zaktualizuj jego wartości odżywcze
                var dbIngredient = existingIngredients.FirstOrDefault(i => i.Name == ingredient.Name);
                if (dbIngredient != null)
                {
                    ingredient.Calories = dbIngredient.Calories;
                    ingredient.Protein = dbIngredient.Protein;
                    ingredient.Fat = dbIngredient.Fat;
                    ingredient.Carbs = dbIngredient.Carbs;
                }

                // Oblicz współczynnik przeliczeniowy na podstawie jednostki
                double factor = GetUnitConversionFactor(ingredient.Unit, ingredient.Quantity);
                
                totalCalories += ingredient.Calories * factor;
                totalProtein += ingredient.Protein * factor;
                totalFat += ingredient.Fat * factor;
                totalCarbs += ingredient.Carbs * factor;
            }

            CalculatedCalories = totalCalories.ToString("F1");
            CalculatedProtein = totalProtein.ToString("F1");
            CalculatedFat = totalFat.ToString("F1");
            CalculatedCarbs = totalCarbs.ToString("F1");

            // Jeśli używamy automatycznych obliczeń, aktualizuj główne wartości
            if (UseCalculatedValues)
            {
                Calories = CalculatedCalories;
                Protein = CalculatedProtein;
                Fat = CalculatedFat;
                Carbs = CalculatedCarbs;
            }
        }

        private double GetUnitConversionFactor(Unit unit, double quantity)
        {
            // Założenie: wartości odżywcze w bazie są podane na 100g/100ml/1 sztukę
            return unit switch
            {
                Unit.Gram => quantity / 100.0,        // wartości na 100g
                Unit.Milliliter => quantity / 100.0,  // wartości na 100ml  
                Unit.Piece => quantity,               // wartości na 1 sztukę
                _ => quantity / 100.0
            };
        }

        private void CopyCalculatedValues()
        {
            Calories = CalculatedCalories;
            Protein = CalculatedProtein;
            Fat = CalculatedFat;
            Carbs = CalculatedCarbs;
        }

        private async Task ImportRecipeAsync()
        {
            ImportStatus = "Importowanie...";
            try
            {
                var recipe = await _importer.ImportFromUrlAsync(ImportUrl);
                Name = recipe.Name;
                Description = recipe.Description ?? string.Empty;
                
                Ingredients.Clear();
                
                if (recipe.Ingredients != null)
                {
                    foreach (var ing in recipe.Ingredients)
                    {
                        Ingredients.Add(ing);
                    }
                }
                
                // Oblicz wartości odżywcze z składników
                CalculateNutritionalValues();
                
                // Jeśli import nie dostarczył wartości odżywczych, użyj obliczonych
                if (recipe.Calories == 0 && recipe.Protein == 0 && recipe.Fat == 0 && recipe.Carbs == 0)
                {
                    UseCalculatedValues = true;
                }
                else
                {
                    // Jeśli import dostarczył wartości, użyj ich ale pozwól na przełączenie
                    UseCalculatedValues = false;
                    Calories = recipe.Calories.ToString("F1");
                    Protein = recipe.Protein.ToString("F1");
                    Fat = recipe.Fat.ToString("F1");
                    Carbs = recipe.Carbs.ToString("F1");
                }
                
                ImportStatus = "Zaimportowano!";
                IsManualMode = true; // Przełącz na tryb ręczny po imporcie
            }
            catch (Exception ex)
            {
                ImportStatus = $"Błąd importu: {ex.Message}";
            }
        }

        private bool CanSave()
        {
            return !HasValidationError;
        }

        private void ValidateInput()
        {
            ValidationMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Name))
            {
                ValidationMessage = "Nazwa przepisu jest wymagana";
            }
            else if (!IsValidInt(IloscPorcji))
            {
                ValidationMessage = "Ilość porcji musi być liczbą całkowitą większą od 0";
            }
            // Usuwamy wymaganie składników - teraz można zapisać przepis bez składników
            else if (!IsValidDouble(Calories))
            {
                ValidationMessage = "Kalorie muszą być liczbą";
            }
            else if (!IsValidDouble(Protein))
            {
                ValidationMessage = "Białko musi być liczbą";
            }
            else if (!IsValidDouble(Fat))
            {
                ValidationMessage = "Tłuszcze muszą być liczbą";
            }
            else if (!IsValidDouble(Carbs))
            {
                ValidationMessage = "Węglowodany muszą być liczbą";
            }
            else
            {
                // Sprawdzenie składników tylko jeśli istnieją
                foreach (var ing in Ingredients)
                {
                    if (string.IsNullOrWhiteSpace(ing.Name))
                    {
                        ValidationMessage = "Każdy składnik musi mieć nazwę";
                        break;
                    }
                    if (ing.Quantity <= 0)
                    {
                        ValidationMessage = "Ilość składnika musi być większa od zera";
                        break;
                    }
                }
            }

            OnPropertyChanged(nameof(HasValidationError));
            ((Command)SaveRecipeCommand).ChangeCanExecute();
        }

        private static bool IsValidDouble(string value)
        {
            return double.TryParse(value, out var result) && result >= 0;
        }

        private static bool IsValidInt(string value)
        {
            return int.TryParse(value, out var result) && result > 0;
        }

        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }

        private async Task SaveRecipeAsync()
        {
            ValidateInput();
            if (HasValidationError)
                return;

            var recipe = _editingRecipe ?? new Recipe();
            recipe.Name = Name;
            recipe.Description = Description;
            recipe.IloscPorcji = int.TryParse(IloscPorcji, out var porcje) ? porcje : 2;
            recipe.Calories = double.TryParse(Calories, out var cal) ? cal : 0;
            recipe.Protein = double.TryParse(Protein, out var prot) ? prot : 0;
            recipe.Fat = double.TryParse(Fat, out var fat) ? fat : 0;
            recipe.Carbs = double.TryParse(Carbs, out var carbs) ? carbs : 0;
            recipe.Ingredients = Ingredients.ToList();

            try
            {
                if (_editingRecipe == null)
                    await _recipeService.AddRecipeAsync(recipe);
                else
                    await _recipeService.UpdateRecipeAsync(recipe);

                // Reset form po udanym zapisie tylko w trybie dodawania
                if (_editingRecipe == null)
                {
                    Name = Description = string.Empty;
                    IloscPorcji = "2";
                    Calories = Protein = Fat = Carbs = "0";
                    CalculatedCalories = CalculatedProtein = CalculatedFat = CalculatedCarbs = "0";
                    
                    Ingredients.Clear();
                    
                    ImportUrl = string.Empty;
                    ImportStatus = string.Empty;
                    UseCalculatedValues = true;
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Błąd zapisywania: {ex.Message}";
            }
        }

        // Dostępne jednostki i lista nazw składników
        public IEnumerable<Unit> Units { get; } = Enum.GetValues(typeof(Unit)).Cast<Unit>();
        public ObservableCollection<string> AvailableIngredientNames { get; } = new();

        public async Task LoadAvailableIngredientsAsync()
        {
            AvailableIngredientNames.Clear();
            var list = await _ingredientService.GetIngredientsAsync();
            foreach (var ing in list)
                AvailableIngredientNames.Add(ing.Name);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void RefreshTranslations()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
        }

        private void OnCultureChanged()
        {
            RefreshTranslations();
        }
    }
}
