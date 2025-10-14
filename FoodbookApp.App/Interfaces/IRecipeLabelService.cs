using Foodbook.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FoodbookApp.Interfaces
{
    public interface IRecipeLabelService
    {
        Task<List<RecipeLabel>> GetAllAsync(CancellationToken ct = default);
        Task<RecipeLabel?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<RecipeLabel> AddAsync(RecipeLabel label, CancellationToken ct = default);
        Task<RecipeLabel> UpdateAsync(RecipeLabel label, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    }
}
