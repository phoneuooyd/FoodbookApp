using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;
using System.IO;
using System.IO.Compression;
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
                // USE CENTRALIZED DATABASE PATH - single source of truth
                var connectionString = DatabaseConfiguration.GetConnectionString();
                options.UseSqlite(connectionString);
            });
            return services;
        }
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly IServiceProvider _services;

        // **DEVELOPER FLAG: Set to TRUE for clean deployment (wipes all data), FALSE to preserve data**
        private const bool FORCE_CLEAN_DEPLOYMENT = false;

        // **SAFETY FLAG: Set to TRUE to automatically create archive backup before any deployment**
        // This protects against Visual Studio cache issues that may overwrite user data
        private const bool ARCHIVE_BEFORE_DEPLOY = true;

        // Safety archive naming
        private const string SafetyArchivePrefix = "Foodbook_Safety_Archive_Update";

        // Preferences DTO for serialization
        private record PrefsDto(string Culture, Foodbook.Models.AppTheme Theme, Foodbook.Models.AppColorTheme ColorTheme,
                                bool ColorfulBackground, bool WallpaperBackground,
                                Foodbook.Models.AppFontFamily FontFamily, Foodbook.Models.AppFontSize FontSize);

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
        /// Creates a safety archive of the current database before deployment.
        /// This protects against Visual Studio cache issues that may overwrite user data during recompilation.
        /// Archives are saved to Downloads/Foodbook folder as .fbk files.
        /// 
        /// Logic:
        /// - If an archive from TODAY already exists, it will be overwritten (updated)
        /// - If no archive from today exists, a new one is created
        /// - Maximum 5 archives are kept; oldest are deleted when limit is exceeded
        /// - Includes database files (db, wal, shm) and user preferences
        /// </summary>
        /// <returns>Path to created archive, or null if failed or database doesn't exist</returns>
        public async Task<string?> CreateSafetyArchiveAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseService] === SAFETY ARCHIVE START ===");

                var dbPath = DatabaseConfiguration.GetDatabasePath();
                
                // Check if database exists - if not, nothing to backup
                if (!File.Exists(dbPath))
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] No database found - skipping safety archive");
                    return null;
                }

                var dbInfo = new FileInfo(dbPath);
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Database found: {dbPath}");
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Database size: {dbInfo.Length} bytes, Last modified: {dbInfo.LastWriteTime}");

                // Skip if database is empty or very small (likely just schema, no data)
                if (dbInfo.Length < 1024) // Less than 1KB
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Database too small (likely empty) - skipping safety archive");
                    return null;
                }

                // Get target folder
                var targetFolder = GetSafetyArchiveFolder();
                if (string.IsNullOrEmpty(targetFolder))
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Could not determine safety archive folder");
                    return null;
                }

                try
                {
                    Directory.CreateDirectory(targetFolder);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Cannot create target folder: {ex.Message}");
                    // Fallback to cache directory
                    targetFolder = Path.Combine(FileSystem.CacheDirectory, "SafetyArchives");
                    Directory.CreateDirectory(targetFolder);
                }
                
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Target folder: {targetFolder}");

                // Check for existing archives and determine if we should update or create new
                var todayDatePrefix = DateTime.Now.ToString("yyyyMMdd");
                var existingArchives = GetExistingSafetyArchives(targetFolder);
                
                // Find archive from today (if exists)
                var todaysArchive = existingArchives
                    .FirstOrDefault(f => f.Name.Contains($"{SafetyArchivePrefix}_{todayDatePrefix}"));

                string archivePath;
                bool isUpdate = false;

                if (todaysArchive != null)
                {
                    // Update existing archive from today
                    archivePath = todaysArchive.FullName;
                    isUpdate = true;
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Found existing archive from today, will update: {todaysArchive.Name}");
                }
                else
                {
                    // Create new archive with timestamp
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var archiveName = $"{SafetyArchivePrefix}_{timestamp}.fbk";
                    archivePath = Path.Combine(targetFolder, archiveName);
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] No archive from today, creating new: {archiveName}");
                    
                    // Check if we need to clean up old archives BEFORE creating new one
                    // (to maintain max 5 archives including the new one)
                    CleanupOldSafetyArchivesSync(targetFolder, 4); // Keep 4, so after adding new = 5
                }

                System.Diagnostics.Debug.WriteLine($"[DatabaseService] {(isUpdate ? "Updating" : "Creating")} archive: {archivePath}");

                // Flush WAL before backup - but don't block if it fails
                try
                {
                    await FlushWalForBackupAsync(dbPath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] WAL flush warning (continuing): {ex.Message}");
                }

                // Export preferences to temp file
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
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] Preferences exported for archive");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Preferences export warning (continuing): {ex.Message}");
                    // Continue without preferences if export fails
                    prefsExportPath = null;
                }

                // Create archive directly at target location - USE TASK.RUN TO AVOID BLOCKING
                bool success = false;
                try
                {
                    // Delete existing file if updating
                    if (File.Exists(archivePath))
                    {
                        File.Delete(archivePath);
                    }

                    // Run ZIP creation on background thread to avoid UI blocking
                    await Task.Run(() =>
                    {
                        using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                        {
                            // Add main database file
                            if (File.Exists(dbPath))
                            {
                                zip.CreateEntryFromFile(dbPath, "database/foodbookapp.db");
                                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Added database to archive");
                            }

                            // Add WAL/SHM files if they exist
                            var (walPath, shmPath) = DatabaseConfiguration.GetWalFiles();
                            if (File.Exists(shmPath))
                            {
                                try
                                {
                                    zip.CreateEntryFromFile(shmPath, "database/foodbookapp.db-shm");
                                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Added SHM file to archive");
                                }
                                catch { /* ignore if locked */ }
                            }
                            if (File.Exists(walPath))
                            {
                                try
                                {
                                    zip.CreateEntryFromFile(walPath, "database/foodbookapp.db-wal");
                                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Added WAL file to archive");
                                }
                                catch { /* ignore if locked */ }
                            }

                            // Add preferences if exported successfully
                            if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                            {
                                try
                                {
                                    zip.CreateEntryFromFile(prefsExportPath, "prefs.json");
                                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Added preferences to archive");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Failed to add preferences: {ex.Message}");
                                }
                            }

                            // Add metadata
                            var metadata = new
                            {
                                CreatedAt = DateTime.Now.ToString("O"),
                                DatabaseSize = dbInfo.Length,
                                DatabaseLastModified = dbInfo.LastWriteTime.ToString("O"),
                                Type = "SafetyArchive",
                                Version = "1.0",
                                IsUpdate = isUpdate,
                                IncludesPreferences = !string.IsNullOrEmpty(prefsExportPath)
                            };
                            var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);
                            var metadataEntry = zip.CreateEntry("metadata.json");
                            using var writer = new StreamWriter(metadataEntry.Open());
                            writer.Write(metadataJson);
                        }
                    }).ConfigureAwait(false);

                    success = true;

                    // Cleanup temp preferences file
                    if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                    {
                        try { File.Delete(prefsExportPath); } catch { }
                    }

                    var archiveInfo = new FileInfo(archivePath);
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Safety archive {(isUpdate ? "updated" : "created")}: {archivePath}");
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Archive size: {archiveInfo.Length} bytes");
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] === SAFETY ARCHIVE COMPLETE ===");

                    return archivePath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Archive creation failed: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[DatabaseService] Stack trace: {ex.StackTrace}");
                    
                    // Cleanup temp preferences file
                    if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                    {
                        try { File.Delete(prefsExportPath); } catch { }
                    }
                    
                    // Try to cleanup partial archive (only if it was a new file, not update)
                    if (!isUpdate && !success)
                    {
                        try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Safety archive FAILED: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Gets list of existing safety archives sorted by date (newest first)
        /// </summary>
        private List<FileInfo> GetExistingSafetyArchives(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                    return new List<FileInfo>();

                return Directory.GetFiles(folder, $"{SafetyArchivePrefix}*.fbk")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .ToList();
            }
            catch
            {
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// Removes old safety archives, keeping only the most recent ones (synchronous version)
        /// </summary>
        private void CleanupOldSafetyArchivesSync(string folder, int keepCount)
        {
            try
            {
                var safetyArchives = GetExistingSafetyArchives(folder);
                var toDelete = safetyArchives.Skip(keepCount).ToList();

                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Found {safetyArchives.Count} archives, keeping {keepCount}, deleting {toDelete.Count}");

                foreach (var file in toDelete)
                {
                    try
                    {
                        file.Delete();
                        System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted old safety archive: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseService] Failed to delete archive {file.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the folder path for safety archives (Downloads/Foodbook)
        /// </summary>
        private static string? GetSafetyArchiveFolder()
        {
#if ANDROID
            try
            {
                var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(
                    Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (!string.IsNullOrWhiteSpace(downloads))
                {
                    return Path.Combine(downloads, "Foodbook");
                }
            }
            catch { }
            // Fallback to app data directory
            return Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives");
#elif WINDOWS
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(docs, "Foodbook");
#else
            return Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives");
#endif
        }

        /// <summary>
        /// Flushes SQLite WAL to ensure all data is in main database file before backup
        /// </summary>
        private async Task FlushWalForBackupAsync(string dbPath)
        {
            try
            {
                var cs = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite,
                    Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared
                }.ToString();

                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(cs);
                await conn.OpenAsync().ConfigureAwait(false);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine("[DatabaseService] WAL flushed for backup");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] WAL flush warning: {ex.Message}");
                // Continue anyway - backup will include WAL file
            }
        }

        /// <summary>
        /// Removes old safety archives, keeping only the most recent ones
        /// </summary>
        private async Task CleanupOldSafetyArchivesAsync(string folder, int keepCount)
        {
            try
            {
                await Task.Run(() =>
                {
                    var safetyArchives = Directory.GetFiles(folder, $"{SafetyArchivePrefix}*.fbk")
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .Skip(keepCount)
                        .ToList();

                    foreach (var file in safetyArchives)
                    {
                        try
                        {
                            file.Delete();
                            System.Diagnostics.Debug.WriteLine($"[DatabaseService] Deleted old safety archive: {file.Name}");
                        }
                        catch { }
                    }
                });
            }
            catch { }
        }

        /// <summary>
        /// Conditional deployment method:
        /// - Creates safety archive if ARCHIVE_BEFORE_DEPLOY is TRUE
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
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] ConditionalDeployment: ARCHIVE_BEFORE_DEPLOY = {ARCHIVE_BEFORE_DEPLOY}");

                // FIRST: Create safety archive if enabled (before any destructive operations)
                if (ARCHIVE_BEFORE_DEPLOY)
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Creating safety archive before deployment...");
                    var archivePath = await CreateSafetyArchiveAsync().ConfigureAwait(false);
                    if (archivePath != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DatabaseService] Safety archive created at: {archivePath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] No safety archive created (database empty or missing)");
                    }
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Safety archive step completed");
                }

                System.Diagnostics.Debug.WriteLine("[DatabaseService] Creating service scope...");
                using var scope = _services.CreateScope();
                System.Diagnostics.Debug.WriteLine("[DatabaseService] Service scope created");
                
                System.Diagnostics.Debug.WriteLine("[DatabaseService] Getting AppDbContext...");
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                System.Diagnostics.Debug.WriteLine("[DatabaseService] AppDbContext obtained");

                // Check if migrations are pending
                System.Diagnostics.Debug.WriteLine("[DatabaseService] Checking for pending migrations...");
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
                var hasPendingMigrations = pendingMigrations.Any();
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Pending migrations detected: {hasPendingMigrations}");

                if (FORCE_CLEAN_DEPLOYMENT)
                {
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] CLEAN DEPLOYMENT MODE - Wiping all app data and cache");

                    // 1. Delete database
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Deleting database...");
                    await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Database deleted");

                    // 2. Clear all preferences (MAUI Preferences)
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Clearing preferences...");
                    Preferences.Clear();
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Preferences cleared");

                    // 3. Clear secure storage
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Clearing secure storage...");
                    SecureStorage.RemoveAll();
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Secure storage cleared");

                    // 4. Delete all files in AppDataDirectory (cache, temp files, etc.)
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Clearing AppDataDirectory...");
                    await ClearAppDataDirectoryAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] AppDataDirectory cleared");

                    // 5. Delete all files in CacheDirectory
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Clearing CacheDirectory...");
                    await ClearCacheDirectoryAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] CacheDirectory cleared");

                    // 6. Recreate database with migrations
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] Recreating database...");
                    await db.Database.MigrateAsync().ConfigureAwait(false);
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
                        await db.Database.MigrateAsync().ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] Migrations applied successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[DatabaseService] Database schema is up-to-date");
                    }
                    
                    System.Diagnostics.Debug.WriteLine("[DatabaseService] ConditionalDeployment completed successfully");
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
