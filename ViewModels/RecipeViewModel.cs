using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
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
        }

        public async Task LoadRecipesAsync()
        {
            Recipes.Clear();
            var recipes = await _recipeService.GetRecipesAsync();
            foreach (var recipe in recipes)
                Recipes.Add(recipe);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}