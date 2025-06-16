using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;

namespace Foodbook.ViewModels
{
    public class RecipeViewModel
    {
        public ObservableCollection<Recipe> Recipes { get; set; } = new();
        public ICommand AddRecipeCommand { get; }
        public ICommand EditRecipeCommand { get; }
        public ICommand DeleteRecipeCommand { get; }

        public RecipeViewModel()
        {
            // Stub: initialize commands
        }
    }
}