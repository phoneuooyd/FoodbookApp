using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.ViewModels
{
    public class ShoppingListViewModel
    {
        private readonly IShoppingListService _shoppingListService;

        public ObservableCollection<Ingredient> ShoppingList { get; } = new();
        public ICommand GenerateListCommand { get; }

        public ShoppingListViewModel(IShoppingListService shoppingListService)
        {
            _shoppingListService = shoppingListService ?? throw new ArgumentNullException(nameof(shoppingListService));

            GenerateListCommand = new Command(async () => await GenerateListAsync());
        }

        public async Task GenerateListAsync()
        {
            var items = await _shoppingListService.GetShoppingListAsync(DateTime.Today, DateTime.Today.AddDays(7));
            ShoppingList.Clear();
            foreach (var item in items)
                ShoppingList.Add(item);
        }
    }
}