using System.Threading.Tasks;

namespace FoodbookApp.Interfaces
{
    public interface IDatabaseService
    {
        Task InitializeAsync();
        Task<bool> EnsureDatabaseSchemaAsync();
        Task<bool> MigrateDatabaseAsync();
        Task<bool> ResetDatabaseAsync();
        Task<bool> ConditionalDeploymentAsync();
    }
}
