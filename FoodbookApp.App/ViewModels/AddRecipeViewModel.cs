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
using System.Threading;
using System.Threading.Tasks;

namespace Foodbook.ViewModels
{
    public class AddRecipeViewModel : INotifyPropertyChanged
    {
        private Recipe? _editingRecipe;
        
        // ✅ NOWE: Cache składników z debouncing
        private List<Ingredient> _cachedIngredients = new();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheValidityDuration = TimeSpan.FromMinutes(5);
        
        // ✅ NOWE: Debouncing dla kalkulacji
        private CancellationTokenSource _calculationCts = new();
        private readonly SemaphoreSlim _calculationSemaphore = new(1, 1);
        
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
                    OnPropertyChanged(nameof(IsIngredientsTabSelected));
                    OnPropertyChanged(nameof(IsNutritionTabSelected));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting SelectedTabIndex: {ex.Message}");
                }
            }
        }
        
        public bool IsBasicInfoTabSelected => SelectedTabIndex == 0;
        public bool IsIngredientsTabSelected => SelectedTabIndex == 1;
        public bool IsNutritionTabSelected => SelectedTabIndex == 2;
        
        public ICommand SelectTabCommand { get; }

        // Tryb: true = reczny, false = import z linku
        private bool _isManualMode = true;
        public bool IsManualMode
        {
            get => _isManualMode;
            set
            {
                try
                {
                    _isManualMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsImportMode));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting IsManualMode: {ex.Message}");
                }
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
                try
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error setting UseCalculatedValues: {ex.Message}");
                }
            } 
        }
        
        public bool UseManualValues => !UseCalculatedValues;

        public ObservableCollection<Ingredient> Ingredients { get; set; } = new();

        public string Title => _editingRecipe == null 
            ? FoodbookApp.Localization.ButtonResources.NewRecipe 
            : FoodbookApp.Localization.ButtonResources.EditRecipe;

        public string SaveButtonText => _editingRecipe == null 
            ? FoodbookApp.Localization.ButtonResources.AddRecipe 
            : FoodbookApp.Localization.ButtonResources.SaveChanges;

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
        private readonly IEventBus _eventBus;

        public AddRecipeViewModel(IRecipeService recipeService, IIngredientService ingredientService, RecipeImporter importer, IEventBus eventBus)
        {
            _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
            _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            AddIngredientCommand = new Command(AddIngredient);
            RemoveIngredientCommand = new Command<Ingredient>(RemoveIngredient);
            SaveRecipeCommand = new Command(async () => await SaveRecipeAsync(), CanSave);
            CancelCommand = new Command(async () => await CancelAsync());
            ImportRecipeCommand = new Command(async () => await ImportRecipeAsync());
            SetManualModeCommand = new Command(() => IsManualMode = true);
            SetImportModeCommand = new Command(() => IsManualMode = false);
            CopyCalculatedValuesCommand = new Command(CopyCalculatedValues);
            SelectTabCommand = new Command<object>(SelectTab);

            // ✅ ZOPTYMALIZOWANE: Asynchroniczne event handling
            Ingredients.CollectionChanged += async (_, __) => 
            {
                await ScheduleNutritionalCalculationAsync();
                ValidateInput();
            };

            ValidateInput();
        }

        // ✅ NOWE: Metoda ładowania cache
        private async Task EnsureIngredientsAreCachedAsync()
        {
            if (_cachedIngredients.Count == 0 || 
                DateTime.Now - _lastCacheUpdate > _cacheValidityDuration)
            {
                _cachedIngredients = await _ingredientService.GetIngredientsAsync();
                _lastCacheUpdate = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"Ingredients cache refreshed: {_cachedIngredients.Count} items");
            }
        }

        // ✅ NOWE: Debounced kalkulacja
        private async Task ScheduleNutritionalCalculationAsync()
        {
            // Anuluj poprzednie kalkulacje
            _calculationCts.Cancel();
            _calculationCts = new CancellationTokenSource();

            try
            {
                // Czekaj 300ms na kolejne zmiany
                await Task.Delay(300, _calculationCts.Token);
                
                await _calculationSemaphore.WaitAsync(_calculationCts.Token);
                try
                {
                    await CalculateNutritionalValuesAsync();
                }
                finally
                {
                    _calculationSemaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Normalne anulowanie - ignoruj
            }
        }

        // ✅ NOWE: Publiczna metoda do synchronizacji cache
        public async Task RefreshIngredientsCacheAsync()
        {
            _lastCacheUpdate = DateTime.MinValue; // Force refresh
            await EnsureIngredientsAreCachedAsync();
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
                if (tabIndex >= 0 && tabIndex <= 2)
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

        public void Reset()
        {
            try
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
                SelectedTabIndex = 0; // Reset do pierwszej zakładki
                
                // Powiadom o zmianach w tytule i przycisku
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(SaveButtonText));
                
                ValidateInput();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in Reset: {ex.Message}");
            }
        }

        public bool IsEditMode => _editingRecipe != null;

        public async Task LoadRecipeAsync(int id)
        {
            try
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
                
                await ScheduleNutritionalCalculationAsync();
                
                // Ważne: Powiadom interfejs o zmianach w tytule i przycisku
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(SaveButtonText));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadRecipeAsync: {ex.Message}");
                ValidationMessage = $"Błąd ładowania przepisu: {ex.Message}";
            }
        }

        private async void AddIngredient()
        {
            try
            {
                await EnsureIngredientsAreCachedAsync();
                
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
                
                // ✅ ZOPTYMALIZOWANE: Używa cache zamiast pobierania z bazy
                if (!string.IsNullOrEmpty(name))
                {
                    var existingIngredient = _cachedIngredients.FirstOrDefault(i => i.Name == name);
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddIngredient: {ex.Message}");
                ValidationMessage = $"Błąd dodawania składnika: {ex.Message}";
            }
        }

        private void RemoveIngredient(Ingredient ingredient)
        {
            try
            {
                if (Ingredients.Contains(ingredient))
                {
                    Ingredients.Remove(ingredient);
                }
                ValidateInput();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RemoveIngredient: {ex.Message}");
                ValidationMessage = $"Błąd usuwania składnika: {ex.Message}";
            }
        }

        // Publiczna metoda do przeliczania wartości odżywczych (wywołana z code-behind)
        public async Task RecalculateNutritionalValuesAsync()
        {
            try
            {
                await ScheduleNutritionalCalculationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RecalculateNutritionalValuesAsync: {ex.Message}");
            }
        }

        // ✅ ZOPTYMALIZOWANE: Szybkie aktualizacje bez pełnego pobierania
        public async Task UpdateIngredientNutritionalValuesAsync(Ingredient ingredient)
        {
            try
            {
                if (string.IsNullOrEmpty(ingredient.Name))
                    return;

                await EnsureIngredientsAreCachedAsync();
                
                var existingIngredient = _cachedIngredients.FirstOrDefault(i => i.Name == ingredient.Name);
                
                if (existingIngredient != null)
                {
                    ingredient.Calories = existingIngredient.Calories;
                    ingredient.Protein = existingIngredient.Protein;
                    ingredient.Fat = existingIngredient.Fat;
                    ingredient.Carbs = existingIngredient.Carbs;
                }
                else
                {
                    // Reset wartości dla nieistniejących składników
                    ingredient.Calories = 0;
                    ingredient.Protein = 0;
                    ingredient.Fat = 0;
                    ingredient.Carbs = 0;
                }

                // Wyzwól przeliczenie z debouncing
                await ScheduleNutritionalCalculationAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateIngredientNutritionalValuesAsync: {ex.Message}");
            }
        }

        // ✅ ZOPTYMALIZOWANE: Szybka kalkulacja z cache
        private async Task CalculateNutritionalValuesAsync()
        {
            try
            {
                await EnsureIngredientsAreCachedAsync();

                double totalCalories = 0;
                double totalProtein = 0;
                double totalFat = 0;
                double totalCarbs = 0;

                foreach (var ingredient in Ingredients)
                {
                    // Używa cache zamiast pobierania z bazy
                    var dbIngredient = _cachedIngredients.FirstOrDefault(i => i.Name == ingredient.Name);
                    if (dbIngredient != null)
                    {
                        ingredient.Calories = dbIngredient.Calories;
                        ingredient.Protein = dbIngredient.Protein;
                        ingredient.Fat = dbIngredient.Fat;
                        ingredient.Carbs = dbIngredient.Carbs;
                    }

                    double factor = GetUnitConversionFactor(ingredient.Unit, ingredient.Quantity);
                    
                    totalCalories += ingredient.Calories * factor;
                    totalProtein += ingredient.Protein * factor;
                    totalFat += ingredient.Fat * factor;
                    totalCarbs += ingredient.Carbs * factor;
                }

                // Aktualizacja UI w wątku głównym
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    CalculatedCalories = totalCalories.ToString("F1");
                    CalculatedProtein = totalProtein.ToString("F1");
                    CalculatedFat = totalFat.ToString("F1");
                    CalculatedCarbs = totalCarbs.ToString("F1");

                    if (UseCalculatedValues)
                    {
                        Calories = CalculatedCalories;
                        Protein = CalculatedProtein;
                        Fat = CalculatedFat;
                        Carbs = CalculatedCarbs;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CalculateNutritionalValuesAsync: {ex.Message}");
            }
        }

        private double GetUnitConversionFactor(Unit unit, double quantity)
        {
            try
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetUnitConversionFactor: {ex.Message}");
                return 0;
            }
        }

        private void CopyCalculatedValues()
        {
            try
            {
                Calories = CalculatedCalories;
                Protein = CalculatedProtein;
                Fat = CalculatedFat;
                Carbs = CalculatedCarbs;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CopyCalculatedValues: {ex.Message}");
            }
        }

        private async Task ImportRecipeAsync()
        {
            try
            {
                ImportStatus = "Importowanie...";
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
                await ScheduleNutritionalCalculationAsync();
                
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
                System.Diagnostics.Debug.WriteLine($"Error in ImportRecipeAsync: {ex.Message}");
            }
        }

        private bool CanSave()
        {
            try
            {
                // ✅ OPTYMALIZACJA: Bardziej szczegółowa logika CanSave
                bool isValid = !HasValidationError && !string.IsNullOrWhiteSpace(Name);
                
                System.Diagnostics.Debug.WriteLine($"CanSave: {isValid} (HasValidationError: {HasValidationError}, Name: '{Name}')");
                
                return isValid;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ValidateInput: {ex.Message}");
                ValidationMessage = "Błąd walidacji danych";
            }
        }

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

        private static bool IsValidInt(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                    return false;
                    
                return int.TryParse(value, out var result) && result > 0;
            }
            catch
            {
                return false;
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

        private async Task SaveRecipeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 SaveRecipeAsync started");
                
                ValidateInput();
                if (HasValidationError)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Validation failed: {ValidationMessage}");
                    return;
                }

                var recipe = _editingRecipe ?? new Recipe();
                recipe.Name = Name;
                recipe.Description = Description;
                recipe.IloscPorcji = int.TryParse(IloscPorcji, out var porcje) ? porcje : 2;
                
                // Parse nutritional values with proper decimal separator handling
                recipe.Calories = ParseDoubleValue(Calories);
                recipe.Protein = ParseDoubleValue(Protein);
                recipe.Fat = ParseDoubleValue(Fat);
                recipe.Carbs = ParseDoubleValue(Carbs);
                
                recipe.Ingredients = Ingredients.ToList();

                System.Diagnostics.Debug.WriteLine($"💾 Saving recipe: {recipe.Name} (Edit mode: {_editingRecipe != null})");

                string action;
                if (_editingRecipe == null)
                {
                    await _recipeService.AddRecipeAsync(recipe);
                    action = "Created";
                    System.Diagnostics.Debug.WriteLine("✅ Recipe added successfully");
                }
                else
                {
                    await _recipeService.UpdateRecipeAsync(recipe);
                    action = "Updated";
                    System.Diagnostics.Debug.WriteLine("✅ Recipe updated successfully");
                }

                // Notify other ViewModels that a recipe was created/updated
                _eventBus.PublishDataChanged("Recipe", action, recipe);
                System.Diagnostics.Debug.WriteLine($"[AddRecipeViewModel] Published Recipe {action} event for recipe {recipe.Name}");

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
                    
                    System.Diagnostics.Debug.WriteLine("🔄 Form reset after successful add");
                }

                // Zawsze wróć do grida po zapisie
                System.Diagnostics.Debug.WriteLine("🔙 Navigating back");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                ValidationMessage = $"Błąd zapisywania: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"❌ Error in SaveRecipeAsync: {ex.Message}");
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

        // Dostępne jednostki i lista nazw składników
        public IEnumerable<Unit> Units { get; } = Enum.GetValues(typeof(Unit)).Cast<Unit>();
        public ObservableCollection<string> AvailableIngredientNames { get; } = new();

        public async Task LoadAvailableIngredientsAsync()
        {
            try
            {
                await EnsureIngredientsAreCachedAsync();
                
                AvailableIngredientNames.Clear();
                foreach (var ing in _cachedIngredients)
                    AvailableIngredientNames.Add(ing.Name);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadAvailableIngredientsAsync: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
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
    }
}
