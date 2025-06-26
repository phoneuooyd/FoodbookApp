using Foodbook.Models;
namespace Foodbook.Services;
public interface IIngredientService
{
    Task<List<Ingredient>> GetIngredientsAsync();
    Task<Ingredient?> GetIngredientAsync(int id);
    Task AddIngredientAsync(Ingredient ingredient);
    Task UpdateIngredientAsync(Ingredient ingredient);
    Task DeleteIngredientAsync(int id);
}
