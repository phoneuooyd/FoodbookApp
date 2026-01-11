using Foodbook.Models;

namespace FoodbookApp.Interfaces
{
    public interface IShoppingListService
    {
        Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to);
        Task<List<Ingredient>> GetShoppingListWithCheckedStateAsync(Guid planId);
        
        /// <summary>
        /// ? Save or update a shopping list item. Returns the database Id of the saved item.
        /// </summary>
        Task<Guid> SaveShoppingListItemStateAsync(Guid planId, Guid id, int order, string ingredientName, Unit unit, bool isChecked, double quantity);
        
        Task SaveAllShoppingListStatesAsync(Guid planId, List<Ingredient> ingredients);
        Task RemoveShoppingListItemAsync(Guid planId, string ingredientName, Unit unit);
        /// <summary>
        /// Remove item by database Id (preferred removal path when Id is known).
        /// </summary>
        Task RemoveShoppingListItemByIdAsync(Guid id);
    }
}
