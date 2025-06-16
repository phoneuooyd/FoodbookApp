using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;

namespace Foodbook.ViewModels
{
    public class ShoppingListViewModel
    {
        public ObservableCollection<Ingredient> ShoppingList { get; set; } = new();
        public ICommand GenerateListCommand { get; }

        public ShoppingListViewModel()
        {
            // Stub: initialize commands
        }
    }
}