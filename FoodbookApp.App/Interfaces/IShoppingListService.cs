using Foodbook.Models;

namespace FoodbookApp.Interfaces
{
    public interface IShoppingListService
    {
        Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to);
        Task<List<Ingredient>> GetShoppingListWithCheckedStateAsync(int planId);
        
        /// <summary>
        /// ? Save or update a shopping list item. Returns the database Id of the saved item.
        /// </summary>
        Task<int> SaveShoppingListItemStateAsync(int planId, int id, int order, string ingredientName, Unit unit, bool isChecked, double quantity);
        
        Task SaveAllShoppingListStatesAsync(int planId, List<Ingredient> ingredients);
        Task RemoveShoppingListItemAsync(int planId, string ingredientName, Unit unit);
    }
}
