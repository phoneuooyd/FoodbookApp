using Foodbook.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Foodbook.Services
{
    public interface IRecipeService
    {
        Task<List<Recipe>> GetRecipesAsync();
        Task<Recipe?> GetRecipeAsync(int id);
        Task AddRecipeAsync(Recipe recipe);
        Task UpdateRecipeAsync(Recipe recipe);
        Task DeleteRecipeAsync(int id);
    }
}