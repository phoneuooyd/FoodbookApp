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

        public RecipeViewModel(IRecipeService recipeService)
        {
            _recipeService = recipeService;
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