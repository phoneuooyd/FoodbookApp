using System.IO;
using System.IO.Compression;
using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using FoodbookApp.Interfaces;
using Microsoft.Data.Sqlite;

namespace Foodbook.Views
{
    public partial class DataArchivizationPage : ContentPage
    {
        private readonly IDatabaseService _dbService;
        private readonly IPreferencesService _prefs;

        private bool _isExporting;

        private static readonly string[] ArchiveExtensions = new[] { ".fbk", ".zip" };

        private record ArchiveItem(string FileName, string FullPath, DateTime ModifiedUtc)
        {
            public string ModifiedDisplay => $"{ModifiedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
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
            LoadArchivesList();
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
                    result.Add(Path.Combine(downloads, "Foodbook"));
                }

                // Some devices/localizations may use alternative casing or alias; include common variants
                var storageRoot = "/storage/emulated/0";
                var altDownloads = Path.Combine(storageRoot, "Download");
                result.Add(altDownloads);
                result.Add(Path.Combine(altDownloads, "Foodbook"));
                // Common roots shown by file managers as "Urz¹dzenie" (primary storage)
                var roots = new List<string?>
                {
                    Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath, // often /storage/emulated/0
                    "/sdcard",
                    "/storage/self/primary",
                    "/storage/emulated/0"
                };

                foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    // Handle both Download and Downloads variants
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

        private void LoadArchivesList()
        {
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
                        items.AddRange(Directory.EnumerateFiles(folder, "*.fbk", SearchOption.TopDirectoryOnly)
                            .Concat(Directory.EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly))
                            .Select(p => new ArchiveItem(Path.GetFileName(p), p, File.GetLastWriteTimeUtc(p))));
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
                        .Select(p => new ArchiveItem(Path.GetFileName(p), p, File.GetLastWriteTimeUtc(p)))
                        .ToList()
                    : new List<ArchiveItem>();
#endif
                ArchivesCollection.ItemsSource = items
                    .GroupBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderByDescending(i => i.ModifiedUtc)
                    .ToList();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error loading list: {ex.Message}";
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

        private static bool IsArchivePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try { return ArchiveExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase); }
            catch { return false; }
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
                        var item = new ArchiveItem(Path.GetFileName(p), p, File.GetLastWriteTimeUtc(p));
                        current.RemoveAll(x => string.Equals(x.FullPath, p, StringComparison.OrdinalIgnoreCase));
                        current.Add(item);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Manual add failed for {p}: {ex.Message}");
                    }
                }

                ArchivesCollection.ItemsSource = current
                    .GroupBy(i => i.FullPath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderByDescending(i => i.ModifiedUtc)
                    .ToList();

                StatusLabel.Text = GetLocalizedText("DataArchivizationPageResources", "ManualSearchDone", "Selected archives added to the list.");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Manual search failed: {ex.Message}", "OK");
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

                // Prepare files
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");

                // Ensure all recent changes are checkpointed from WAL to DB before zipping
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
                File.WriteAllText(prefsExport, json);

                // Target name
                var targetDir = GetDefaultArchiveFolder();
                var customNameRaw = ArchiveNameEntry.Text?.Trim();
                var safeName = string.IsNullOrWhiteSpace(customNameRaw)
                    ? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : SanitizeFileName(customNameRaw);
                var outName = safeName.EndsWith(".fbk", StringComparison.OrdinalIgnoreCase) ? safeName : safeName + ".fbk";

                // Create ZIP in a temporary file first
                var tempZip = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}.fbk");
                using (var zip = ZipFile.Open(tempZip, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(dbPath, "database/foodbookapp.db");
                    if (File.Exists(dbPath + "-shm")) zip.CreateEntryFromFile(dbPath + "-shm", "database/foodbookapp.db-shm");
                    if (File.Exists(dbPath + "-wal")) zip.CreateEntryFromFile(dbPath + "-wal", "database/foodbookapp.db-wal");
                    zip.CreateEntryFromFile(prefsExport, "prefs.json");
                }

                bool saved = false;
                string? outPath = null;

                // Try direct save to default persistent folder
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
                    File.Copy(tempZip, outPath, overwrite: false);
                    saved = true;
                }
                catch (Exception directEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Direct save failed, will use picker: {directEx.Message}");
                    saved = false;
                }

                // Fallback to file saver picker (persists outside sandbox)
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

                StatusLabel.Text = $"Saved: {outName}";
                LoadArchivesList();

                await DisplayAlert("OK", $"{outName} {GetLocalizedText("DataArchivizationPageResources", "ArchiveCreatedSuffix", defaultText: "created successfully")}", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Export failed: {ex.Message}", "OK");
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
                    await DisplayAlert("Info", GetLocalizedText("DataArchivizationPageResources", "NoArchivesInDefault", "No archives in default folder. Please pick a file."), "OK");
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
                await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
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
                if (string.IsNullOrWhiteSpace(cancel)) cancel = "Cancel";

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
                        await DisplayAlert("Error", $"Delete failed: {ex.Message}", "OK");
                    }
                }
            }
        }

        private async Task RestoreFromPathAsync(string archivePath)
        {
            try
            {
                var tmpDir = Path.Combine(FileSystem.CacheDirectory, "fbk_import");
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);

                using (var stream = File.OpenRead(archivePath))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    zip.ExtractToDirectory(tmpDir, overwriteFiles: true);
                }

                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");
                var backup = dbPath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                if (File.Exists(dbPath)) File.Move(dbPath, backup);

                // Clean current WAL/SHM
                if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
                if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");

                var srcDbDir = Path.Combine(tmpDir, "database");
                var srcDb = Path.Combine(srcDbDir, "foodbookapp.db");
                var srcShm = Path.Combine(srcDbDir, "foodbookapp.db-shm");
                var srcWal = Path.Combine(srcDbDir, "foodbookapp.db-wal");

                File.Copy(srcDb, dbPath, overwrite: true);
                if (File.Exists(srcShm)) File.Copy(srcShm, dbPath + "-shm", overwrite: true);
                if (File.Exists(srcWal)) File.Copy(srcWal, dbPath + "-wal", overwrite: true);

                var prefsJson = Path.Combine(tmpDir, "prefs.json");
                if (File.Exists(prefsJson))
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
                    }
                }

                // Open and run a quick checkpoint to make sure DB is clean after copy
                await FlushSqliteWalAsync(dbPath);

                await _dbService.MigrateDatabaseAsync();
                StatusLabel.Text = GetLocalizedText("DataArchivizationPageResources", "RestoreSuccess", "Restored. Please restart app.");
            }
            catch (Exception ioex)
            {
                await DisplayAlert("Error", $"Restore failed: {ioex.Message}", "OK");
            }
        }

        private record PrefsDto(string Culture, Foodbook.Models.AppTheme Theme, Foodbook.Models.AppColorTheme ColorTheme,
                                bool ColorfulBackground, bool WallpaperBackground,
                                Foodbook.Models.AppFontFamily FontFamily, Foodbook.Models.AppFontSize FontSize);
    }
}
