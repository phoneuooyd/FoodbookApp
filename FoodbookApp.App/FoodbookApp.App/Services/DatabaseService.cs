using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;

namespace Foodbook.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly IServiceProvider _services;

        public DatabaseService(IServiceProvider services)
        {
            _services = services;
        }

        public async Task InitializeAsync()
        {
            try
            {
                LogDebug("Initializing database (EF Core Migrations)");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Apply EF Core migrations (creates DB/tables if missing and updates schema)
                await db.Database.MigrateAsync();

                // Optional seeding outside of migrations
                await SeedData.InitializeAsync(db);

                LogDebug("Database initialization finished");
            }
            catch (Exception ex)
            {
                LogError($"DB init failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public async Task<bool> EnsureDatabaseSchemaAsync()
        {
            try
            {
                LogDebug("Ensuring database schema via EF Core migrations");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await db.Database.MigrateAsync();
                return true;
            }
            catch (Exception ex)
            {
                LogError($"EnsureDatabaseSchemaAsync failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> MigrateDatabaseAsync()
        {
            try
            {
                LogDebug("Applying EF Core migrations");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await db.Database.MigrateAsync();
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Database migration failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                LogDebug("Starting database reset");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
                await SeedData.InitializeAsync(db);
                LogDebug("Database reset completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Database reset failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private static void LogDebug(string message) => System.Diagnostics.Debug.WriteLine($"[DatabaseService] {message}");
        private static void LogWarning(string message) => System.Diagnostics.Debug.WriteLine($"[DatabaseService] WARNING: {message}");
        private static void LogError(string message) => System.Diagnostics.Debug.WriteLine($"[DatabaseService] ERROR: {message}");
    }
}
