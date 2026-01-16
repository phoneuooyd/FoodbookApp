using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using FoodbookApp.Interfaces;
using CommunityToolkit.Maui.Storage;
using Foodbook.Data;
using Foodbook.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Foodbook.Services.Archive;

namespace Foodbook.Views
{
    public partial class DataArchivizationPage : ContentPage
    {
        private readonly IDatabaseService _dbService;
        private readonly IPreferencesService _prefs;

        private bool _isExporting;

        private static readonly string[] ArchiveExtensions = new[] { ".fbk", ".zip" };

        private record ArchiveItem(string FileName, string FullPath, DateTime ModifiedUtc, long Length)
        {
            public string ModifiedDisplay => $"{ModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        private record PrefsDto(string Culture, Foodbook.Models.AppTheme Theme, Foodbook.Models.AppColorTheme ColorTheme,
                                bool ColorfulBackground, bool WallpaperBackground,
                                Foodbook.Models.AppFontFamily FontFamily, Foodbook.Models.AppFontSize FontSize);

        /// <summary>
        /// Context for tracking ID mappings and folder hierarchy during restore from legacy archives
        /// </summary>
        private class RestoreContext
        {
            /// <summary>
            /// Maps old integer folder IDs to new GUIDs (legacy archives only)
            /// Key: old integer ID from legacy archive, Value: new GUID in current database
            /// </summary>
            public Dictionary<int, Guid> FolderIdMapping { get; } = new();

            /// <summary>
            /// Maps source GUID folder IDs to target GUIDs (for GUID-based archives)
            /// Key: source GUID, Value: target GUID (may be same if preserved)
            /// </summary>
            public Dictionary<Guid, Guid> FolderGuidMapping { get; } = new();

            /// <summary>
            /// Folder parent-child relationships from source archive (GUID-based)
            /// Key: source folder GUID, Value: source parent folder GUID (null if root)
            /// </summary>
            public Dictionary<Guid, Guid?> FolderGuidParentMap { get; } = new();

            /// <summary>
            /// Maps old integer recipe IDs to new GUIDs
            /// </summary>
            public Dictionary<int, Guid> RecipeIdMapping { get; } = new();

            /// <summary>
            /// Maps source GUID recipe IDs to target GUIDs (for GUID-based archives)
            /// </summary>
            public Dictionary<Guid, Guid> RecipeGuidMapping { get; } = new();

            /// <summary>
            /// Recipe to folder assignments from source archive (GUID-based)
            /// Key: source recipe GUID, Value: source folder GUID (null if not in folder)
            /// </summary>
            public Dictionary<Guid, Guid?> RecipeGuidFolderMap { get; } = new();

            /// <summary>
            /// Maps old integer label IDs to new GUIDs
            /// </summary>
            public Dictionary<int, Guid> LabelIdMapping { get; } = new();

            /// <summary>
            /// Maps old integer ingredient IDs to new GUIDs
            /// </summary>
            public Dictionary<int, Guid> IngredientIdMapping { get; } = new();

            /// <summary>
            /// Maps old integer plan IDs to new GUIDs
            /// </summary>
            public Dictionary<int, Guid> PlanIdMapping { get; } = new();

            /// <summary>
            /// Folder parent-child relationships from legacy archive
            /// Key: old folder ID, Value: old parent folder ID (null if root)
            /// </summary>
            public Dictionary<int, int?> FolderParentMap { get; } = new();

            /// <summary>
            /// Recipe to folder assignments from legacy archive
            /// Key: old recipe ID, Value: old folder ID (null if not in folder)
            /// </summary>
            public Dictionary<int, int?> RecipeFolderMap { get; } = new();

            /// <summary>
            /// Ingredient to recipe assignments from legacy archive
            /// Key: old ingredient ID, Value: old recipe ID (null if standalone)
            /// </summary>
            public Dictionary<int, int?> IngredientRecipeMap { get; } = new();

            /// <summary>
            /// PlannedMeal to recipe and plan assignments
            /// Key: old planned meal ID, Value: (old recipe ID, old plan ID)
            /// </summary>
            public Dictionary<int, (int RecipeId, int? PlanId)> PlannedMealMap { get; } = new();

            /// <summary>
            /// Recipe-Label relationships
            /// List of (old recipe ID, old label ID) pairs
            /// </summary>
            public List<(int RecipeId, int LabelId)> RecipeLabelLinks { get; } = new();

            /// <summary>
            /// ShoppingListItem to plan assignments
            /// Key: old item ID, Value: old plan ID
            /// </summary>
            public Dictionary<int, int?> ShoppingItemPlanMap { get; } = new();

            /// <summary>
            /// Whether the source archive uses GUID IDs (vs legacy integer IDs)
            /// </summary>
            public bool IsGuidBasedArchive { get; set; } = false;

            public void LogMappings(Action<string> log)
            {
                log($"Restore context summary:");
                if (IsGuidBasedArchive)
                {
                    log($"  - Archive type: GUID-based");
                    log($"  - Folders: {FolderGuidMapping.Count} mapped, {FolderGuidParentMap.Count} parent relationships");
                    log($"  - Recipes: {RecipeGuidMapping.Count} mapped, {RecipeGuidFolderMap.Count} folder assignments");
                }
                else
                {
                    log($"  - Archive type: Legacy (integer IDs)");
                    log($"  - Folders: {FolderIdMapping.Count} mapped, {FolderParentMap.Count} parent relationships");
                    log($"  - Recipes: {RecipeIdMapping.Count} mapped, {RecipeFolderMap.Count} folder assignments");
                }
                log($"  - Labels: {LabelIdMapping.Count} mapped, {RecipeLabelLinks.Count} recipe-label links");
                log($"  - Ingredients: {IngredientIdMapping.Count} mapped");
                log($"  - Plans: {PlanIdMapping.Count} mapped");
            }
        }

        public DataArchivizationPage(IDatabaseService dbService, IPreferencesService prefs)
        {
            InitializeComponent();
            _dbService = dbService;
            _prefs = prefs;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
#if ANDROID
            await EnsureLegacyStoragePermissionsAsync();
#endif
            DefaultFolderPathLabel.Text = GetDefaultArchiveFolder();

            // Ensure the manual search button has a visible caption even if localization key is missing
            try
            {
                var caption = GetLocalizedText("DataArchivizationPageResources", "SearchArchivesButton", "Search device files");
                var manualBtn = this.FindByName<Button>("ManualSearchButton");
                if (manualBtn != null && string.IsNullOrWhiteSpace(manualBtn.Text))
                {
                    manualBtn.Text = caption;
                    manualBtn.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
                    manualBtn.TextColor = Colors.White;
                }
            }
            catch { }

            LoadArchivesList();
        }

        private void OnManualSearchButtonLoaded(object? sender, EventArgs e)
        {
            try
            {
                if (sender is Button b)
                {
                    if (string.IsNullOrWhiteSpace(b.Text))
                        b.Text = GetLocalizedText("DataArchivizationPageResources", "SearchArchivesButton", "Search device files");
                    // Enforce primary styling in case default style overrides it
                    b.BackgroundColor = (Color)Application.Current!.Resources["Primary"];
                    b.TextColor = Colors.White;
                }
            }
            catch { }
        }

        private string GetDefaultArchiveFolder()
        {
#if ANDROID
            var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath ?? FileSystem.AppDataDirectory;
            var folder = Path.Combine(downloads, "Foodbook");
            try { Directory.CreateDirectory(folder); } catch { /* ignore scoped storage failures; fallback used on save */ }
            return folder;
#elif WINDOWS
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var folder = Path.Combine(docs, "Foodbook");
            Directory.CreateDirectory(folder);
            return folder;
#else
            var folder = Path.Combine(FileSystem.AppDataDirectory, "FoodbookArchives");
            Directory.CreateDirectory(folder);
            return folder;
#endif
        }

#if ANDROID
        private static IEnumerable<string> GetAndroidCandidateFolders()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // Primary way: system downloads path
                var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (!string.IsNullOrWhiteSpace(downloads))
                {
                    result.Add(downloads);
                    var fb = Path.Combine(downloads, "Foodbook");
                    result.Add(fb);
                }

                // Some devices/localizations may use alternative casing or alias; include common variants
                var storageRoot = "/storage/emulated/0";
                var altDownloads = Path.Combine(storageRoot, "Download");
                result.Add(altDownloads);
                result.Add(Path.Combine(altDownloads, "Foodbook"));

                var roots = new List<string?>
                {
                    Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath, // often /storage/emulated/0
                    "/sdcard",
                    "/storage/self/primary",
                    "/storage/emulated/0"
                };

                foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    var d1 = Path.Combine(root!, "Download");
                    var d2 = Path.Combine(root!, "Downloads");
                    result.Add(d1);
                    result.Add(Path.Combine(d1, "Foodbook"));
                    result.Add(d2);
                    result.Add(Path.Combine(d2, "Foodbook"));
                }
            }
            catch { }
            return result.Where(Directory.Exists);
        }
#endif

        private static bool IsArchivePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try { return ArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static ArchiveItem CreateItem(string p)
        {
            var fi = new FileInfo(p);
            var modifiedUtc = fi.Exists ? fi.LastWriteTimeUtc : File.GetLastWriteTimeUtc(p);
            var len = fi.Exists ? fi.Length : 0L;
            return new ArchiveItem(Path.GetFileName(p), p, modifiedUtc, len);
        }

        private void LoadArchivesList()
        {
            if (_isExporting)
            {
                // Avoid listing partially created files during export
                return;
            }

            try
            {
                List<ArchiveItem> items;
#if ANDROID
                var folders = GetAndroidCandidateFolders();
                items = new List<ArchiveItem>();
                foreach (var folder in folders)
                {
                    try
                    {
                        var fbk = Directory.EnumerateFiles(folder, "*.fbk", SearchOption.TopDirectoryOnly).Select(CreateItem);
                        var zip = Directory.EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly).Select(CreateItem);
                        items.AddRange(fbk.Concat(zip));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Listing failed in {folder}: {ex.Message}");
                    }
                }
#else
                var folder = GetDefaultArchiveFolder();
                items = Directory.Exists(folder)
                    ? Directory.EnumerateFiles(folder, "*.fbk", SearchOption.TopDirectoryOnly)
                        .Concat(Directory.EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly))
                        .Select(CreateItem)
                        .ToList()
                    : new List<ArchiveItem>();
#endif
                // Deduplicate across alias paths (same physical file visible under different roots)
                // Group by FileName + ModifiedUtc + Length to keep a single entry per actual file
                var list = items
                    .GroupBy(i => $"{i.FileName.ToUpperInvariant()}|{i.ModifiedUtc.Ticks}|{i.Length}")
                    .Select(g => g.First())
                    .OrderByDescending(i => i.ModifiedUtc)
                    .ToList();

                ArchivesCollection.ItemsSource = list;
            }
            catch (Exception ex)
            {
                StatusLabel.Text = string.Format(GetLocalizedText("DataArchivizationPageResources", "ErrorLoadingList", "Error loading list: {0}"), ex.Message);
            }
        }

        private async void OnRefreshListClicked(object sender, EventArgs e)
        {
#if ANDROID
            await EnsureLegacyStoragePermissionsAsync();
#endif
            LoadArchivesList();
            await Task.CompletedTask;
        }

        private async void OnManualSearchClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = GetLocalizedText("DataArchivizationPageResources", "PickArchiveTitle", "Pick archive (.fbk/.zip)"),
                    FileTypes = GetArchivePickerFileType()
                });

                if (result == null) return;

                var picked = result
                    .Where(f => IsArchivePath(f.FullPath))
                    .Select(f => f.FullPath!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (picked.Count == 0)
                {
                    // Some Android pickers may not honor filters; allow picking any and filter manually
                    var any = await FilePicker.PickMultipleAsync();
                    if (any != null)
                    {
                        picked = any.Where(f => IsArchivePath(f.FullPath)).Select(f => f.FullPath!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                }

                if (picked.Count == 0) return;

                var current = (ArchivesCollection.ItemsSource as IEnumerable<ArchiveItem>)?.ToList() ?? new List<ArchiveItem>();
                foreach (var p in picked)
                {
                    try
                    {
                        var fi = new FileInfo(p);
                        var item = new ArchiveItem(Path.GetFileName(p), p, fi.Exists ? fi.LastWriteTimeUtc : File.GetLastWriteTimeUtc(p), fi.Exists ? fi.Length : 0L);
                        // Remove all possible aliases for the same physical file
                        current.RemoveAll(x => string.Equals(x.FileName, item.FileName, StringComparison.OrdinalIgnoreCase)
                                               && x.ModifiedUtc == item.ModifiedUtc
                                               && x.Length == item.Length);
                        current.Add(item);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Manual add failed for {p}: {ex.Message}");
                    }
                }

                ArchivesCollection.ItemsSource = current
                    .GroupBy(i => $"{i.FileName.ToUpperInvariant()}|{i.ModifiedUtc.Ticks}|{i.Length}")
                    .Select(g => g.First())
                    .OrderByDescending(i => i.ModifiedUtc)
                    .ToList();

                StatusLabel.Text = GetLocalizedText("DataArchivizationPageResources", "ManualSearchDone", "Selected archives added to the list.");
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    GetLocalizedText("DataArchivizationPageResources", "ErrorTitle", "Error"),
                    string.Format(GetLocalizedText("DataArchivizationPageResources", "ManualSearchFailed", "Manual search failed: {0}"), ex.Message),
                    GetLocalizedText("ButtonResources", "OK", "OK"));
            }
        }

#if ANDROID
        private static async Task EnsureLegacyStoragePermissionsAsync()
        {
            try
            {
                // For Android 10 (API 29) and below, request legacy storage permissions
                if (DeviceInfo.Version.Major <= 10)
                {
                    var read = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
                    if (read != PermissionStatus.Granted)
                        await Permissions.RequestAsync<Permissions.StorageRead>();

                    var write = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                    if (write != PermissionStatus.Granted)
                        await Permissions.RequestAsync<Permissions.StorageWrite>();
                }
            }
            catch { }
        }
