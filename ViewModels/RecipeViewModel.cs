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


namespace Foodbook.ViewModels
{
    public class RecipeViewModel : INotifyPropertyChanged
    {
        private readonly IRecipeService _recipeService;

        public ObservableCollection<Recipe> Recipes { get; } = new();

        public ICommand AddRecipeCommand { get; }
        public ICommand EditRecipeCommand { get; }
        public ICommand DeleteRecipeCommand { get; }

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
        }

        public async Task LoadRecipesAsync()
        {
            Recipes.Clear();
            var recipes = await _recipeService.GetRecipesAsync();
            foreach (var recipe in recipes)
                Recipes.Add(recipe);
        }

        private async Task DeleteRecipeAsync(Recipe? recipe)
        {
            if (recipe == null) return;
            await _recipeService.DeleteRecipeAsync(recipe.Id);
            Recipes.Remove(recipe);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}