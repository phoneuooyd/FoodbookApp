using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;
using System.IO;
using System.IO.Compression;
using Microsoft.Maui.Storage;
using FoodbookApp.Interfaces;
using Microsoft.Data.Sqlite;

namespace Foodbook.Services
{
    public static class ServiceCollectionDbExtensions
    {
        public static IServiceCollection AddAppDbContext(this IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                var connectionString = DatabaseConfiguration.GetConnectionString();
                System.Diagnostics.Debug.WriteLine($"[AddAppDbContext] Connection string: {connectionString}");
                options.UseSqlite(connectionString);
            });
            return services;
        }
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly IServiceProvider _services;

        private const bool FORCE_CLEAN_DEPLOYMENT = false;
        private const bool ARCHIVE_BEFORE_DEPLOY = true;
        private const string SafetyArchivePrefix = "Foodbook_Safety_Archive_Update";

        private static readonly string[] RequiredTables = 
        {
            "Recipes", "Ingredients", "Folders", "RecipeLabels", 
            "Plans", "PlannedMeals", "ShoppingListItems", 
            "AuthAccounts", "SyncQueue", "SyncStates",
            "__EFMigrationsHistory"
        };

        private record PrefsDto(string Culture, Foodbook.Models.AppTheme Theme, Foodbook.Models.AppColorTheme ColorTheme,
                                bool ColorfulBackground, bool WallpaperBackground,
                                Foodbook.Models.AppFontFamily FontFamily, Foodbook.Models.AppFontSize FontSize);

        public DatabaseService(IServiceProvider services)
        {
            _services = services;
            LogInfo("DatabaseService created");
            LogInfo($"Database path: {DatabaseConfiguration.GetDatabasePath()}");
        }

        public async Task InitializeAsync()
        {
            LogInfo("=== InitializeAsync START ===");
            
            try
            {
                var dbPath = DatabaseConfiguration.GetDatabasePath();
                LogInfo($"Database path: {dbPath}");
                LogInfo($"Database exists: {File.Exists(dbPath)}");

                if (File.Exists(dbPath))
                {
                    var fi = new FileInfo(dbPath);
                    LogInfo($"Database size: {fi.Length} bytes, LastWrite: {fi.LastWriteTime}");
                }

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                
                LogInfo($"Applied migrations: {string.Join(", ", appliedMigrations)}");
                LogInfo($"Pending migrations: {string.Join(", ", pendingMigrations)}");

                if (pendingMigrations.Any())
                {
                    LogInfo("Applying pending migrations...");
                    await db.Database.MigrateAsync();
                    LogInfo("Migrations applied successfully");
                }
                else
                {
                    LogInfo("No pending migrations - schema up to date");
                }

                var (isValid, missingTables) = await ValidateSchemaAsync();
                if (!isValid)
                {
                    LogError($"Schema validation FAILED! Missing tables: {string.Join(", ", missingTables)}");
                    LogWarning("Attempting schema repair...");
                    await RepairSchemaAsync(db);
                    
                    var (isValidAfterRepair, stillMissing) = await ValidateSchemaAsync();
                    if (!isValidAfterRepair)
                    {
                        LogError($"Schema repair FAILED! Still missing: {string.Join(", ", stillMissing)}");
                        throw new InvalidOperationException($"Database schema is corrupted. Missing tables: {string.Join(", ", stillMissing)}");
                    }
                    LogInfo("Schema repair successful");
                }
                else
                {
                    LogInfo("Schema validation passed - all required tables exist");
                }

                LogInfo("=== InitializeAsync COMPLETE ===");
            }
            catch (Exception ex)
            {
                LogError($"InitializeAsync FAILED: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private async Task<(bool IsValid, List<string> MissingTables)> ValidateSchemaAsync()
        {
            var missingTables = new List<string>();
            
            try
            {
                var dbPath = DatabaseConfiguration.GetDatabasePath();
                if (!File.Exists(dbPath))
                {
                    LogWarning("Database file does not exist yet");
                    return (false, RequiredTables.ToList());
                }

                var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                
                await using var connection = new SqliteConnection(DatabaseConfiguration.GetConnectionString());
                await connection.OpenAsync();
                
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var tableName = reader.GetString(0);
                    existingTables.Add(tableName);
                }

                LogInfo($"Found tables in DB: {string.Join(", ", existingTables)}");

                foreach (var required in RequiredTables)
                {
                    if (!existingTables.Contains(required))
                    {
                        missingTables.Add(required);
                    }
                }

                return (missingTables.Count == 0, missingTables);
            }
            catch (Exception ex)
            {
                LogError($"Schema validation error: {ex.Message}");
                return (false, RequiredTables.ToList());
            }
        }

        private async Task RepairSchemaAsync(AppDbContext db)
        {
            LogWarning("=== SCHEMA REPAIR START ===");
            
            try
            {
                LogInfo("Attempting EnsureCreated + Migrate...");
                await db.Database.EnsureCreatedAsync();
                await db.Database.MigrateAsync();
                LogInfo("Schema repair attempt completed");
            }
            catch (Exception ex)
            {
                LogError($"Schema repair failed: {ex.Message}");
                LogWarning("Last resort: deleting and recreating database...");
                try
                {
                    await db.Database.EnsureDeletedAsync();
                    await db.Database.MigrateAsync();
                    LogInfo("Database recreated from scratch");
                }
                catch (Exception ex2)
                {
                    LogError($"Database recreation failed: {ex2.Message}");
                    throw;
                }
            }
            
            LogWarning("=== SCHEMA REPAIR END ===");
        }

        public async Task<bool> EnsureDatabaseSchemaAsync()
        {
            try
            {
                LogInfo("EnsureDatabaseSchemaAsync called");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                
                var (isValid, missing) = await ValidateSchemaAsync();
                if (!isValid)
                {
                    LogError($"Schema validation failed after migration. Missing: {string.Join(", ", missing)}");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"EnsureDatabaseSchemaAsync failed: {ex.Message}");
                return false;
            }
        }

        public Task<bool> MigrateDatabaseAsync() => EnsureDatabaseSchemaAsync();

        public async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                LogWarning("ResetDatabaseAsync called - this will DELETE all data!");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                LogInfo("Deleting database...");
                await db.Database.EnsureDeletedAsync();
                
                LogInfo("Recreating database with migrations...");
                await db.Database.MigrateAsync();
                
                var (isValid, missing) = await ValidateSchemaAsync();
                if (!isValid)
                {
                    LogError($"Reset failed - missing tables: {string.Join(", ", missing)}");
                    return false;
                }
                
                LogInfo("Database reset completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"ResetDatabaseAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ConditionalDeploymentAsync()
        {
            LogInfo("=== ConditionalDeploymentAsync START ===");
            LogInfo($"FORCE_CLEAN_DEPLOYMENT = {FORCE_CLEAN_DEPLOYMENT}");
            LogInfo($"ARCHIVE_BEFORE_DEPLOY = {ARCHIVE_BEFORE_DEPLOY}");
            LogInfo($"Database path: {DatabaseConfiguration.GetDatabasePath()}");

            try
            {
                if (ARCHIVE_BEFORE_DEPLOY)
                {
                    LogInfo("Creating safety archive before deployment...");
                    try
                    {
                        var archivePath = await CreateSafetyArchiveAsync().ConfigureAwait(false);
                        LogInfo(archivePath != null 
                            ? $"Safety archive created: {archivePath}" 
                            : "No archive created (database empty or missing)");
                    }
                    catch (Exception archiveEx)
                    {
                        LogWarning($"Safety archive failed (continuing): {archiveEx.Message}");
                    }
                }

                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var dbPath = DatabaseConfiguration.GetDatabasePath();
                var dbExists = File.Exists(dbPath);
                LogInfo($"Database file exists: {dbExists}");

                if (dbExists)
                {
                    var fi = new FileInfo(dbPath);
                    LogInfo($"Database size: {fi.Length} bytes");
                }

                IEnumerable<string> pendingMigrations;
                IEnumerable<string> appliedMigrations;
                
                try
                {
                    pendingMigrations = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
                    appliedMigrations = await db.Database.GetAppliedMigrationsAsync().ConfigureAwait(false);
                    
                    LogInfo($"Applied migrations ({appliedMigrations.Count()}): {string.Join(", ", appliedMigrations)}");
                    LogInfo($"Pending migrations ({pendingMigrations.Count()}): {string.Join(", ", pendingMigrations)}");
                }
                catch (Exception migEx)
                {
                    LogWarning($"Could not get migration status: {migEx.Message}");
                    LogInfo("Database may be corrupted or not initialized - will attempt to create/repair");
                    pendingMigrations = new[] { "Unknown" };
                    appliedMigrations = Array.Empty<string>();
                }

                if (FORCE_CLEAN_DEPLOYMENT)
                {
                    LogWarning("CLEAN DEPLOYMENT MODE - Wiping all app data");
                    
                    await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
                    Preferences.Clear();
                    SecureStorage.RemoveAll();
                    await ClearAppDataDirectoryAsync().ConfigureAwait(false);
                    await ClearCacheDirectoryAsync().ConfigureAwait(false);
                    await db.Database.MigrateAsync().ConfigureAwait(false);
                    
                    LogInfo("Fresh database created");
                    return true;
                }

                if (pendingMigrations.Any())
                {
                    LogInfo("Applying pending migrations...");
                    try
                    {
                        await db.Database.MigrateAsync().ConfigureAwait(false);
                        LogInfo("Migrations applied successfully");
                    }
                    catch (Exception migEx)
                    {
                        LogError($"Migration failed: {migEx.Message}");
                        LogWarning("Attempting database repair after migration failure...");
                        await RepairSchemaAsync(db);
                    }
                }
                else
                {
                    LogInfo("No pending migrations");
                }

                var (isValid, missingTables) = await ValidateSchemaAsync();
                if (!isValid)
                {
                    LogError($"Schema validation failed! Missing: {string.Join(", ", missingTables)}");
                    LogWarning("Attempting schema repair...");
                    await RepairSchemaAsync(db);
                    
                    var (isValidAfterRepair, stillMissing) = await ValidateSchemaAsync();
                    if (!isValidAfterRepair)
                    {
                        LogError($"Schema repair failed! Still missing: {string.Join(", ", stillMissing)}");
                        return false;
                    }
                }

                LogInfo("=== ConditionalDeploymentAsync COMPLETE ===");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"ConditionalDeploymentAsync FAILED: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<string?> CreateSafetyArchiveAsync()
        {
            try
            {
                LogInfo("=== SAFETY ARCHIVE START ===");

                var dbPath = DatabaseConfiguration.GetDatabasePath();
                
                if (!File.Exists(dbPath))
                {
                    LogInfo("No database found - skipping safety archive");
                    return null;
                }

                var dbInfo = new FileInfo(dbPath);
                LogInfo($"Database found: {dbPath}, size: {dbInfo.Length} bytes");

                if (dbInfo.Length < 1024)
                {
                    LogInfo("Database too small (likely empty) - skipping safety archive");
                    return null;
                }

                var targetFolder = GetSafetyArchiveFolder();
                if (string.IsNullOrEmpty(targetFolder))
                {
                    LogWarning("Could not determine safety archive folder");
                    return null;
                }

                try { Directory.CreateDirectory(targetFolder); }
                catch (Exception ex)
                {
                    LogWarning($"Cannot create target folder: {ex.Message}");
                    targetFolder = Path.Combine(FileSystem.CacheDirectory, "SafetyArchives");
                    Directory.CreateDirectory(targetFolder);
                }

                var todayDatePrefix = DateTime.Now.ToString("yyyyMMdd");
                var existingArchives = GetExistingSafetyArchives(targetFolder);
                var todaysArchive = existingArchives.FirstOrDefault(f => f.Name.Contains($"{SafetyArchivePrefix}_{todayDatePrefix}"));

                string archivePath;
                bool isUpdate = false;

                if (todaysArchive != null)
                {
                    archivePath = todaysArchive.FullName;
                    isUpdate = true;
                    LogInfo($"Updating existing archive: {todaysArchive.Name}");
                }
                else
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var archiveName = $"{SafetyArchivePrefix}_{timestamp}.fbk";
                    archivePath = Path.Combine(targetFolder, archiveName);
                    CleanupOldSafetyArchivesSync(targetFolder, 4);
                    LogInfo($"Creating new archive: {archiveName}");
                }

                try { await FlushWalForBackupAsync(dbPath).ConfigureAwait(false); }
                catch (Exception ex) { LogWarning($"WAL flush warning: {ex.Message}"); }

                string? prefsExportPath = null;
                try
                {
                    var prefsService = _services.GetService<IPreferencesService>();
                    if (prefsService != null)
                    {
                        prefsExportPath = Path.Combine(FileSystem.CacheDirectory, $"prefs_safety_{Guid.NewGuid():N}.json");
                        var prefsDto = new PrefsDto(
                            prefsService.GetSavedLanguage(),
                            prefsService.GetSavedTheme(),
                            prefsService.GetSavedColorTheme(),
                            prefsService.GetIsColorfulBackgroundEnabled(),
                            prefsService.GetIsWallpaperEnabled(),
                            prefsService.GetSavedFontFamily(),
                            prefsService.GetSavedFontSize()
                        );
                        var json = System.Text.Json.JsonSerializer.Serialize(prefsDto);
                        await File.WriteAllTextAsync(prefsExportPath, json).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Preferences export warning: {ex.Message}");
                    prefsExportPath = null;
                }

                bool success = false;
                try
                {
                    if (File.Exists(archivePath)) File.Delete(archivePath);

                    await Task.Run(() =>
                    {
                        using var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create);
                        
                        if (File.Exists(dbPath))
                            zip.CreateEntryFromFile(dbPath, "database/foodbookapp.db");

                        var (walPath, shmPath) = DatabaseConfiguration.GetWalFiles();
                        if (File.Exists(shmPath))
                            try { zip.CreateEntryFromFile(shmPath, "database/foodbookapp.db-shm"); } catch { }
                        if (File.Exists(walPath))
                            try { zip.CreateEntryFromFile(walPath, "database/foodbookapp.db-wal"); } catch { }

                        if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                            try { zip.CreateEntryFromFile(prefsExportPath, "prefs.json"); } catch { }

                        var metadata = new
                        {
                            CreatedAt = DateTime.Now.ToString("O"),
                            DatabaseSize = dbInfo.Length,
                            DatabaseLastModified = dbInfo.LastWriteTime.ToString("O"),
                            Type = "SafetyArchive",
                            Version = "1.0",
                            IsUpdate = isUpdate
                        };
                        var metadataEntry = zip.CreateEntry("metadata.json");
                        using var writer = new StreamWriter(metadataEntry.Open());
                        writer.Write(System.Text.Json.JsonSerializer.Serialize(metadata));
                    }).ConfigureAwait(false);

                    success = true;
                    if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                        try { File.Delete(prefsExportPath); } catch { }

                    LogInfo($"Safety archive {(isUpdate ? "updated" : "created")}: {archivePath}");
                    return archivePath;
                }
                catch (Exception ex)
                {
                    LogError($"Archive creation failed: {ex.Message}");
                    if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                        try { File.Delete(prefsExportPath); } catch { }
                    if (!isUpdate && !success && File.Exists(archivePath))
                        try { File.Delete(archivePath); } catch { }
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"Safety archive FAILED: {ex.Message}");
                return null;
            }
        }

        private List<FileInfo> GetExistingSafetyArchives(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return new List<FileInfo>();
                return Directory.GetFiles(folder, $"{SafetyArchivePrefix}*.fbk")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        private void CleanupOldSafetyArchivesSync(string folder, int keepCount)
        {
            try
            {
                var toDelete = GetExistingSafetyArchives(folder).Skip(keepCount).ToList();
                foreach (var file in toDelete)
                    try { file.Delete(); } catch { }
            }
            catch { }
        }

        private static string? GetSafetyArchiveFolder()
        {
#if ANDROID
            try
            {
                var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (!string.IsNullOrWhiteSpace(downloads))
                    return Path.Combine(downloads, "Foodbook");
            }
            catch { }
            return Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives");
#elif WINDOWS
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Foodbook");
#else
            return Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives");
#endif
        }

        private async Task FlushWalForBackupAsync(string dbPath)
        {
            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Cache = SqliteCacheMode.Shared
                }.ToString();

                using var conn = new SqliteConnection(cs);
                await conn.OpenAsync().ConfigureAwait(false);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWarning($"WAL flush warning: {ex.Message}");
            }
        }

        private async Task ClearAppDataDirectoryAsync()
        {
            try
            {
                var appDataPath = FileSystem.AppDataDirectory;
                if (!Directory.Exists(appDataPath)) return;

                foreach (var file in Directory.GetFiles(appDataPath, "*", SearchOption.AllDirectories))
                {
                    if (!file.EndsWith(".db") && !file.EndsWith(".db-shm") && !file.EndsWith(".db-wal"))
                        try { File.Delete(file); } catch { }
                }

                foreach (var dir in Directory.GetDirectories(appDataPath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        try { Directory.Delete(dir); } catch { }
                }
            }
            catch { }
            await Task.CompletedTask;
        }

        private async Task ClearCacheDirectoryAsync()
        {
            try
            {
                var cachePath = FileSystem.CacheDirectory;
                if (!Directory.Exists(cachePath)) return;

                foreach (var file in Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories))
                    try { File.Delete(file); } catch { }

                foreach (var dir in Directory.GetDirectories(cachePath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        try { Directory.Delete(dir); } catch { }
                }
            }
            catch { }
            await Task.CompletedTask;
        }

        private static void LogInfo(string message) => 
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] {message}");
        
        private static void LogWarning(string message) => 
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] WARNING: {message}");
        
        private static void LogError(string message) => 
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] ERROR: {message}");
    }
}
