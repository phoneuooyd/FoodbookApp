using System.IO;
using Microsoft.Maui.Storage;

namespace Foodbook.Data
{
    /// <summary>
    /// Centralized database configuration to ensure consistent database path usage across all services.
    /// This prevents database path mismatches between different parts of the application.
    /// </summary>
    public static class DatabaseConfiguration
    {
        // SINGLE SOURCE OF TRUTH for database filename
        private const string DatabaseFileName = "foodbookapp.db";

        /// <summary>
        /// Gets the absolute path to the SQLite database file.
        /// This is the single source of truth for the database location.
        /// </summary>
        public static string GetDatabasePath()
        {
            return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
        }

        /// <summary>
        /// Gets the SQLite connection string for the application database.
        /// </summary>
        public static string GetConnectionString()
        {
            return $"Data Source={GetDatabasePath()}";
        }

        /// <summary>
        /// Gets the paths to WAL and SHM files (Write-Ahead Log and Shared Memory).
        /// These files are part of SQLite's WAL mode and need to be included in backups.
        /// </summary>
        public static (string walPath, string shmPath) GetWalFiles()
        {
            var dbPath = GetDatabasePath();
            return (dbPath + "-wal", dbPath + "-shm");
        }
    }
}
