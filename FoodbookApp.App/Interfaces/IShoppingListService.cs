using Foodbook.Models;

namespace FoodbookApp.Interfaces
{
    public interface IShoppingListService
    {
        Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to);
        Task<List<Ingredient>> GetShoppingListWithCheckedStateAsync(int planId);
        Task SaveShoppingListItemStateAsync(int planId, string ingredientName, Unit unit, bool isChecked, double quantity);
        Task SaveAllShoppingListStatesAsync(int planId, List<Ingredient> ingredients);
        Task RemoveShoppingListItemAsync(int planId, string ingredientName, Unit unit);
    }
}
