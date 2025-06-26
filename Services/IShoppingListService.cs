using Foodbook.Models;

namespace Foodbook.Services
{
    public interface IShoppingListService
    {
        Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to);
    }
}
