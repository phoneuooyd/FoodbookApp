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
        // Registers AppDbContext with the default SQLite connection used by the app (minimal settings)
        public static IServiceCollection AddAppDbContext(this IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp_dev.db");
                options.UseSqlite($"Data Source={dbPath}");
            });
            return services;
        }
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly IServiceProvider _services;

        // **DEVELOPER FLAG: Set to TRUE for clean deployment (wipes all data), FALSE to preserve data**
        private const bool FORCE_CLEAN_DEPLOYMENT = true;

        public DatabaseService(IServiceProvider services)
        {
            _services = services;
        }

        // Creates DB and applies migrations on app startup. No extra pragmas, no seeding.
        public async Task InitializeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        // Ensures schema is up-to-date
        public async Task<bool> EnsureDatabaseSchemaAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Triggers migrations on demand (e.g., from SettingsPage)
        public Task<bool> MigrateDatabaseAsync()
        {
            return EnsureDatabaseSchemaAsync();
        }

        // Drops and recreates DB schema, no extra pragmas or seeding
        public async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Conditional deployment method:
        /// - Detects if migrations are needed
        /// - If FORCE_CLEAN_DEPLOYMENT is TRUE: wipes ALL app data, cache, preferences, and recreates database
        /// - If FALSE: only applies pending migrations
        /// Call this method at app startup BEFORE InitializeAsync
        /// </summary>
        public async Task<bool> ConditionalDeploymentAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] ConditionalDeployment: FORCE_CLEAN_DEPLOYMENT = {FORCE_CLEAN_DEPLOYMENT}");

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Check if migrations are pending
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                var hasPendingMigrations = pendingMigrations.Any();

                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Pending migrations detected: {hasPendingMigrations}");

                if (FORCE_CLEAN_DEPLOYMENT)
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] CLEAN DEPLOYMENT MODE - Wiping all app data and cache");

                    // 1. Delete database
                    await db.Database.EnsureDeletedAsync();
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Database deleted");

                    // 2. Clear all preferences (MAUI Preferences)
                    Preferences.Clear();
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Preferences cleared");

                    // 3. Clear secure storage
                    SecureStorage.RemoveAll();
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Secure storage cleared");

                    // 4. Delete all files in AppDataDirectory (cache, temp files, etc.)
                    await ClearAppDataDirectoryAsync();

                    // 5. Delete all files in CacheDirectory
                    await ClearCacheDirectoryAsync();

                    // 6. Recreate database with migrations
                    await db.Database.MigrateAsync();
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Database recreated with fresh schema");

                    System.Diagnostics.Debug.WriteLine("[DatabaseService] CLEAN DEPLOYMENT COMPLETED - All data wiped");
                    return true;
                }
                else
                {
                    // Normal mode: just apply pending migrations if any
                    if (hasPendingMigrations)
                    {
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] Applying pending migrations...");
                        await db.Database.MigrateAsync();
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] Migrations applied successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] Database schema is up-to-date");
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] ConditionalDeployment failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Clears all files in AppDataDirectory except for the database file itself (to avoid access conflicts)
        /// </summary>
        private async Task ClearAppDataDirectoryAsync()
        {
            try
            {
                var appDataPath = FileSystem.AppDataDirectory;
                if (Directory.Exists(appDataPath))
                {
                    var files = Directory.GetFiles(appDataPath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            // Skip the database file as it's being handled by EF Core
                            if (!file.EndsWith(".db") && !file.EndsWith(".db-shm") && !file.EndsWith(".db-wal"))
                            {
                                File.Delete(file);
                                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted file: {file}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Failed to delete file {file}: {ex.Message}");
                        }
                    }

                    // Delete empty directories
                    var directories = Directory.GetDirectories(appDataPath, "*", SearchOption.AllDirectories);
                    foreach (var dir in directories.OrderByDescending(d => d.Length)) // Delete deepest first
                    {
                        try
                        {
                            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir);
                                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted directory: {dir}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Failed to delete directory {dir}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] ClearAppDataDirectory failed: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears all files in CacheDirectory
        /// </summary>
        private async Task ClearCacheDirectoryAsync()
        {
            try
            {
                var cachePath = FileSystem.CacheDirectory;
                if (Directory.Exists(cachePath))
                {
                    var files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted cache file: {file}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Failed to delete cache file {file}: {ex.Message}");
                        }
                    }

                    // Delete empty directories
                    var directories = Directory.GetDirectories(cachePath, "*", SearchOption.AllDirectories);
                    foreach (var dir in directories.OrderByDescending(d => d.Length))
                    {
                        try
                        {
                            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir);
                                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted cache directory: {dir}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Failed to delete cache directory {dir}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] ClearCacheDirectory failed: {ex.Message}");
            }
            await Task.CompletedTask;
        }
    }
}