#endif

        private static FilePickerFileType GetArchivePickerFileType()
        {
            // Single Android mapping; other platforms use UTTypes/extensions
            return new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new [] { "application/zip", "application/octet-stream", "application/x-zip-compressed", "application/x-fbk" } },
                { DevicePlatform.iOS, new [] { "com.pkware.zip-archive", "public.zip-archive" } },
                { DevicePlatform.MacCatalyst, new [] { "com.pkware.zip-archive", "public.zip-archive" } },
                { DevicePlatform.WinUI, new [] { ".fbk", ".zip" } },
                { DevicePlatform.Tizen, new [] { "*/*" } }
            });
        }

        private async Task FlushSqliteWalAsync(string dbPath)
        {
            const int MaxRetries = 3;
            const int RetryDelayMs = 200;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] WAL checkpoint attempt {attempt}/{MaxRetries}");
                    
                    // First: Dispose all EF Core contexts to release database connections
                    // This is critical - EF Core may hold connections that prevent WAL flush
                    using var scope = FoodbookApp.MauiProgram.ServiceProvider!.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    
                    // Force EF Core to save any pending changes
                    await context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine("[Archive] EF Core changes saved");
                    
                    // Dispose the context to release connection
                    await context.DisposeAsync();
                    System.Diagnostics.Debug.WriteLine("[Archive] EF Core context disposed");
                    
                    // Small delay to ensure connection is fully released
                    await Task.Delay(100);

                    // Second: Direct WAL checkpoint via new connection
                    var cs = new SqliteConnectionStringBuilder 
                    { 
                        DataSource = dbPath, 
                        Mode = SqliteOpenMode.ReadWrite, 
                        Cache = SqliteCacheMode.Shared 
                    }.ToString();
                    
                    using var conn = new SqliteConnection(cs);
                    await conn.OpenAsync();
                    System.Diagnostics.Debug.WriteLine("[Archive] Direct SQLite connection opened");
                    
                    using var cmd = conn.CreateCommand();
                    // TRUNCATE mode: checkpoint and truncate WAL file
                    cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                    var result = await cmd.ExecuteScalarAsync();
                    System.Diagnostics.Debug.WriteLine($"[Archive] WAL checkpoint result: {result}");
                    
                    // Optimize database
                    cmd.CommandText = "PRAGMA optimize;";
                    await cmd.ExecuteNonQueryAsync();
                    System.Diagnostics.Debug.WriteLine("[Archive] Database optimized");
                    
                    return; // Success
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] WAL checkpoint attempt {attempt} failed: {ex.Message}");
                    if (attempt < MaxRetries)
                    {
                        await Task.Delay(RetryDelayMs);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Archive] WAL checkpoint failed after {MaxRetries} attempts: {ex.Message}");
                    }
                }
            }
        }

        private async void OnExportClicked(object sender, EventArgs e)
        {
            if (_isExporting) return;
            var createBtn = this.FindByName<Button>("CreateArchiveButton");
            try
            {
                _isExporting = true;
                if (createBtn != null) createBtn.IsEnabled = false;

                System.Diagnostics.Debug.WriteLine("[Archive] === EXPORT STARTED ===");

                await _dbService.EnsureDatabaseSchemaAsync();

                // USE CENTRALIZED DATABASE PATH - critical fix!
                var dbPath = DatabaseConfiguration.GetDatabasePath();
                System.Diagnostics.Debug.WriteLine($"[Archive] Database path: {dbPath}");
                System.Diagnostics.Debug.WriteLine($"[Archive] Database exists: {File.Exists(dbPath)}");
                
                if (File.Exists(dbPath))
                {
                    var dbInfo = new FileInfo(dbPath);
                    System.Diagnostics.Debug.WriteLine($"[Archive] Database size: {dbInfo.Length} bytes, Last modified: {dbInfo.LastWriteTime}");
                }

                // Flush WAL to ensure all recent changes are in the main DB file
                System.Diagnostics.Debug.WriteLine("[Archive] Flushing WAL...");
                await FlushSqliteWalAsync(dbPath);
                System.Diagnostics.Debug.WriteLine("[Archive] WAL flush completed");

                // Verify database state after WAL flush
                if (File.Exists(dbPath))
                {
                    var dbInfo = new FileInfo(dbPath);
                    System.Diagnostics.Debug.WriteLine($"[Archive] Database size after WAL flush: {dbInfo.Length} bytes");
                }

                var prefsExport = Path.Combine(FileSystem.CacheDirectory, "prefs_export.json");
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Culture = _prefs.GetSavedLanguage(),
                    Theme = _prefs.GetSavedTheme(),
                    ColorTheme = _prefs.GetSavedColorTheme(),
                    ColorfulBackground = _prefs.GetIsColorfulBackgroundEnabled(),
                    WallpaperBackground = _prefs.GetIsWallpaperEnabled(),
                    FontFamily = _prefs.GetSavedFontFamily(),
                    FontSize = _prefs.GetSavedFontSize()
                });
                await File.WriteAllTextAsync(prefsExport, json);
                System.Diagnostics.Debug.WriteLine("[Archive] Preferences exported");

                // Target name
                var targetDir = GetDefaultArchiveFolder();
                var customNameRaw = ArchiveNameEntry.Text?.Trim();
                var safeName = string.IsNullOrWhiteSpace(customNameRaw)
                    ? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : SanitizeFileName(customNameRaw);
                var outName = safeName.EndsWith(".fbk", StringComparison.OrdinalIgnoreCase) ? safeName : safeName + ".fbk";

                // Create ZIP in a temporary file on background thread to avoid blocking UI
                var tempZip = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.fbk");

                System.Diagnostics.Debug.WriteLine("[Archive] Creating ZIP archive...");
                await Task.Run(() =>
                {
                    try
                    {
                        using var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create);
                        
                        // Add main database file
                        if (File.Exists(dbPath))
                        {
                            zip.CreateEntryFromFile(dbPath, "database/foodbookapp.db");
                            System.Diagnostics.Debug.WriteLine($"[Archive] Added database: {new FileInfo(dbPath).Length} bytes");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[Archive] WARNING: Database file not found!");
                        }
                        
                        // Add WAL/SHM files if they exist (for safety, though they should be flushed)
                        var (walPath, shmPath) = DatabaseConfiguration.GetWalFiles();
                        if (File.Exists(shmPath))
                        {
                            zip.CreateEntryFromFile(shmPath, "database/foodbookapp.db-shm");
                            System.Diagnostics.Debug.WriteLine($"[Archive] Added SHM file: {new FileInfo(shmPath).Length} bytes");
                        }
                        if (File.Exists(walPath))
                        {
                            zip.CreateEntryFromFile(walPath, "database/foodbookapp.db-wal");
                            System.Diagnostics.Debug.WriteLine($"[Archive] Added WAL file: {new FileInfo(walPath).Length} bytes");
                        }
                        
                        // Add preferences
                        zip.CreateEntryFromFile(prefsExport, "prefs.json");
                        System.Diagnostics.Debug.WriteLine("[Archive] Added preferences");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Archive] Error creating zip in background: {ex.Message}");
                        throw;
                    }
                });

                System.Diagnostics.Debug.WriteLine($"[Archive] ZIP created: {new FileInfo(tempZip).Length} bytes");

                bool saved = false;
                string? outPath = null;

                // Try direct save to default persistent folder (background IO)
                try
                {
#if ANDROID
                    await EnsureLegacyStoragePermissionsAsync();
#endif
                    Directory.CreateDirectory(targetDir);
                    outPath = Path.Combine(targetDir, outName);
                    if (File.Exists(outPath))
                    {
                        outName = $"{Path.GetFileNameWithoutExtension(outName)}_{DateTime.Now:HHmmss}.fbk";
                        outPath = Path.Combine(targetDir, outName);
                    }

                    // Do file copy on background thread
                    await Task.Run(() => File.Copy(tempZip, outPath, overwrite: false));
                    saved = true;
                    System.Diagnostics.Debug.WriteLine($"[Archive] Saved to: {outPath}");
                }
                catch (Exception directEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Archive] Direct save failed, will use picker: {directEx.Message}");
                    saved = false;
                }

                // Fallback to file saver picker (must be called on UI thread)
                if (!saved)
                {
                    await using var stream = File.OpenRead(tempZip);
                    var result = await FileSaver.Default.SaveAsync(outName, stream, default);
                    if (!result.IsSuccessful)
                    {
                        throw result.Exception ?? new InvalidOperationException("Saving canceled or failed");
                    }
                    outPath = result.FilePath;
                    saved = true;
                    System.Diagnostics.Debug.WriteLine($"[Archive] Saved via picker to: {outPath}");
                }

                // Cleanup temp
                try { File.Delete(tempZip); } catch { }

                StatusLabel.Text = string.Format(GetLocalizedText("DataArchivizationPageResources", "SavedMessage", "Saved: {0}"), outName);

                System.Diagnostics.Debug.WriteLine("[Archive] === EXPORT COMPLETED SUCCESSFULLY ===");

                // Refresh list now that export is complete
                LoadArchivesList();

                var infoTitle = GetLocalizedText("DataArchivizationPageResources", "InfoTitle", "Info") ?? "Info";
                var okText = GetLocalizedText("ButtonResources", "OK", "OK") ?? "OK";
                var createdSuffix = GetLocalizedText("DataArchivizationPageResources", "ArchiveCreatedSuffix", "created successfully") ?? "created successfully";

                // Ensure alerts run on UI thread and button texts are non-null/non-empty
                var infoTitleSafe = string.IsNullOrWhiteSpace(infoTitle) ? "Info" : infoTitle;
                var okTextSafe = string.IsNullOrWhiteSpace(okText) ? "OK" : okText;
                var createdSuffixSafe = string.IsNullOrWhiteSpace(createdSuffix) ? "created successfully" : createdSuffix;

                await MainThread.InvokeOnMainThreadAsync(() => Application.Current?.MainPage?.DisplayAlert(infoTitleSafe, $"{outName} {createdSuffixSafe}", okTextSafe));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Archive] === EXPORT FAILED ===");
                System.Diagnostics.Debug.WriteLine($"[Archive] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Archive] Stack trace: {ex.StackTrace}");
                
                var errorTitle = GetLocalizedText("DataArchivizationPageResources", "ErrorTitle", "Error") ?? "Error";
                var ok = GetLocalizedText("ButtonResources", "OK", "OK") ?? "OK";
                var msg = string.Format(GetLocalizedText("DataArchivizationPageResources", "ExportFailed", "Export failed: {0}"), ex.Message);
                var errorTitleSafe = string.IsNullOrWhiteSpace(errorTitle) ? "Error" : errorTitle;
                var okSafe = string.IsNullOrWhiteSpace(ok) ? "OK" : ok;
                await MainThread.InvokeOnMainThreadAsync(() => Application.Current?.MainPage?.DisplayAlert(errorTitleSafe, msg, okSafe));
            }
            finally
            {
                _isExporting = false;
                if (createBtn != null) createBtn.IsEnabled = true;
            }
        }

        private string GetLocalizedText(string resource, string key, string defaultText)
        {
            try
            {
                var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService(typeof(FoodbookApp.Interfaces.ILocalizationService)) as FoodbookApp.Interfaces.ILocalizationService;
                return loc?.GetString(resource, key) ?? defaultText;
            }
            catch { return defaultText; }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim().TrimEnd('.');
        }

        private async void OnImportClicked(object sender, EventArgs e)
        {
            try
            {
                var folder = GetDefaultArchiveFolder();
                var latest = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.fbk").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
                if (latest == null)
                {
                    await DisplayAlert(
                        GetLocalizedText("DataArchivizationPageResources", "InfoTitle", "Info"),
                        GetLocalizedText("DataArchivizationPageResources", "NoArchivesInDefault", "No archives in default folder. Please pick a file."),
                        GetLocalizedText("ButtonResources", "OK", "OK"));
                    var result = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = GetLocalizedText("DataArchivizationPageResources", "PickArchiveTitle", "Pick archive (.fbk)"),
                        FileTypes = GetArchivePickerFileType()
                    });
                    if (result?.FullPath is string picked)
                    {
                        await RestoreFromPathAsync(picked);
                    }
                    return;
                }
                await RestoreFromPathAsync(latest);
            }
            catch (Exception ex)
            {
                await DisplayAlert(
                    GetLocalizedText("DataArchivizationPageResources", "ErrorTitle", "Error"),
                    string.Format(GetLocalizedText("DataArchivizationPageResources", "ImportFailed", "Import failed: {0}"), ex.Message),
                    GetLocalizedText("ButtonResources", "OK", "OK"));
            }
        }

        private async void OnRestoreFromListClicked(object sender, EventArgs e)
        {
            if (sender is Button b && b.CommandParameter is string path && File.Exists(path))
            {
                await RestoreFromPathAsync(path);
            }
        }

        private async void OnDeleteFromListClicked(object sender, EventArgs e)
        {
            if (sender is Button b && b.CommandParameter is string path && File.Exists(path))
            {
                var fileName = Path.GetFileName(path);

                // Localized labels with safe fallbacks to avoid null/empty exceptions
                var accept = GetLocalizedText("ButtonResources", "Yes", "Yes");
                if (string.IsNullOrWhiteSpace(accept)) accept = "Yes";
                var cancel = GetLocalizedText("ButtonResources", "No", "No");
                if (string.IsNullOrWhiteSpace(cancel)) cancel = "No";

                var confirm = await DisplayAlert(
                    GetLocalizedText("DataArchivizationPageResources", "ConfirmTitle", "Confirm"),
                    string.Format(GetLocalizedText("DataArchivizationPageResources", "ConfirmDelete", "Delete {0}?"), fileName),
                    accept,
                    cancel);

                if (confirm)
                {
                    try
                    {
                        File.Delete(path);
                        StatusLabel.Text = string.Format(GetLocalizedText("DataArchivizationPageResources", "DeletedMessage", "Deleted: {0}"), fileName);
                        LoadArchivesList();
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert(
                            GetLocalizedText("DataArchivizationPageResources", "ErrorTitle", "Error"),
                            string.Format(GetLocalizedText("DataArchivizationPageResources", "DeleteFailed", "Delete failed: {0}"), ex.Message),
                            GetLocalizedText("ButtonResources", "OK", "OK"));
                    }
                }
            }
        }

        private async Task RestoreFromPathAsync(string archivePath)
        {
            var logFolder = GetDefaultArchiveFolder();
            Directory.CreateDirectory(logFolder);
            var logPath = Path.Combine(logFolder, $"restore_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            
            void Log(string msg)
            {
                var logMsg = $"[{DateTime.Now:HH:mm:ss}] {msg}";
                System.Diagnostics.Debug.WriteLine($"[Restore] {msg}");
                try { File.AppendAllText(logPath, logMsg + "\n"); } catch { }
            }

            try
            {
                Log($"=== START RESTORE: {Path.GetFileName(archivePath)} ===");

                // 1. Extract archive
                var tmpDir = Path.Combine(FileSystem.CacheDirectory, "fbk_import");
                if (Directory.Exists(tmpDir)) 
                {
                    Directory.Delete(tmpDir, true);
                    Log("Cleaned temporary directory");
                }
                Directory.CreateDirectory(tmpDir);

                using (var stream = File.OpenRead(archivePath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.ExtractToDirectory(tmpDir, overwriteFiles: true);
                    Log($"Archive extracted to {tmpDir}");
                }

                // 2. Locate source database
                var srcDbDir = Path.Combine(tmpDir, "database");
                var srcDb = Path.Combine(srcDbDir, "foodbookapp.db");
                if (!File.Exists(srcDb))
                {
                    Log($"ERROR: Database file not found at {srcDb}");
                    throw new FileNotFoundException("Brak pliku bazy w archiwum", srcDb);
                }
                Log($"Source database found: {srcDb}");

                // Ensure compatibility: add missing timestamp columns and defaults for older archives
                try
                {
                    Log("Ensuring timestamp columns exist in source DB for backward compatibility...");
                    ArchiveCompatibilityHelper.EnsureTimestampColumnsExist(srcDb, Log);
                    Log("Timestamp compatibility fixes applied (if needed)");
                }
                catch (Exception exCompat)
                {
                    Log($"WARNING: Compatibility helper failed: {exCompat.Message}");
                }

                // 3. Backup current database
                var destDb = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");
                var backup = destDb + $".bak_{DateTime.Now:yyyyMMddHHmmss}";
                try
                {
                    if (File.Exists(destDb))
                    {
                        File.Copy(destDb, backup, true);
                        Log($"Current database backed up to: {backup}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"WARNING: Backup failed: {ex.Message}");
                }

                // 4. Create fresh schema
                Log("Creating fresh database schema...");
                await _dbService.ResetDatabaseAsync();
                Log("Fresh schema created");

                // 5. Import data using EF Core with proper mapping
                await ImportDataWithEfCoreAsync(srcDb, destDb, Log);

                // 6. Import preferences
                var prefsJson = Path.Combine(tmpDir, "prefs.json");
                if (File.Exists(prefsJson))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(prefsJson);
                        var obj = System.Text.Json.JsonSerializer.Deserialize<PrefsDto>(json);
                        if (obj != null)
                        {
                            _prefs.SaveLanguage(obj.Culture);
                            _prefs.SaveTheme(obj.Theme);
                            _prefs.SaveColorTheme(obj.ColorTheme);
                            _prefs.SaveColorfulBackground(obj.ColorfulBackground);
                            _prefs.SaveWallpaperEnabled(obj.WallpaperBackground);
                            _prefs.SaveFontFamily(obj.FontFamily);
                            _prefs.SaveFontSize(obj.FontSize);
                            Log("Preferences restored");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"WARNING: Preferences import failed: {ex.Message}");
                    }
                }
                else
                {
                    Log("No preferences file found - skipping");
                }

                // 7. Run migrations to ensure schema is up-to-date
                await _dbService.MigrateDatabaseAsync();
                Log("Final migrations applied");

                Log("=== RESTORE COMPLETED SUCCESSFULLY ===");
                
                // Update UI status - more reliable than DisplayAlert
                StatusLabel.Text = GetLocalizedText("DataArchivizationPageResources", "RestoreSuccess", "? Przywrócono dane. Uruchom ponownie aplikacjê.");
                StatusLabel.TextColor = Colors.Green;
            }
            catch (Exception ex)
            {
                Log($"? FATAL ERROR: {ex.Message}");
                Log($"Stack trace: {ex.StackTrace}");
                
                // Update UI status - more reliable than DisplayAlert
                var errorMsg = string.Format(
                    GetLocalizedText("DataArchivizationPageResources", "RestoreFailed", "? Restore failed: {0}"), 
                    ex.Message);
                StatusLabel.Text = errorMsg;
                StatusLabel.TextColor = Colors.Red;
                
                System.Diagnostics.Debug.WriteLine($"[Restore] Error shown to user: {errorMsg}");
            }
        }

        /// <summary>
        /// Import data from old database to new database using EF Core with schema-aware mapping
        /// </summary>
        private async Task ImportDataWithEfCoreAsync(string srcDb, string destDb, Action<string> log)
        {
            log("Starting data import with schema mapping...");

            // Create restore context for tracking ID mappings and relationships
            var restoreCtx = new RestoreContext();

            try
            {
                // Create scope and get DbContext
                using var scope = FoodbookApp.MauiProgram.ServiceProvider!.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Read data from source database using raw SQL (schema-agnostic)
                var srcConnectionString = new SqliteConnectionStringBuilder 
                { 
                    DataSource = srcDb, 
                    Mode = SqliteOpenMode.ReadOnly 
                }.ToString();

                using var srcConnection = new SqliteConnection(srcConnectionString);
                await srcConnection.OpenAsync();
                log("Source database opened");

                // Check which tables exist in source
                var existingTables = await GetExistingTablesAsync(srcConnection, log);
                log($"Found {existingTables.Count} tables in source database");

                // Phase 1: Import all entities and capture ID mappings
                log("=== Phase 1: Importing entities ===");
                await ImportFoldersAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportRecipeLabelsAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportIngredientsAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportRecipesAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportRecipeRecipeLabelAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportPlansAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportPlannedMealsAsync(srcConnection, context, existingTables, log, restoreCtx);
                await ImportShoppingListItemsAsync(srcConnection, context, existingTables, log, restoreCtx);

                // Phase 2: Rebuild relationships using captured mappings
                log("=== Phase 2: Rebuilding relationships ===");
                restoreCtx.LogMappings(log);
                
                await RebuildFolderHierarchyAsync(context, restoreCtx, log);
                await AssignRecipesToFoldersAsync(context, restoreCtx, log);

                log("All data imported successfully with folder structure preserved");
            }
            catch (Exception ex)
            {
                log($"? Import error: {ex.Message}");
                throw;
            }
        }

        private async Task<HashSet<string>> GetExistingTablesAsync(SqliteConnection connection, Action<string> log)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            return tables;
        }

        private async Task<List<string>> GetColumnNamesAsync(SqliteConnection connection, string tableName, Action<string> log)
        {
            var columns = new List<string>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info('{tableName}')";
            try
            {
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    columns.Add(reader.GetString(1)); // Column name is at index 1
                }
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to get columns for {tableName}: {ex.Message}");
            }
            return columns;
        }

        private async Task ImportFoldersAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("Folders"))
            {
                log("Folders table not found in source - skipping");
                return;
            }

            try
            {
                var folders = new List<Folder>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Folders";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");
                var hasParentFolderIdColumn = HasColumn(reader, "ParentFolderId");

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    Guid? srcGuid = null;
                    int? oldIntId = null;
                    Guid? srcParentGuid = null;
                    int? oldParentIntId = null;

                    // Read ID - try string (GUID) first, then integer
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        var rawId = reader.GetValue(reader.GetOrdinal("Id"));
                        if (rawId is string idStr && Guid.TryParse(idStr, out var parsedGuid))
                        {
                            srcGuid = parsedGuid;
                            newId = parsedGuid; // Preserve GUID from archive
                            restoreCtx.IsGuidBasedArchive = true;
                        }
                        else if (rawId is long longId)
                        {
                            oldIntId = (int)longId;
                        }
                        else if (rawId is int intId)
                        {
                            oldIntId = intId;
                        }
                    }

                    // Read ParentFolderId - try string (GUID) first, then integer
                    if (hasParentFolderIdColumn && !reader.IsDBNull(reader.GetOrdinal("ParentFolderId")))
                    {
                        var rawParentId = reader.GetValue(reader.GetOrdinal("ParentFolderId"));
                        if (rawParentId is string parentStr && Guid.TryParse(parentStr, out var parsedParentGuid))
                        {
                            srcParentGuid = parsedParentGuid;
                        }
                        else if (rawParentId is long longParentId)
                        {
                            oldParentIntId = (int)longParentId;
                        }
                        else if (rawParentId is int intParentId)
                        {
                            oldParentIntId = intParentId;
                        }
                    }

                    var folder = new Folder
                    {
                        Id = newId,
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = !reader.IsDBNull(reader.GetOrdinal("Description")) ? reader.GetString(reader.GetOrdinal("Description")) : null,
                        ParentFolderId = null, // Will be set later after all folders are imported
                        Order = HasColumn(reader, "Order") && !reader.IsDBNull(reader.GetOrdinal("Order")) ? reader.GetInt32(reader.GetOrdinal("Order")) : 0,
                        CreatedAt = HasColumn(reader, "CreatedAt") && !reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? reader.GetDateTime(reader.GetOrdinal("CreatedAt")) : DateTime.Now
                    };
                    folders.Add(folder);

                    // Store mappings based on ID type
                    if (srcGuid.HasValue)
                    {
                        restoreCtx.FolderGuidMapping[srcGuid.Value] = newId;
                        restoreCtx.FolderGuidParentMap[srcGuid.Value] = srcParentGuid;
                        log($"  Folder '{folder.Name}': GUID {srcGuid.Value} -> {newId}, parent: {(srcParentGuid.HasValue ? srcParentGuid.Value.ToString() : "null")}");
                    }
                    else if (oldIntId.HasValue)
                    {
                        restoreCtx.FolderIdMapping[oldIntId.Value] = newId;
                        restoreCtx.FolderParentMap[oldIntId.Value] = oldParentIntId;
                        log($"  Folder '{folder.Name}': old ID {oldIntId.Value} -> new ID {newId}");
                    }
                }

                foreach (var folder in folders)
                {
                    context.Folders.Add(folder);
                }
                await context.SaveChangesAsync();
                log($"Imported {folders.Count} folders");

                // DO NOT rebuild hierarchy here - will be done in Phase 2
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import Folders: {ex.Message}");
            }
        }

        private async Task ImportRecipeLabelsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("RecipeLabels"))
            {
                log("RecipeLabels table not found in source - skipping");
                return;
            }

            try
            {
                var labels = new List<RecipeLabel>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM RecipeLabels";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    
                    // Capture old integer ID
                    int? oldId = null;
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        try { oldId = reader.GetInt32(reader.GetOrdinal("Id")); } catch { }
                    }

                    var label = new RecipeLabel
                    {
                        Id = newId,
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        ColorHex = HasColumn(reader, "ColorHex") && !reader.IsDBNull(reader.GetOrdinal("ColorHex")) ? reader.GetString(reader.GetOrdinal("ColorHex")) : null,
                        CreatedAt = HasColumn(reader, "CreatedAt") && !reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? reader.GetDateTime(reader.GetOrdinal("CreatedAt")) : DateTime.Now
                    };
                    labels.Add(label);

                    if (oldId.HasValue)
                    {
                        restoreCtx.LabelIdMapping[oldId.Value] = newId;
                    }
                }

                foreach (var label in labels)
                {
                    context.RecipeLabels.Add(label);
                }
                await context.SaveChangesAsync();
                log($"Imported {labels.Count} recipe labels (legacy ids remapped)");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import RecipeLabels: {ex.Message}");
            }
        }

        private async Task ImportIngredientsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("Ingredients"))
            {
                log("Ingredients table not found in source - skipping");
                return;
            }

            try
            {
                var ingredients = new List<Ingredient>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Ingredients WHERE RecipeId IS NULL";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    
                    // Capture old integer ID
                    int? oldId = null;
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        try { oldId = reader.GetInt32(reader.GetOrdinal("Id")); } catch { }
                    }

                    var ingredient = new Ingredient
                    {
                        Id = newId,
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                        Unit = (Unit)reader.GetInt32(reader.GetOrdinal("Unit")),
                        UnitWeight = HasColumn(reader, "UnitWeight") && !reader.IsDBNull(reader.GetOrdinal("UnitWeight")) ? reader.GetDouble(reader.GetOrdinal("UnitWeight")) : 1.0,
                        Calories = reader.GetDouble(reader.GetOrdinal("Calories")),
                        Protein = reader.GetDouble(reader.GetOrdinal("Protein")),
                        Fat = reader.GetDouble(reader.GetOrdinal("Fat")),
                        Carbs = reader.GetDouble(reader.GetOrdinal("Carbs")),
                        RecipeId = null
                    };
                    ingredients.Add(ingredient);

                    if (oldId.HasValue)
                    {
                        restoreCtx.IngredientIdMapping[oldId.Value] = newId;
                    }
                }

                foreach (var ingredient in ingredients)
                {
                    context.Ingredients.Add(ingredient);
                }
                await context.SaveChangesAsync();
                log($"Imported {ingredients.Count} standalone ingredients (legacy ids remapped)");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import standalone Ingredients: {ex.Message}");
            }
        }

        private async Task ImportRecipesAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("Recipes"))
            {
                log("Recipes table not found in source - skipping");
                return;
            }

            try
            {
                var recipes = new List<Recipe>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Recipes";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");
                var hasFolderIdColumn = HasColumn(reader, "FolderId");

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    Guid? srcGuid = null;
                    int? oldIntId = null;
                    Guid? srcFolderGuid = null;
                    int? oldFolderIntId = null;

                    // Read ID - try string (GUID) first, then integer
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        var rawId = reader.GetValue(reader.GetOrdinal("Id"));
                        if (rawId is string idStr && Guid.TryParse(idStr, out var parsedGuid))
                        {
                            srcGuid = parsedGuid;
                            newId = parsedGuid; // Preserve GUID from archive
                        }
                        else if (rawId is long longId)
                        {
                            oldIntId = (int)longId;
                        }
                        else if (rawId is int intId)
                        {
                            oldIntId = intId;
                        }
                    }

                    // Read FolderId - try string (GUID) first, then integer
                    if (hasFolderIdColumn && !reader.IsDBNull(reader.GetOrdinal("FolderId")))
                    {
                        var rawFolderId = reader.GetValue(reader.GetOrdinal("FolderId"));
                        if (rawFolderId is string folderStr && Guid.TryParse(folderStr, out var parsedFolderGuid))
                        {
                            srcFolderGuid = parsedFolderGuid;
                        }
                        else if (rawFolderId is long longFolderId)
                        {
                            oldFolderIntId = (int)longFolderId;
                        }
                        else if (rawFolderId is int intFolderId)
                        {
                            oldFolderIntId = intFolderId;
                        }
                    }

                    var recipe = new Recipe
                    {
                        Id = newId,
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = !reader.IsDBNull(reader.GetOrdinal("Description")) ? reader.GetString(reader.GetOrdinal("Description")) : null,
                        Calories = reader.GetDouble(reader.GetOrdinal("Calories")),
                        Protein = reader.GetDouble(reader.GetOrdinal("Protein")),
                        Fat = reader.GetDouble(reader.GetOrdinal("Fat")),
                        Carbs = reader.GetDouble(reader.GetOrdinal("Carbs")),
                        IloscPorcji = reader.GetInt32(reader.GetOrdinal("IloscPorcji")),
                        FolderId = null // Will be set later after folder relationships are rebuilt
                    };
                    recipes.Add(recipe);

                    // Store mappings based on ID type
                    if (srcGuid.HasValue)
                    {
                        restoreCtx.RecipeGuidMapping[srcGuid.Value] = newId;
                        restoreCtx.RecipeGuidFolderMap[srcGuid.Value] = srcFolderGuid;
                        if (srcFolderGuid.HasValue)
                        {
                            log($"  Recipe '{recipe.Name}': GUID {srcGuid.Value} -> {newId}, folder: {srcFolderGuid.Value}");
                        }
                    }
                    else if (oldIntId.HasValue)
                    {
                        restoreCtx.RecipeIdMapping[oldIntId.Value] = newId;
                        restoreCtx.RecipeFolderMap[oldIntId.Value] = oldFolderIntId;
                        if (oldFolderIntId.HasValue)
                        {
                            log($"  Recipe '{recipe.Name}': old ID {oldIntId.Value} -> new ID {newId}, folder: {oldFolderIntId.Value}");
                        }
                    }
                }

                foreach (var recipe in recipes)
                {
                    context.Recipes.Add(recipe);
                }
                await context.SaveChangesAsync();
                log($"Imported {recipes.Count} recipes");

                await ImportRecipeIngredientsAsync(srcConnection, context, log, restoreCtx);
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import Recipes: {ex.Message}");
            }
        }

        private async Task ImportRecipeIngredientsAsync(SqliteConnection srcConnection, AppDbContext context, Action<string> log, RestoreContext restoreCtx)
        {
            try
            {
                var ingredients = new List<Ingredient>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Ingredients WHERE RecipeId IS NOT NULL";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");
                var hasRecipeIdColumn = HasColumn(reader, "RecipeId");

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    Guid? newRecipeId = null;

                    // Read RecipeId - try string (GUID) first, then integer
                    if (hasRecipeIdColumn && !reader.IsDBNull(reader.GetOrdinal("RecipeId")))
                    {
                        var rawRecipeId = reader.GetValue(reader.GetOrdinal("RecipeId"));
                        if (rawRecipeId is string recipeStr && Guid.TryParse(recipeStr, out var parsedRecipeGuid))
                        {
                            // GUID-based: map directly
                            if (restoreCtx.RecipeGuidMapping.TryGetValue(parsedRecipeGuid, out var mappedGuid))
                            {
                                newRecipeId = mappedGuid;
                            }
                            else
                            {
                                // Recipe GUID preserved directly
                                newRecipeId = parsedRecipeGuid;
                            }
                        }
                        else if (rawRecipeId is long longRecipeId)
                        {
                            // Legacy: map from int
                            if (restoreCtx.RecipeIdMapping.TryGetValue((int)longRecipeId, out var mappedId))
                            {
                                newRecipeId = mappedId;
                            }
                        }
                        else if (rawRecipeId is int intRecipeId)
                        {
                            if (restoreCtx.RecipeIdMapping.TryGetValue(intRecipeId, out var mappedId))
                            {
                                newRecipeId = mappedId;
                            }
                        }
                    }

                    var ingredient = new Ingredient
                    {
                        Id = newId,
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                        Unit = (Unit)reader.GetInt32(reader.GetOrdinal("Unit")),
                        UnitWeight = HasColumn(reader, "UnitWeight") && !reader.IsDBNull(reader.GetOrdinal("UnitWeight")) ? reader.GetDouble(reader.GetOrdinal("UnitWeight")) : 1.0,
                        Calories = reader.GetDouble(reader.GetOrdinal("Calories")),
                        Protein = reader.GetDouble(reader.GetOrdinal("Protein")),
                        Fat = reader.GetDouble(reader.GetOrdinal("Fat")),
                        Carbs = reader.GetDouble(reader.GetOrdinal("Carbs")),
                        RecipeId = newRecipeId
                    };
                    ingredients.Add(ingredient);
                }

                foreach (var ingredient in ingredients)
                {
                    context.Ingredients.Add(ingredient);
                }
                await context.SaveChangesAsync();
                log($"Imported {ingredients.Count} recipe ingredients");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import recipe ingredients: {ex.Message}");
            }
        }

        private async Task ImportRecipeRecipeLabelAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("RecipeRecipeLabel"))
            {
                log("RecipeRecipeLabel junction table not found - skipping");
                return;
            }

            try
            {
                var links = new List<(int RecipeId, int LabelId)>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM RecipeRecipeLabel";
                using var reader = await cmd.ExecuteReaderAsync();
                
                // Column names may vary - try both naming conventions
                int recipeIdIndex = -1, labelIdIndex = -1;
                try { recipeIdIndex = reader.GetOrdinal("RecipesId"); } catch { }
                try { labelIdIndex = reader.GetOrdinal("LabelsId"); } catch { }
                
                if (recipeIdIndex == -1) try { recipeIdIndex = reader.GetOrdinal("RecipeId"); } catch { }
                if (labelIdIndex == -1) try { labelIdIndex = reader.GetOrdinal("RecipeLabelId"); } catch { }

                if (recipeIdIndex >= 0 && labelIdIndex >= 0)
                {
                    while (await reader.ReadAsync())
                    {
                        var oldRecipeId = reader.GetInt32(recipeIdIndex);
                        var oldLabelId = reader.GetInt32(labelIdIndex);
                        links.Add((oldRecipeId, oldLabelId));
                        restoreCtx.RecipeLabelLinks.Add((oldRecipeId, oldLabelId));
                    }
                }

                // Now insert with mapped GUIDs
                int insertedCount = 0;
                await context.Database.OpenConnectionAsync();
                try
                {
                    using var linkCmd = context.Database.GetDbConnection().CreateCommand();
                    linkCmd.CommandText = "INSERT OR IGNORE INTO RecipeRecipeLabel (RecipesId, LabelsId) VALUES (@recipeId, @labelId)";
                    var recipeParam = linkCmd.CreateParameter();
                    recipeParam.ParameterName = "@recipeId";
                    linkCmd.Parameters.Add(recipeParam);
                    var labelParam = linkCmd.CreateParameter();
                    labelParam.ParameterName = "@labelId";
                    linkCmd.Parameters.Add(labelParam);

                    foreach (var (oldRecipeId, oldLabelId) in links)
                    {
                        // Map old IDs to new GUIDs
                        if (restoreCtx.RecipeIdMapping.TryGetValue(oldRecipeId, out var newRecipeId) &&
                            restoreCtx.LabelIdMapping.TryGetValue(oldLabelId, out var newLabelId))
                        {
                            recipeParam.Value = newRecipeId.ToString();
                            labelParam.Value = newLabelId.ToString();
                            try
                            {
                                await linkCmd.ExecuteNonQueryAsync();
                                insertedCount++;
                            }
                            catch (Exception ex)
                            {
                                log($"WARNING: Failed to link recipe {oldRecipeId}->{newRecipeId} to label {oldLabelId}->{newLabelId}: {ex.Message}");
                            }
                        }
                        else
                        {
                            log($"WARNING: Could not map recipe {oldRecipeId} or label {oldLabelId} - skipping link");
                        }
                    }
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
                log($"Imported {insertedCount}/{links.Count} recipe-label links (properly remapped)");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import RecipeRecipeLabel: {ex.Message}");
            }
        }

        private async Task ImportPlansAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("Plans"))
            {
                log("Plans table not found in source - skipping");
                return;
            }

            try
            {
                var plans = new List<Plan>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Plans";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");
                var hasTypeColumn = HasColumn(reader, "Type");
                var hasTitleColumn = HasColumn(reader, "Title");
                var hasNameColumn = HasColumn(reader, "Name");
                var hasStartDateColumn = HasColumn(reader, "StartDate");
                var hasEndDateColumn = HasColumn(reader, "EndDate");
                var hasIsArchivedColumn = HasColumn(reader, "IsArchived");

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();

                    // Read ID (may be GUID or int)
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        var rawId = reader.GetValue(reader.GetOrdinal("Id"));
                        if (rawId is string idStr && Guid.TryParse(idStr, out var parsedGuid))
                        {
                            newId = parsedGuid; // Preserve GUID
                        }
                        else if (rawId is long longId)
                        {
                            // Legacy int - store mapping
                            restoreCtx.PlanIdMapping[(int)longId] = newId;
                        }
                        else if (rawId is int intId)
                        {
                            restoreCtx.PlanIdMapping[intId] = newId;
                        }
                    }

                    // SAFE reading of Type (may be int, string, or missing)
                    PlanType planType = PlanType.ShoppingList; // Default
                    if (hasTypeColumn)
                    {
                        try
                        {
                            var typeOrdinal = reader.GetOrdinal("Type");
                            if (!reader.IsDBNull(typeOrdinal))
                            {
                                var rawType = reader.GetValue(typeOrdinal);
                                if (rawType is long longType)
                                {
                                    planType = (PlanType)(int)longType;
                                }
                                else if (rawType is int intType)
                                {
                                    planType = (PlanType)intType;
                                }
                                else if (rawType is string strType && int.TryParse(strType, out var parsedType))
                                {
                                    planType = (PlanType)parsedType;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log($"WARNING: Failed to read Type column: {ex.Message}, using default ShoppingList");
                        }
                    }

                    // SAFE reading of Title/Name
                    string? title = null;
                    if (hasTitleColumn)
                    {
                        try
                        {
                            var titleOrdinal = reader.GetOrdinal("Title");
                            if (!reader.IsDBNull(titleOrdinal))
                            {
                                title = reader.GetString(titleOrdinal);
                            }
                        }
                        catch { }
                    }
                    if (string.IsNullOrWhiteSpace(title) && hasNameColumn)
                    {
                        try
                        {
                            var nameOrdinal = reader.GetOrdinal("Name");
                            if (!reader.IsDBNull(nameOrdinal))
                            {
                                title = reader.GetString(nameOrdinal);
                            }
                        }
                        catch { }
                    }

                    var plan = new Plan
                    {
                        Id = newId,
                        StartDate = hasStartDateColumn && !reader.IsDBNull(reader.GetOrdinal("StartDate")) 
                            ? reader.GetDateTime(reader.GetOrdinal("StartDate")) 
                            : DateTime.Now,
                        EndDate = hasEndDateColumn && !reader.IsDBNull(reader.GetOrdinal("EndDate")) 
                            ? reader.GetDateTime(reader.GetOrdinal("EndDate")) 
                            : DateTime.Now.AddDays(7),
                        IsArchived = hasIsArchivedColumn && !reader.IsDBNull(reader.GetOrdinal("IsArchived")) 
                            ? reader.GetBoolean(reader.GetOrdinal("IsArchived")) 
                            : false,
                        Type = planType,
                        Title = title,
                        LinkedShoppingListPlanId = null
                    };
                    plans.Add(plan);
                }

                foreach (var plan in plans)
                {
                    context.Plans.Add(plan);
                }
                await context.SaveChangesAsync();
                log($"Imported {plans.Count} plans");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import Plans: {ex.Message}");
            }
        }

        private async Task ImportPlannedMealsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("PlannedMeals"))
            {
                log("PlannedMeals table not found in source - skipping");
                return;
            }

            try
            {
                var meals = new List<PlannedMeal>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM PlannedMeals";
                using var reader = await cmd.ExecuteReaderAsync();

                var hasIdColumn = HasColumn(reader, "Id");
                var hasRecipeIdColumn = HasColumn(reader, "RecipeId");
                var hasPlanIdColumn = HasColumn(reader, "PlanId");
                var hasDateColumn = HasColumn(reader, "Date");
                var hasPortionsColumn = HasColumn(reader, "Portions");

                // Get existing IDs to validate foreign keys
                var existingRecipeIds = await context.Recipes.Select(r => r.Id).ToHashSetAsync();
                var existingPlanIds = await context.Plans.Select(p => p.Id).ToHashSetAsync();

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    Guid? newRecipeId = null;
                    Guid? newPlanId = null;

                    // Read ID (may be GUID or int)
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        var rawId = reader.GetValue(reader.GetOrdinal("Id"));
                        if (rawId is string idStr && Guid.TryParse(idStr, out var parsedGuid))
                        {
                            newId = parsedGuid; // Preserve GUID
                        }
                    }

                    // Read RecipeId (may be GUID or int)
                    if (hasRecipeIdColumn && !reader.IsDBNull(reader.GetOrdinal("RecipeId")))
                    {
                        var rawRecipeId = reader.GetValue(reader.GetOrdinal("RecipeId"));
                        if (rawRecipeId is string recipeStr && Guid.TryParse(recipeStr, out var parsedRecipeGuid))
                        {
                            // GUID-based: use direct or mapped
                            if (restoreCtx.RecipeGuidMapping.TryGetValue(parsedRecipeGuid, out var mappedGuid))
                            {
                                newRecipeId = mappedGuid;
                            }
                            else
                            {
                                newRecipeId = parsedRecipeGuid;
                            }
                        }
                        else if (rawRecipeId is long longRecipeId)
                        {
                            if (restoreCtx.RecipeIdMapping.TryGetValue((int)longRecipeId, out var mappedId))
                            {
                                newRecipeId = mappedId;
                            }
                        }
                        else if (rawRecipeId is int intRecipeId)
                        {
                            if (restoreCtx.RecipeIdMapping.TryGetValue(intRecipeId, out var mappedId))
                            {
                                newRecipeId = mappedId;
                            }
                        }
                    }

                    // Read PlanId (may be GUID or int)
                    if (hasPlanIdColumn && !reader.IsDBNull(reader.GetOrdinal("PlanId")))
                    {
                        var rawPlanId = reader.GetValue(reader.GetOrdinal("PlanId"));
                        if (rawPlanId is string planStr && Guid.TryParse(planStr, out var parsedPlanGuid))
                        {
                            newPlanId = parsedPlanGuid;
                        }
                        else if (rawPlanId is long longPlanId)
                        {
                            if (restoreCtx.PlanIdMapping.TryGetValue((int)longPlanId, out var mappedPlanId))
                            {
                                newPlanId = mappedPlanId;
                            }
                        }
                        else if (rawPlanId is int intPlanId)
                        {
                            if (restoreCtx.PlanIdMapping.TryGetValue(intPlanId, out var mappedPlanId))
                            {
                                newPlanId = mappedPlanId;
                            }
                        }
                    }

                    // SKIP meals with invalid foreign keys
                    if (!newRecipeId.HasValue || !existingRecipeIds.Contains(newRecipeId.Value))
                    {
                        log($"WARNING: Skipping planned meal - Recipe ID {newRecipeId} not found");
                        continue;
                    }
                    if (newPlanId.HasValue && !existingPlanIds.Contains(newPlanId.Value))
                    {
                        log($"WARNING: Skipping planned meal - Plan ID {newPlanId} not found");
                        continue;
                    }

                    var meal = new PlannedMeal
                    {
                        Id = newId,
                        RecipeId = newRecipeId.Value,
                        PlanId = newPlanId,
                        Date = hasDateColumn && !reader.IsDBNull(reader.GetOrdinal("Date"))
                            ? reader.GetDateTime(reader.GetOrdinal("Date"))
                            : DateTime.Now,
                        Portions = hasPortionsColumn && !reader.IsDBNull(reader.GetOrdinal("Portions"))
                            ? reader.GetInt32(reader.GetOrdinal("Portions"))
                            : 1
                    };
                    meals.Add(meal);
                }

                foreach (var meal in meals)
                {
                    context.PlannedMeals.Add(meal);
                }
                await context.SaveChangesAsync();
                log($"Imported {meals.Count} planned meals (properly linked)");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import PlannedMeals: {ex.Message}");
                if (ex.InnerException != null)
                {
                    log($"  Inner: {ex.InnerException.Message}");
                }
            }
        }

        private async Task ImportShoppingListItemsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log, RestoreContext restoreCtx)
        {
            if (!existingTables.Contains("ShoppingListItems"))
            {
                log("ShoppingListItems table not found in source - skipping");
                return;
            }

            try
            {
                var items = new List<ShoppingListItem>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM ShoppingListItems";
                using var reader = await cmd.ExecuteReaderAsync();
                
                var hasIdColumn = HasColumn(reader, "Id");
                var hasPlanIdColumn = HasColumn(reader, "PlanId");

                // Get all existing plan IDs to validate foreign key constraints
                var existingPlanIds = await context.Plans.Select(p => p.Id).ToHashSetAsync();

                while (await reader.ReadAsync())
                {
                    var newId = Guid.NewGuid();
                    Guid? newPlanId = null;

                    // Read ID (may be GUID or int)
                    if (hasIdColumn && !reader.IsDBNull(reader.GetOrdinal("Id")))
                    {
                        var rawId = reader.GetValue(reader.GetOrdinal("Id"));
                        if (rawId is string idStr && Guid.TryParse(idStr, out var parsedGuid))
                        {
                            newId = parsedGuid; // Preserve GUID
                        }
                    }

                    // Read PlanId (may be GUID or int)
                    if (hasPlanIdColumn && !reader.IsDBNull(reader.GetOrdinal("PlanId")))
                    {
                        var rawPlanId = reader.GetValue(reader.GetOrdinal("PlanId"));
                        if (rawPlanId is string planStr && Guid.TryParse(planStr, out var parsedPlanGuid))
                        {
                            newPlanId = parsedPlanGuid;
                        }
                        else if (rawPlanId is long longPlanId)
                        {
                            if (restoreCtx.PlanIdMapping.TryGetValue((int)longPlanId, out var mappedPlanId))
                            {
                                newPlanId = mappedPlanId;
                            }
                        }
                        else if (rawPlanId is int intPlanId)
                        {
                            if (restoreCtx.PlanIdMapping.TryGetValue(intPlanId, out var mappedPlanId))
                            {
                                newPlanId = mappedPlanId;
                            }
                        }
                    }

                    // SKIP items with invalid/missing Plan references
                    if (!newPlanId.HasValue || !existingPlanIds.Contains(newPlanId.Value))
                    {
                        log($"WARNING: Skipping shopping item '{reader.GetString(reader.GetOrdinal("IngredientName"))}' - Plan ID {newPlanId} not found");
                        continue;
                    }

                    var item = new ShoppingListItem
                    {
                        Id = newId,
                        PlanId = newPlanId.Value,
                        IngredientName = reader.GetString(reader.GetOrdinal("IngredientName")),
                        Unit = (Unit)reader.GetInt32(reader.GetOrdinal("Unit")),
                        Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                        IsChecked = reader.GetBoolean(reader.GetOrdinal("IsChecked")),
                        Order = HasColumn(reader, "Order") && !reader.IsDBNull(reader.GetOrdinal("Order")) ? reader.GetInt32(reader.GetOrdinal("Order")) : 0
                    };
                    items.Add(item);
                }

                foreach (var item in items)
                {
                    context.ShoppingListItems.Add(item);
                }
                await context.SaveChangesAsync();
                log($"Imported {items.Count} shopping list items (properly linked)");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import ShoppingListItems: {ex.Message}");
                if (ex.InnerException != null)
                {
                    log($"  Inner: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Rebuilds folder parent-child relationships after all folders have been imported
        /// </summary>
        private async Task RebuildFolderHierarchyAsync(AppDbContext context, RestoreContext restoreCtx, Action<string> log)
        {
            log("Rebuilding folder hierarchy...");
            
            int updatedCount = 0;

            if (restoreCtx.IsGuidBasedArchive)
            {
                // GUID-based archive: use FolderGuidParentMap
                // Load all folders into memory as tracked entities
                var allFolders = await context.Folders.ToListAsync();
                var folderDict = allFolders.ToDictionary(f => f.Id);

                foreach (var (srcFolderGuid, srcParentGuid) in restoreCtx.FolderGuidParentMap)
                {
                    if (!srcParentGuid.HasValue)
                        continue; // Root folder, no parent to set

                    // Get target folder GUID
                    if (!restoreCtx.FolderGuidMapping.TryGetValue(srcFolderGuid, out var targetFolderId))
                    {
                        log($"WARNING: Could not find mapping for folder with source GUID {srcFolderGuid}");
                        continue;
                    }

                    // Get target parent GUID
                    if (!restoreCtx.FolderGuidMapping.TryGetValue(srcParentGuid.Value, out var targetParentId))
                    {
                        log($"WARNING: Could not find mapping for parent folder with source GUID {srcParentGuid.Value}");
                        continue;
                    }

                    // Update folder's parent using tracked entity
                    if (folderDict.TryGetValue(targetFolderId, out var folder))
                    {
                        folder.ParentFolderId = targetParentId;
                        updatedCount++;
                    }
                    else
                    {
                        log($"WARNING: Folder with ID {targetFolderId} not found in database");
                    }
                }

                await context.SaveChangesAsync();
            }
            else
            {
                // Legacy archive: use FolderParentMap
                // Load all folders into memory as tracked entities
                var allFolders = await context.Folders.ToListAsync();
                var folderDict = allFolders.ToDictionary(f => f.Id);

                foreach (var (oldFolderId, oldParentId) in restoreCtx.FolderParentMap)
                {
                    if (!oldParentId.HasValue)
                        continue; // Root folder, no parent to set

                    // Get new folder GUID
                    if (!restoreCtx.FolderIdMapping.TryGetValue(oldFolderId, out var newFolderId))
                    {
                        log($"WARNING: Could not find mapping for folder with old ID {oldFolderId}");
                        continue;
                    }

                    // Get new parent GUID
                    if (!restoreCtx.FolderIdMapping.TryGetValue(oldParentId.Value, out var newParentId))
                    {
                        log($"WARNING: Could not find mapping for parent folder with old ID {oldParentId.Value}");
                        continue;
                    }

                    // Update folder's parent using tracked entity
                    if (folderDict.TryGetValue(newFolderId, out var folder))
                    {
                        folder.ParentFolderId = newParentId;
                        updatedCount++;
                    }
                    else
                    {
                        log($"WARNING: Folder with ID {newFolderId} not found in database");
                    }
                }

                await context.SaveChangesAsync();
            }

            log($"Rebuilt folder hierarchy: {updatedCount} parent-child relationships restored");
        }

        /// <summary>
        /// Assigns recipes to their folders based on the mapping
        /// </summary>
        private async Task AssignRecipesToFoldersAsync(AppDbContext context, RestoreContext restoreCtx, Action<string> log)
        {
            log("Assigning recipes to folders...");
            
            int assignedCount = 0;

            if (restoreCtx.IsGuidBasedArchive)
            {
                // GUID-based archive: use RecipeGuidFolderMap
                // Load all recipes into memory as tracked entities
                var allRecipes = await context.Recipes.ToListAsync();
                var recipeDict = allRecipes.ToDictionary(r => r.Id);

                foreach (var (srcRecipeGuid, srcFolderGuid) in restoreCtx.RecipeGuidFolderMap)
                {
                    if (!srcFolderGuid.HasValue)
                        continue; // Recipe not in any folder

                    // Get target recipe GUID
                    if (!restoreCtx.RecipeGuidMapping.TryGetValue(srcRecipeGuid, out var targetRecipeId))
                    {
                        log($"WARNING: Could not find mapping for recipe with source GUID {srcRecipeGuid}");
                        continue;
                    }

                    // Get target folder GUID
                    if (!restoreCtx.FolderGuidMapping.TryGetValue(srcFolderGuid.Value, out var targetFolderId))
                    {
                        log($"WARNING: Could not find mapping for folder with source GUID {srcFolderGuid.Value}");
                        continue;
                    }

                    // Update recipe's folder using tracked entity
                    if (recipeDict.TryGetValue(targetRecipeId, out var recipe))
                    {
                        recipe.FolderId = targetFolderId;
                        assignedCount++;
                    }
                    else
                    {
                        log($"WARNING: Recipe with ID {targetRecipeId} not found in database");
                    }
                }

                await context.SaveChangesAsync();
            }
            else
            {
                // Legacy archive: use RecipeFolderMap
                // Load all recipes into memory as tracked entities
                var allRecipes = await context.Recipes.ToListAsync();
                var recipeDict = allRecipes.ToDictionary(r => r.Id);

                foreach (var (oldRecipeId, oldFolderId) in restoreCtx.RecipeFolderMap)
                {
                    if (!oldFolderId.HasValue)
                        continue; // Recipe not in any folder

                    // Get new recipe GUID
                    if (!restoreCtx.RecipeIdMapping.TryGetValue(oldRecipeId, out var newRecipeId))
                    {
                        log($"WARNING: Could not find mapping for recipe with old ID {oldRecipeId}");
                        continue;
                    }

                    // Get new folder GUID
                    if (!restoreCtx.FolderIdMapping.TryGetValue(oldFolderId.Value, out var newFolderId))
                    {
                        log($"WARNING: Could not find mapping for folder with old ID {oldFolderId.Value}");
                        continue;
                    }

                    // Update recipe's folder using tracked entity
                    if (recipeDict.TryGetValue(newRecipeId, out var recipe))
                    {
                        recipe.FolderId = newFolderId;
                        assignedCount++;
                    }
                    else
                    {
                        log($"WARNING: Recipe with ID {newRecipeId} not found in database");
                    }
                }

                await context.SaveChangesAsync();
            }

            log($"Assigned {assignedCount} recipes to folders");
        }

        /// <summary>
        /// Helper method to check if a column exists in SqliteDataReader
        /// </summary>
        private static bool HasColumn(SqliteDataReader reader, string columnName)
        {
            try
            {
                reader.GetOrdinal(columnName);
                return true;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
        }
    }
}
