using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using Foodbook.Data;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.Linq;


namespace Foodbook.ViewModels
{
    public class RecipeViewModel : INotifyPropertyChanged
    {
        private readonly IRecipeService _recipeService;
        private readonly IIngredientService _ingredientService;
        private bool _isLoading;
        private bool _isRefreshing;
        private string _searchText = string.Empty;
        private List<Recipe> _allRecipes = new();

        public ObservableCollection<Recipe> Recipes { get; } = new();

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
                FilterRecipes();
            }
        }

        public ICommand AddRecipeCommand { get; }
        public ICommand EditRecipeCommand { get; }
        public ICommand DeleteRecipeCommand { get; }
        public ICommand RefreshCommand { get; }

        public RecipeViewModel(IRecipeService recipeService, IIngredientService ingredientService)
        {
            _recipeService = recipeService;
            _ingredientService = ingredientService;
            AddRecipeCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(AddRecipePage)));
            EditRecipeCommand = new Command<Recipe>(async r =>
            {
                if (r != null)
                    await Shell.Current.GoToAsync($"{nameof(AddRecipePage)}?id={r.Id}");
            });
            DeleteRecipeCommand = new Command<Recipe>(async r => await DeleteRecipeAsync(r));
            RefreshCommand = new Command(async () => await ReloadAsync());
        }

        public async Task LoadRecipesAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                var recipes = await _recipeService.GetRecipesAsync();
                
                // Recalculate nutritional values for all recipes to ensure accuracy
                await RecalculateNutritionalValuesForRecipes(recipes);
                
                _allRecipes = recipes;
                
                // Clear and add in batches to improve UI responsiveness
                Recipes.Clear();
                
                // Add items in smaller batches to prevent UI blocking
                const int batchSize = 50;
                for (int i = 0; i < recipes.Count; i += batchSize)
                {
                    var batch = recipes.Skip(i).Take(batchSize);
                    foreach (var recipe in batch)
                    {
                        Recipes.Add(recipe);
                    }
                    
                    // Allow UI to update between batches
                    if (i + batchSize < recipes.Count)
                    {
                        await Task.Delay(1);
                    }
                }
                
                FilterRecipes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading recipes: {ex.Message}");
                // Could show user-friendly error message here
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RecalculateNutritionalValuesForRecipes(List<Recipe> recipes)
        {
            try
            {
                // Get all ingredients from the database for reference
                var allIngredients = await _ingredientService.GetIngredientsAsync();
                
                foreach (var recipe in recipes)
                {
                    if (recipe.Ingredients?.Any() == true)
                    {
                        double totalCalories = 0;
                        double totalProtein = 0;
                        double totalFat = 0;
                        double totalCarbs = 0;

                        foreach (var ingredient in recipe.Ingredients)
                        {
                            // Find the ingredient in the database to get current nutritional values
                            var dbIngredient = allIngredients.FirstOrDefault(i => i.Name == ingredient.Name);
                            if (dbIngredient != null)
                            {
                                // Update ingredient with current nutritional values from database
                                ingredient.Calories = dbIngredient.Calories;
                                ingredient.Protein = dbIngredient.Protein;
                                ingredient.Fat = dbIngredient.Fat;
                                ingredient.Carbs = dbIngredient.Carbs;
                            }

                            // Calculate conversion factor based on unit
                            double factor = GetUnitConversionFactor(ingredient.Unit, ingredient.Quantity);
                            
                            totalCalories += ingredient.Calories * factor;
                            totalProtein += ingredient.Protein * factor;
                            totalFat += ingredient.Fat * factor;
                            totalCarbs += ingredient.Carbs * factor;
                        }

                        // Update recipe nutritional values with calculated totals
                        recipe.Calories = totalCalories;
                        recipe.Protein = totalProtein;
                        recipe.Fat = totalFat;
                        recipe.Carbs = totalCarbs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recalculating nutritional values: {ex.Message}");
            }
        }

        private double GetUnitConversionFactor(Unit unit, double quantity)
        {
            // Same conversion logic as in AddRecipeViewModel
            // Assumption: nutritional values in database are per 100g/100ml/1 piece
            return unit switch
            {
                Unit.Gram => quantity / 100.0,        // values per 100g
                Unit.Milliliter => quantity / 100.0,  // values per 100ml  
                Unit.Piece => quantity,               // values per 1 piece
                _ => quantity / 100.0
            };
        }

        public async Task ReloadAsync()
        {
            if (IsRefreshing) return;
            
            IsRefreshing = true;
            try
            {
                await LoadRecipesAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void FilterRecipes()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // If no search text, show all recipes
                if (Recipes.Count != _allRecipes.Count)
                {
                    Recipes.Clear();
                    foreach (var recipe in _allRecipes)
                    {
                        Recipes.Add(recipe);
                    }
                }
            }
            else
            {
                // Filter recipes based on search text (name and description)
                var filtered = _allRecipes
                    .Where(r => r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                               (r.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
                
                Recipes.Clear();
                foreach (var recipe in filtered)
                {
                    Recipes.Add(recipe);
                }
            }
        }

        private async Task DeleteRecipeAsync(Recipe? recipe)
        {
            if (recipe == null) return;
            
            try
            {
                await _recipeService.DeleteRecipeAsync(recipe.Id);
                Recipes.Remove(recipe);
                _allRecipes.Remove(recipe);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting recipe: {ex.Message}");
                // Could show user-friendly error message here
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}