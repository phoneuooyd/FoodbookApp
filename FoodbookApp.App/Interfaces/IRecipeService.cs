using Foodbook.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FoodbookApp.Interfaces
{
    public interface IRecipeService
    {
        Task<List<Recipe>> GetRecipesAsync();
        Task<Recipe?> GetRecipeAsync(Guid id);
        Task AddRecipeAsync(Recipe recipe);
        Task UpdateRecipeAsync(Recipe recipe);
        Task DeleteRecipeAsync(Guid id);
    }
}