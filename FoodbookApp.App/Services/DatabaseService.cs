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
                options.UseSqlite(DatabaseConfiguration.GetConnectionString());
            });
            return services;
        }
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly IServiceProvider _services;

        // ?? Dev workflow toggles ?????????????????????????????????????
        private const bool FORCE_CLEAN_DEPLOYMENT = false;
#if ANDROID
        private const bool ARCHIVE_BEFORE_DEPLOY = false;
#else
        private const bool ARCHIVE_BEFORE_DEPLOY = true;
#endif

        private const string SafetyArchivePrefix = "Foodbook_Safety_Archive_Update";
        private const string DbSeededKey = "DbSeeded";
        // Legacy: "DbInitialized" was used for both migration and seeding status
        private const string DbSchemaVersionKey = "DbSchemaVersion";

        private static readonly string[] RequiredTables =
        {
            "Recipes", "Ingredients", "Folders", "RecipeLabels",
            "Plans", "PlannedMeals", "ShoppingListItems",
            "AuthAccounts", "SyncQueue", "SyncStates",
            "SubscriptionOperations",
            "__EFMigrationsHistory"
        };

        private record PrefsDto(string Culture, Foodbook.Models.AppTheme Theme, Foodbook.Models.AppColorTheme ColorTheme,
                                bool ColorfulBackground, bool WallpaperBackground,
                                Foodbook.Models.AppFontFamily FontFamily, Foodbook.Models.AppFontSize FontSize);

        public DatabaseService(IServiceProvider services)
        {
            _services = services;
            Log($"Created. Path: {DatabaseConfiguration.GetDatabasePath()}");
        }

        // ================================================================
        //  Core: shared migrate + validate (DRY � used by all public methods)
        // ================================================================

        /// <summary>
        /// Applies pending migrations and validates schema.
        /// Returns list of missing tables (empty = all OK).
        /// </summary>
        private async Task<List<string>> MigrateAndValidateAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.MigrateAsync().ConfigureAwait(false);

            // Ensure WAL mode is enabled (recommended by Microsoft docs for concurrent read/write)
            try
            {
                await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log($"WARNING: Could not set WAL mode: {ex.Message}");
            }

            return await GetMissingTablesAsync().ConfigureAwait(false);
        }

        private async Task<List<string>> GetMissingTablesAsync()
        {
            var dbPath = DatabaseConfiguration.GetDatabasePath();
            if (!File.Exists(dbPath)) return RequiredTables.ToList();

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var conn = new SqliteConnection(DatabaseConfiguration.GetConnectionString());
                await conn.OpenAsync().ConfigureAwait(false);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                while (await reader.ReadAsync().ConfigureAwait(false))
                    existing.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                Log($"ERROR: Schema validation failed: {ex.Message}");
                return RequiredTables.ToList();
            }

            return RequiredTables.Where(t => !existing.Contains(t)).ToList();
        }

        // ================================================================
        //  PUBLIC: InitializeAsync
        // ================================================================

        public async Task InitializeAsync()
        {
            // Migration handling: 
            // - "DbSeeded" tracks if initial data seeding ran (initially just a flag)
            // - Migrations are tracked by EF Core __EFMigrationsHistory and DbSchemaVersion
            
            var isSeeded = Preferences.Get(DbSeededKey, false);
            
            // Backwards compatibility: if not seeded but "DbInitialized" exists/true, migrate flag
            if (!isSeeded && Preferences.ContainsKey("DbInitialized"))
            {
                if (Preferences.Get("DbInitialized", false))
                {
                    isSeeded = true;
                    Preferences.Set(DbSeededKey, true);
                    Log("Migrated legacy DbInitialized -> DbSeeded");
                }
            }

            var currentSchemaVersion = Preferences.Get(DbSchemaVersionKey, 0);
            Log($"InitializeAsync ENTER (DbSeeded={isSeeded}, DbSchemaVersion={currentSchemaVersion})");

            Log("=== InitializeAsync START ===");

            try
            {
                var dbPath = DatabaseConfiguration.GetDatabasePath();
                var dbExists = File.Exists(dbPath);
                Log($"InitializeAsync DB path: {dbPath} | exists={dbExists}");

                // If the database file already exists � leave it alone, just ensure migrations
                if (dbExists)
                {
                    var fi = new FileInfo(dbPath);
                    Log($"DB exists ({fi.Length} bytes, modified {fi.LastWriteTime}) - applying migrations and schema validation");
                }
                else
                {
                    Log("DB does not exist  will be created by MigrateAsync");
                }

                // Always check pending migrations! (Rule K3.1)
                var missing = await MigrateAndValidateAsync().ConfigureAwait(false);

                if (missing.Count > 0)
                    Log($"ERROR: Schema incomplete after migration. Missing: {string.Join(", ", missing)}");
                else
                    Log("Schema OK  all required tables present");

                // DbSeeded now strictly means "seed/setup completed" (Rule K3.2)
                if (!isSeeded)
                {
                    Preferences.Set(DbSeededKey, true);
                    Log("InitializeAsync SET DbSeeded=true (seed/setup completed)");
                }

                // Persist current schema version based on applied EF migrations count
                using (var scope = _services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var appliedMigrations = await db.Database.GetAppliedMigrationsAsync().ConfigureAwait(false);
                    var newSchemaVersion = appliedMigrations.Count();
                    Preferences.Set(DbSchemaVersionKey, newSchemaVersion);
                    Log($"InitializeAsync SET DbSchemaVersion={newSchemaVersion}");
                }

                Log("=== InitializeAsync COMPLETE ===");
            }
            catch (Exception ex)
            {
                Log($"ERROR: InitializeAsync FAILED: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        // ================================================================
        //  PUBLIC: EnsureDatabaseSchemaAsync / MigrateDatabaseAsync (DRY)
        // ================================================================

        public async Task<bool> EnsureDatabaseSchemaAsync()
        {
            try
            {
                Log("EnsureDatabaseSchemaAsync called");
                var missing = await MigrateAndValidateAsync().ConfigureAwait(false);
                if (missing.Count > 0)
                {
                    Log($"ERROR: Schema validation failed. Missing: {string.Join(", ", missing)}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR: EnsureDatabaseSchemaAsync failed: {ex.Message}");
                return false;
            }
        }

        public Task<bool> MigrateDatabaseAsync() => EnsureDatabaseSchemaAsync();

        // ================================================================
        //  PUBLIC: ResetDatabaseAsync
        // ================================================================

        public async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                Log("WARNING: ResetDatabaseAsync - deleting all data!");
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
                await db.Database.MigrateAsync().ConfigureAwait(false);

                var missing = await GetMissingTablesAsync().ConfigureAwait(false);
                if (missing.Count > 0)
                {
                    Log($"ERROR: Reset failed - missing tables: {string.Join(", ", missing)}");
                    return false;
                }

                Log("Database reset completed");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR: ResetDatabaseAsync failed: {ex.Message}");
                return false;
            }
        }

        // ================================================================
        //  PUBLIC: ConditionalDeploymentAsync
        // ================================================================

        public async Task<bool> ConditionalDeploymentAsync()
        {
            // Rule K3.1: Always check for pending migrations (via InitializeAsync later), 
            // do NOT return early based on a flag like DbInitialized.
            // Removed the early DbInitialized check to ensure deployment safety checks run.

            Log($"=== ConditionalDeploymentAsync START (FORCE_CLEAN={FORCE_CLEAN_DEPLOYMENT}, ARCHIVE={ARCHIVE_BEFORE_DEPLOY}) ===");

            try
            {
                var dbPath = DatabaseConfiguration.GetDatabasePath();
                var dbExists = File.Exists(dbPath);

                // Marker-based restore is only meaningful on first run
                var markerRestored = await CheckDeploymentMarkerAndRestoreAsync(dbPath).ConfigureAwait(false);
                if (markerRestored)
                {
                    Log("Auto-restore from deployment marker completed - skipping normal archive creation");
                    dbExists = File.Exists(dbPath);
                }

                if (ARCHIVE_BEFORE_DEPLOY && dbExists && !markerRestored)
                {
                    try
                    {
                        var archivePath = await CreateSafetyArchiveAsync().ConfigureAwait(false);
                        Log(archivePath != null ? $"Safety archive: {archivePath}" : "No archive created (DB too small or missing)");
                    }
                    catch (Exception ex) { Log($"WARNING: Safety archive failed (continuing): {ex.Message}"); }
                }

                if (FORCE_CLEAN_DEPLOYMENT)
                {
                    Log("WARNING: FORCE_CLEAN_DEPLOYMENT - wiping all app data");
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.EnsureDeletedAsync().ConfigureAwait(false);
                    Preferences.Clear();
                    SecureStorage.RemoveAll();
                    ClearDirectory(FileSystem.AppDataDirectory, preserveDbFiles: true);
                    ClearDirectory(FileSystem.CacheDirectory, preserveDbFiles: false);
                    await db.Database.MigrateAsync().ConfigureAwait(false);
                    Preferences.Set(DbSchemaVersionKey, 0);
                    Log("Fresh database created");
                    return true;
                }

                // Always run InitializeAsync (migrations must run even when DbInitialized=true)
                await InitializeAsync().ConfigureAwait(false);

                Log("=== ConditionalDeploymentAsync COMPLETE ===");
                return true;
            }
            catch (Exception ex)
            {
                Log($"ERROR: ConditionalDeploymentAsync FAILED: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Checks for deployment marker file created by MSBuild pre-build script.
        /// If found: auto-restore from most recent archive, then delete marker.
        /// This protects against VS Android deployment wiping /data/data/.
        /// </summary>
        private async Task<bool> CheckDeploymentMarkerAndRestoreAsync(string dbPath)
        {
            // Marker locations (check both Windows dev machine and Android device)
            var devMarkerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".foodbook_deployment_marker");
            var deviceMarkerPath = Path.Combine(GetSafetyArchiveFolder() ?? FileSystem.CacheDirectory, ".deployment_marker");

            string? markerPath = null;
            if (File.Exists(devMarkerPath))
            {
                markerPath = devMarkerPath;
                Log($"Deployment marker detected (dev machine): {markerPath}");
            }
            else if (File.Exists(deviceMarkerPath))
            {
                markerPath = deviceMarkerPath;
                Log($"Deployment marker detected (device): {markerPath}");
            }

            if (markerPath == null)
            {
                return false; // No marker ? normal flow
            }

            try
            {
                // Read marker metadata
                var markerContent = await File.ReadAllTextAsync(markerPath).ConfigureAwait(false);
                Log($"Marker content: {markerContent}");

                // If DB already exists, deployment didn't wipe it � delete marker and continue
                if (File.Exists(dbPath))
                {
                    Log("DB exists - skipping auto-restore");
                    TryDelete(markerPath);
                    return false;
                }

                // DB was wiped ? restore from archive
                Log("DB was wiped by deployment - attempting auto-restore from safety archive...");
                var archivePath = FindBestArchive();
                if (archivePath == null)
                {
                    Log("WARNING: No safety archive found for restore - user data may be lost!");
                    TryDelete(markerPath);
                    return false;
                }

                // Perform restore
                var dbDir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDir)) Directory.CreateDirectory(dbDir);

                var (walPath, shmPath) = DatabaseConfiguration.GetWalFiles();
                TryDelete(dbPath); TryDelete(walPath); TryDelete(shmPath);

                await Task.Run(() =>
                {
                    using var stream = File.OpenRead(archivePath);
                    using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                    ExtractEntry(zip, "database/foodbookapp.db", dbPath);
                    ExtractEntry(zip, "database/foodbookapp.db-wal", walPath);
                    ExtractEntry(zip, "database/foodbookapp.db-shm", shmPath);
                }).ConfigureAwait(false);

                await RestorePreferencesAsync(archivePath).ConfigureAwait(false);

                var restored = File.Exists(dbPath) && new FileInfo(dbPath).Length > 1024;
                if (restored)
                {
                    Log("? AUTO-RESTORE SUCCESS - user data recovered from safety archive");
                }
                else
                {
                    Log("ERROR: Auto-restore failed - user data may be lost!");
                }

                // Delete marker regardless of restore outcome (avoid infinite retry loop)
                TryDelete(markerPath);
                return restored;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Deployment marker processing failed: {ex.Message}");
                TryDelete(markerPath);
                return false;
            }
        }

        // ================================================================
        //  PRIVATE: Archive restore helpers
        // ================================================================

        private static void ExtractEntry(ZipArchive zip, string entryName, string targetPath)
        {
            var entry = zip.GetEntry(entryName);
            if (entry == null) return;
            try { using var s = entry.Open(); using var f = File.Create(targetPath); s.CopyTo(f); } catch { }
        }

        private async Task RestorePreferencesAsync(string archivePath)
        {
            try
            {
                string? json = null;
                await Task.Run(() =>
                {
                    using var s = File.OpenRead(archivePath);
                    using var zip = new ZipArchive(s, ZipArchiveMode.Read);
                    var e = zip.GetEntry("prefs.json");
                    if (e != null) { using var r = new StreamReader(e.Open()); json = r.ReadToEnd(); }
                }).ConfigureAwait(false);
                if (string.IsNullOrEmpty(json)) return;
                var prefs = _services.GetService<IPreferencesService>();
                if (prefs == null) return;
                var dto = System.Text.Json.JsonSerializer.Deserialize<PrefsDto>(json);
                if (dto == null) return;
                prefs.SaveLanguage(dto.Culture);
                prefs.SaveTheme(dto.Theme);
                prefs.SaveColorTheme(dto.ColorTheme);
                prefs.SaveColorfulBackground(dto.ColorfulBackground);
                prefs.SaveWallpaperEnabled(dto.WallpaperBackground);
                prefs.SaveFontFamily(dto.FontFamily);
                prefs.SaveFontSize(dto.FontSize);
            }
            catch (Exception ex) { Log($"WARN: Prefs restore: {ex.Message}"); }
        }

        // ================================================================
        //  PRIVATE: Archive search & management
        // ================================================================

        private string? FindBestArchive()
        {
            FileInfo? best = null;
            foreach (var folder in GetArchiveCandidateFolders())
            {
                if (!Directory.Exists(folder)) continue;
                try
                {
                    foreach (var file in Directory.GetFiles(folder, "*.fbk", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            if (fi.Length < 1024) continue;  // Fixed: use literal instead of MinArchiveSize constant
                            if (!ArchiveHasDatabase(file)) continue;
                            if (best == null || fi.LastWriteTimeUtc > best.LastWriteTimeUtc) best = fi;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (best != null)
                Log($"Best archive found: {best.Name} ({best.Length} bytes, modified {best.LastWriteTime})");
            else
                Log("No valid archives found in any candidate folder");

            return best?.FullName;
        }

        // ================================================================
        //  PUBLIC: CreateSafetyArchiveAsync (archive code � untouched logic)
        // ================================================================

        public async Task<string?> CreateSafetyArchiveAsync()
        {
            try
            {
                var dbPath = DatabaseConfiguration.GetDatabasePath();
                if (!File.Exists(dbPath)) return null;

                var dbInfo = new FileInfo(dbPath);
                if (dbInfo.Length < 1024)
                {
                    Log("DB too small for archive - skipping");
                    return null;
                }

                var targetFolder = GetSafetyArchiveFolder();
                if (string.IsNullOrEmpty(targetFolder)) return null;

                try { Directory.CreateDirectory(targetFolder); }
                catch
                {
                    targetFolder = Path.Combine(FileSystem.CacheDirectory, "SafetyArchives");
                    Directory.CreateDirectory(targetFolder);
                }

                var todayPrefix = DateTime.Now.ToString("yyyyMMdd");
                var existingArchives = GetExistingSafetyArchives(targetFolder);
                var todaysArchive = existingArchives.FirstOrDefault(f => f.Name.Contains($"{SafetyArchivePrefix}_{todayPrefix}"));
                bool isUpdate = todaysArchive != null;

                string archivePath = isUpdate
                    ? todaysArchive!.FullName
                    : Path.Combine(targetFolder, $"{SafetyArchivePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.fbk");

                if (!isUpdate) CleanupOldSafetyArchivesSync(targetFolder, 4);

                try { await FlushWalForBackupAsync(dbPath).ConfigureAwait(false); }
                catch (Exception ex) { Log($"WARNING: WAL flush: {ex.Message}"); }

                string? prefsExportPath = null;
                try
                {
                    var prefsService = _services.GetService<IPreferencesService>();
                    if (prefsService != null)
                    {
                        prefsExportPath = Path.Combine(FileSystem.CacheDirectory, $"prefs_safety_{Guid.NewGuid():N}.json");
                        var prefsDto = new PrefsDto(
                            prefsService.GetSavedLanguage(), prefsService.GetSavedTheme(),
                            prefsService.GetSavedColorTheme(), prefsService.GetIsColorfulBackgroundEnabled(),
                            prefsService.GetIsWallpaperEnabled(), prefsService.GetSavedFontFamily(),
                            prefsService.GetSavedFontSize());
                        var json = System.Text.Json.JsonSerializer.Serialize(prefsDto);
                        await File.WriteAllTextAsync(prefsExportPath, json).ConfigureAwait(false);
                    }
                }
                catch { prefsExportPath = null; }

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
                        if (File.Exists(shmPath)) try { zip.CreateEntryFromFile(shmPath, "database/foodbookapp.db-shm"); } catch { }
                        if (File.Exists(walPath)) try { zip.CreateEntryFromFile(walPath, "database/foodbookapp.db-wal"); } catch { }

                        if (!string.IsNullOrEmpty(prefsExportPath) && File.Exists(prefsExportPath))
                            try { zip.CreateEntryFromFile(prefsExportPath, "prefs.json"); } catch { }

                        var metadataEntry = zip.CreateEntry("metadata.json");
                        using var writer = new StreamWriter(metadataEntry.Open());
                        writer.Write(System.Text.Json.JsonSerializer.Serialize(new
                        {
                            CreatedAt = DateTime.Now.ToString("O"),
                            DatabaseSize = dbInfo.Length,
                            DatabaseLastModified = dbInfo.LastWriteTime.ToString("O"),
                            Type = "SafetyArchive", Version = "1.0", IsUpdate = isUpdate
                        }));
                    }).ConfigureAwait(false);

                    success = true;
                    TryDelete(prefsExportPath);
                    Log($"Archive {(isUpdate ? "updated" : "created")}: {archivePath}");
                    return archivePath;
                }
                catch (Exception ex)
                {
                    Log($"ERROR: Archive creation failed: {ex.Message}");
                    TryDelete(prefsExportPath);
                    if (!isUpdate && !success) TryDelete(archivePath);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log($"ERROR: Safety archive FAILED: {ex.Message}");
                return null;
            }
        }

        // ================================================================
        //  PRIVATE: Archive helpers (untouched)
        // ================================================================

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
                foreach (var file in GetExistingSafetyArchives(folder).Skip(keepCount))
                    try { file.Delete(); } catch { }
            }
            catch { }
        }

        private static bool ArchiveHasDatabase(string path)
        {
            try
            {
                using var s = File.OpenRead(path);
                using var zip = new ZipArchive(s, ZipArchiveMode.Read);
                var e = zip.GetEntry("database/foodbookapp.db");
                return e != null && e.Length > 1024;
            }
            catch { return false; }
        }

        private static string? GetSafetyArchiveFolder()
        {
#if ANDROID
            try
            {
                var dl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (!string.IsNullOrWhiteSpace(dl)) return Path.Combine(dl, "Foodbook");
            }
            catch { }
            return Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives");
#elif WINDOWS
            try { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Foodbook"); } catch { return null; }
#else
            return null;
#endif
        }

        private static List<string> GetArchiveCandidateFolders()
        {
            var folders = new List<string>();
#if ANDROID
            try
            {
                var dl = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (!string.IsNullOrWhiteSpace(dl)) { folders.Add(Path.Combine(dl, "Foodbook")); folders.Add(dl); }
                foreach (var root in new[] { "/storage/emulated/0", "/sdcard", "/storage/self/primary" })
                { folders.Add(Path.Combine(root, "Download", "Foodbook")); folders.Add(Path.Combine(root, "Downloads", "Foodbook")); }
            }
            catch { }
#elif WINDOWS
            try { var d = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); if (!string.IsNullOrEmpty(d)) folders.Add(Path.Combine(d, "Foodbook")); } catch { }
#endif
            // Also check AppDataDirectory fallback (may not survive deploy but worth trying)
            try { folders.Add(Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives")); } catch { }

            return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
                Log($"WARNING: WAL flush: {ex.Message}");
            }
        }

        // ================================================================
        //  PRIVATE: Utility
        // ================================================================

        private static void ClearDirectory(string path, bool preserveDbFiles)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (preserveDbFiles && (file.EndsWith(".db") || file.EndsWith(".db-shm") || file.EndsWith(".db-wal")))
                        continue;
                    try { File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        try { Directory.Delete(dir); } catch { }
                }
            }
            catch { }
        }

        private static void TryDelete(string? path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                try { File.Delete(path); } catch { }
        }

        private static void Log(string msg) =>
            System.Diagnostics.Debug.WriteLine($"[DatabaseService] {msg}");
    }
}
