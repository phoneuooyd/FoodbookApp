using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;
using System.IO;
using Microsoft.Maui.Storage;
using FoodbookApp.Interfaces;

namespace Foodbook.Services
{
    public static class ServiceCollectionDbExtensions
    {
        // Registers AppDbContext with the default SQLite connection used by the app
        public static IServiceCollection AddAppDbContext(this IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");
                options.UseSqlite($"Data Source={dbPath};Cache=Shared;Pooling=True;Default Timeout=20;Foreign Keys=True");
            });
            return services;
        }
    }

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

                // Apply PRAGMAs after migrations
                try
                {
                    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                }
                catch (Exception pragmaEx)
                {
                    LogWarning($"PRAGMA setup skipped/failed: {pragmaEx.Message}");
                }

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
                // Reapply PRAGMAs just in case
                try
                {
                    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                }
                catch (Exception pragmaEx)
                {
                    LogWarning($"PRAGMA setup skipped/failed: {pragmaEx.Message}");
                }
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
                try
                {
                    await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;");
                    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                }
                catch (Exception pragmaEx)
                {
                    LogWarning($"PRAGMA setup skipped/failed: {pragmaEx.Message}");
                }
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
