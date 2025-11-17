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
            try
            {
                // Run checkpoint to flush WAL into main DB to avoid losing recent changes
                var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadWrite, Cache = SqliteCacheMode.Shared }.ToString();
                using var conn = new SqliteConnection(cs);
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); PRAGMA optimize;";
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SQLite checkpoint failed: {ex.Message}");
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

                await _dbService.EnsureDatabaseSchemaAsync();

                // Prepare paths
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");

                // Ensure WAL is flushed before reading DB
                await FlushSqliteWalAsync(dbPath);

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

                // Target name
                var targetDir = GetDefaultArchiveFolder();
                var customNameRaw = ArchiveNameEntry.Text?.Trim();
                var safeName = string.IsNullOrWhiteSpace(customNameRaw)
                    ? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : SanitizeFileName(customNameRaw);
                var outName = safeName.EndsWith(".fbk", StringComparison.OrdinalIgnoreCase) ? safeName : safeName + ".fbk";

                // Create ZIP in a temporary file on background thread to avoid blocking UI
                var tempZip = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.fbk");

                await Task.Run(() =>
                {
                    try
                    {
                        using var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create);
                        zip.CreateEntryFromFile(dbPath, "database/foodbookapp.db");
                        if (File.Exists(dbPath + "-shm")) zip.CreateEntryFromFile(dbPath + "-shm", "database/foodbookapp.db-shm");
                        if (File.Exists(dbPath + "-wal")) zip.CreateEntryFromFile(dbPath + "-wal", "database/foodbookapp.db-wal");
                        zip.CreateEntryFromFile(prefsExport, "prefs.json");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error creating zip in background: {ex.Message}");
                        throw;
                    }
                });

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
                }
                catch (Exception directEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Direct save failed, will use picker: {directEx.Message}");
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
                }

                // Cleanup temp
                try { File.Delete(tempZip); } catch { }

                StatusLabel.Text = string.Format(GetLocalizedText("DataArchivizationPageResources", "SavedMessage", "Saved: {0}"), outName);

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

                // Import in correct order (respecting foreign keys)
                await ImportFoldersAsync(srcConnection, context, existingTables, log);
                await ImportRecipeLabelsAsync(srcConnection, context, existingTables, log);
                await ImportIngredientsAsync(srcConnection, context, existingTables, log);
                await ImportRecipesAsync(srcConnection, context, existingTables, log);
                await ImportRecipeRecipeLabelAsync(srcConnection, context, existingTables, log);
                await ImportPlansAsync(srcConnection, context, existingTables, log);
                await ImportPlannedMealsAsync(srcConnection, context, existingTables, log);
                await ImportShoppingListItemsAsync(srcConnection, context, existingTables, log);

                log("All data imported successfully");
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

        private async Task ImportFoldersAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                
                while (await reader.ReadAsync())
                {
                    var folder = new Folder
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = !reader.IsDBNull(reader.GetOrdinal("Description")) ? reader.GetString(reader.GetOrdinal("Description")) : null,
                        ParentFolderId = !reader.IsDBNull(reader.GetOrdinal("ParentFolderId")) ? reader.GetInt32(reader.GetOrdinal("ParentFolderId")) : null,
                        Order = HasColumn(reader, "Order") && !reader.IsDBNull(reader.GetOrdinal("Order")) ? reader.GetInt32(reader.GetOrdinal("Order")) : 0,
                        CreatedAt = HasColumn(reader, "CreatedAt") && !reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? reader.GetDateTime(reader.GetOrdinal("CreatedAt")) : DateTime.Now
                    };
                    folders.Add(folder);
                }

                foreach (var folder in folders)
                {
                    context.Folders.Add(folder);
                }
                await context.SaveChangesAsync();
                log($"Imported {folders.Count} folders");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import Folders: {ex.Message}");
            }
        }

        private async Task ImportRecipeLabelsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                
                while (await reader.ReadAsync())
                {
                    var label = new RecipeLabel
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        ColorHex = HasColumn(reader, "ColorHex") && !reader.IsDBNull(reader.GetOrdinal("ColorHex")) ? reader.GetString(reader.GetOrdinal("ColorHex")) : null,
                        CreatedAt = HasColumn(reader, "CreatedAt") && !reader.IsDBNull(reader.GetOrdinal("CreatedAt")) ? reader.GetDateTime(reader.GetOrdinal("CreatedAt")) : DateTime.Now
                    };
                    labels.Add(label);
                }

                foreach (var label in labels)
                {
                    context.RecipeLabels.Add(label);
                }
                await context.SaveChangesAsync();
                log($"Imported {labels.Count} recipe labels");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import RecipeLabels: {ex.Message}");
            }
        }

        private async Task ImportIngredientsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                cmd.CommandText = "SELECT * FROM Ingredients WHERE RecipeId IS NULL"; // Import standalone ingredients first
                using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var ingredient = new Ingredient
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
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
                }

                foreach (var ingredient in ingredients)
                {
                    context.Ingredients.Add(ingredient);
                }
                await context.SaveChangesAsync();
                log($"Imported {ingredients.Count} standalone ingredients");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import standalone Ingredients: {ex.Message}");
            }
        }

        private async Task ImportRecipesAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                
                while (await reader.ReadAsync())
                {
                    var recipe = new Recipe
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Description = !reader.IsDBNull(reader.GetOrdinal("Description")) ? reader.GetString(reader.GetOrdinal("Description")) : null,
                        Calories = reader.GetDouble(reader.GetOrdinal("Calories")),
                        Protein = reader.GetDouble(reader.GetOrdinal("Protein")),
                        Fat = reader.GetDouble(reader.GetOrdinal("Fat")),
                        Carbs = reader.GetDouble(reader.GetOrdinal("Carbs")),
                        IloscPorcji = reader.GetInt32(reader.GetOrdinal("IloscPorcji")),
                        FolderId = HasColumn(reader, "FolderId") && !reader.IsDBNull(reader.GetOrdinal("FolderId")) ? reader.GetInt32(reader.GetOrdinal("FolderId")) : null
                    };
                    recipes.Add(recipe);
                }

                // Import recipes first
                foreach (var recipe in recipes)
                {
                    context.Recipes.Add(recipe);
                }
                await context.SaveChangesAsync();
                log($"Imported {recipes.Count} recipes");

                // Now import recipe ingredients
                await ImportRecipeIngredientsAsync(srcConnection, context, log);
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import Recipes: {ex.Message}");
            }
        }

        private async Task ImportRecipeIngredientsAsync(SqliteConnection srcConnection, AppDbContext context, Action<string> log)
        {
            try
            {
                var ingredients = new List<Ingredient>();
                using var cmd = srcConnection.CreateCommand();
                cmd.CommandText = "SELECT * FROM Ingredients WHERE RecipeId IS NOT NULL";
                using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var ingredient = new Ingredient
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Name = reader.GetString(reader.GetOrdinal("Name")),
                        Quantity = reader.GetDouble(reader.GetOrdinal("Quantity")),
                        Unit = (Unit)reader.GetInt32(reader.GetOrdinal("Unit")),
                        UnitWeight = HasColumn(reader, "UnitWeight") && !reader.IsDBNull(reader.GetOrdinal("UnitWeight")) ? reader.GetDouble(reader.GetOrdinal("UnitWeight")) : 1.0,
                        Calories = reader.GetDouble(reader.GetOrdinal("Calories")),
                        Protein = reader.GetDouble(reader.GetOrdinal("Protein")),
                        Fat = reader.GetDouble(reader.GetOrdinal("Fat")),
                        Carbs = reader.GetDouble(reader.GetOrdinal("Carbs")),
                        RecipeId = reader.GetInt32(reader.GetOrdinal("RecipeId"))
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

        private async Task ImportRecipeRecipeLabelAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                        links.Add((reader.GetInt32(recipeIdIndex), reader.GetInt32(labelIdIndex)));
                    }
                }

                // Add links using raw SQL to avoid EF navigation complexity
                using var linkCmd = context.Database.GetDbConnection().CreateCommand();
                linkCmd.CommandText = "INSERT OR IGNORE INTO RecipeRecipeLabel (RecipesId, LabelsId) VALUES (@recipeId, @labelId)";
                var recipeParam = linkCmd.CreateParameter();
                recipeParam.ParameterName = "@recipeId";
                linkCmd.Parameters.Add(recipeParam);
                var labelParam = linkCmd.CreateParameter();
                labelParam.ParameterName = "@labelId";
                linkCmd.Parameters.Add(labelParam);

                await context.Database.OpenConnectionAsync();
                foreach (var (recipeId, labelId) in links)
                {
                    recipeParam.Value = recipeId;
                    labelParam.Value = labelId;
                    try
                    {
                        await linkCmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        log($"WARNING: Failed to link recipe {recipeId} to label {labelId}: {ex.Message}");
                    }
                }
                await context.Database.CloseConnectionAsync();
                log($"Imported {links.Count} recipe-label links");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import RecipeRecipeLabel: {ex.Message}");
            }
        }

        private async Task ImportPlansAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                
                while (await reader.ReadAsync())
                {
                    var plan = new Plan
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        StartDate = reader.GetDateTime(reader.GetOrdinal("StartDate")),
                        EndDate = reader.GetDateTime(reader.GetOrdinal("EndDate")),
                        IsArchived = HasColumn(reader, "IsArchived") && !reader.IsDBNull(reader.GetOrdinal("IsArchived")) ? reader.GetBoolean(reader.GetOrdinal("IsArchived")) : false,
                        // Handle new fields with defaults
                        Type = HasColumn(reader, "Type") && !reader.IsDBNull(reader.GetOrdinal("Type")) ? (PlanType)reader.GetInt32(reader.GetOrdinal("Type")) : PlanType.ShoppingList,
                        LinkedShoppingListPlanId = HasColumn(reader, "LinkedShoppingListPlanId") && !reader.IsDBNull(reader.GetOrdinal("LinkedShoppingListPlanId")) ? reader.GetInt32(reader.GetOrdinal("LinkedShoppingListPlanId")) : null
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

        private async Task ImportPlannedMealsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                
                while (await reader.ReadAsync())
                {
                    var meal = new PlannedMeal
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        RecipeId = reader.GetInt32(reader.GetOrdinal("RecipeId")),
                        Date = reader.GetDateTime(reader.GetOrdinal("Date")),
                        Portions = reader.GetInt32(reader.GetOrdinal("Portions")),
                        PlanId = HasColumn(reader, "PlanId") && !reader.IsDBNull(reader.GetOrdinal("PlanId")) ? reader.GetInt32(reader.GetOrdinal("PlanId")) : null
                    };
                    meals.Add(meal);
                }

                foreach (var meal in meals)
                {
                    context.PlannedMeals.Add(meal);
                }
                await context.SaveChangesAsync();
                log($"Imported {meals.Count} planned meals");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import PlannedMeals: {ex.Message}");
            }
        }

        private async Task ImportShoppingListItemsAsync(SqliteConnection srcConnection, AppDbContext context, HashSet<string> existingTables, Action<string> log)
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
                
                while (await reader.ReadAsync())
                {
                    var item = new ShoppingListItem
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        PlanId = reader.GetInt32(reader.GetOrdinal("PlanId")),
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
                log($"Imported {items.Count} shopping list items");
            }
            catch (Exception ex)
            {
                log($"WARNING: Failed to import ShoppingListItems: {ex.Message}");
            }
        }

        private bool HasColumn(SqliteDataReader reader, string columnName)
        {
            try
            {
                reader.GetOrdinal(columnName);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
