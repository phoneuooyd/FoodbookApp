using Foodbook.Models;
namespace FoodbookApp.Interfaces;
public interface IIngredientService
{
    Task<List<Ingredient>> GetIngredientsAsync();
    
    /// <summary>
    /// ? NOWA METODA: Lightweight - pobiera tylko nazwy sk³adników
    /// </summary>
    Task<List<string>> GetIngredientNamesAsync();
    
    Task<Ingredient?> GetIngredientAsync(Guid id);
    Task AddIngredientAsync(Ingredient ingredient);
    Task UpdateIngredientAsync(Ingredient ingredient);
    Task DeleteIngredientAsync(Guid id);
    
    /// <summary>
    /// Invalidates the ingredients cache to force refresh on next access
    /// </summary>
    void InvalidateCache();
}
