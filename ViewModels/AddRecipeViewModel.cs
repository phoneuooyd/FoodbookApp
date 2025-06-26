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
                OnPropertyChanged(nameof(IsImportMode)); // <-- DODAJ TO
            }
        }

        public bool IsImportMode => !IsManualMode;

        // Pola do recznego dodawania
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        private string _name;
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        private string _description;
        public string Calories { get => _calories; set { _calories = value; OnPropertyChanged(); } }
        private string _calories;
        public string Protein { get => _protein; set { _protein = value; OnPropertyChanged(); } }
        private string _protein;
        public string Fat { get => _fat; set { _fat = value; OnPropertyChanged(); } }
        private string _fat;
        public string Carbs { get => _carbs; set { _carbs = value; OnPropertyChanged(); } }
        private string _carbs;

        public ObservableCollection<Ingredient> Ingredients { get; set; } = new();

        // Pola do importu
        public string ImportUrl { get => _importUrl; set { _importUrl = value; OnPropertyChanged(); } }
        private string _importUrl;

        public string ImportStatus { get => _importStatus; set { _importStatus = value; OnPropertyChanged(); } }
        private string _importStatus;

        // Komendy
        public ICommand AddIngredientCommand { get; }
        public ICommand RemoveIngredientCommand { get; }
        public ICommand SaveRecipeCommand { get; }
        public ICommand ImportRecipeCommand { get; }
        public ICommand SetManualModeCommand { get; }
        public ICommand SetImportModeCommand { get; }

        private readonly IRecipeService _recipeService;
        private readonly IIngredientService _ingredientService;
        private readonly RecipeImporter _importer;

        public AddRecipeViewModel(IRecipeService recipeService, IIngredientService ingredientService, RecipeImporter importer)
        {
            _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
            _ingredientService = ingredientService ?? throw new ArgumentNullException(nameof(ingredientService));
            _importer = importer ?? throw new ArgumentNullException(nameof(importer));

            AddIngredientCommand = new Command(AddIngredient);
            RemoveIngredientCommand = new Command<Ingredient>(RemoveIngredient);
            SaveRecipeCommand = new Command(async () => await SaveRecipeAsync());
            ImportRecipeCommand = new Command(async () => await ImportRecipeAsync());
            SetManualModeCommand = new Command(() => IsManualMode = true);
            SetImportModeCommand = new Command(() => IsManualMode = false);
        }

        public async Task LoadRecipeAsync(int id)
        {
            var recipe = await _recipeService.GetRecipeAsync(id);
            if (recipe == null)
                return;

            _editingRecipe = recipe;
            Name = recipe.Name;
            Description = recipe.Description ?? string.Empty;
            Calories = recipe.Calories.ToString();
            Protein = recipe.Protein.ToString();
            Fat = recipe.Fat.ToString();
            Carbs = recipe.Carbs.ToString();
            Ingredients.Clear();
            foreach (var ing in recipe.Ingredients)
                Ingredients.Add(new Ingredient { Id = ing.Id, Name = ing.Name, Quantity = ing.Quantity, Unit = ing.Unit, RecipeId = ing.RecipeId });
        }

        private void AddIngredient()
        {
            var name = AvailableIngredientNames.FirstOrDefault() ?? string.Empty;
            Ingredients.Add(new Ingredient { Name = name, Quantity = 0, Unit = Unit.Gram });
        }

        private void RemoveIngredient(Ingredient ingredient)
        {
            if (Ingredients.Contains(ingredient))
                Ingredients.Remove(ingredient);
        }

        private async Task ImportRecipeAsync()
        {
            ImportStatus = "Importowanie...";
            try
            {
                var recipe = await _importer.ImportFromUrlAsync(ImportUrl);
                Name = recipe.Name;
                Description = recipe.Description;
                Calories = recipe.Calories.ToString();
                Protein = recipe.Protein.ToString();
                Fat = recipe.Fat.ToString();
                Carbs = recipe.Carbs.ToString();
                Ingredients.Clear();
                if (recipe.Ingredients != null)
                {
                    foreach (var ing in recipe.Ingredients)
                        Ingredients.Add(ing);
                }
                ImportStatus = "Zaimporotwano!";
            }
            catch
            {
                ImportStatus = "Blad importu!";
            }
        }

        private async Task SaveRecipeAsync()
        {

            var recipe = _editingRecipe ?? new Recipe();
            recipe.Name = Name;
            recipe.Description = Description;
            recipe.Calories = double.TryParse(Calories, out var cal) ? cal : 0;
            recipe.Protein = double.TryParse(Protein, out var prot) ? prot : 0;
            recipe.Fat = double.TryParse(Fat, out var fat) ? fat : 0;
            recipe.Carbs = double.TryParse(Carbs, out var carbs) ? carbs : 0;
            recipe.Ingredients = Ingredients.ToList();

            // Walidacja: nie zapisuj pustych przepisów
            if (string.IsNullOrWhiteSpace(recipe.Name) || recipe.Ingredients.Count == 0)
                return;

            if (_editingRecipe == null)
                await _recipeService.AddRecipeAsync(recipe);
            else
                await _recipeService.UpdateRecipeAsync(recipe);

            if (_editingRecipe == null)
            {
                Name = Description = Calories = Protein = Fat = Carbs = string.Empty;
                Ingredients.Clear();
                ImportUrl = string.Empty;
                ImportStatus = string.Empty;
            }

            await Shell.Current.GoToAsync("..");
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

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
