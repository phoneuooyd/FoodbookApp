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
        
        /// <summary>
        /// Creates a safety archive of the current database before deployment.
        /// This protects against Visual Studio cache issues that may overwrite user data.
        /// </summary>
        /// <returns>Path to created archive, or null if failed or database doesn't exist</returns>
        Task<string?> CreateSafetyArchiveAsync();
    }
}
