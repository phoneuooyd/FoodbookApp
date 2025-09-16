using System.Threading.Tasks;

namespace Foodbook.Services
{
    public interface IDatabaseService
    {
        Task InitializeAsync();
        Task<bool> EnsureDatabaseSchemaAsync();
        Task<bool> MigrateDatabaseAsync();
        Task<bool> ResetDatabaseAsync();
    }
}
