using System.IO;
using System.IO.Compression;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using FoodbookApp.Interfaces;

namespace Foodbook.Views;

public partial class DataArchivizationPage : ContentPage
{
    private readonly IDatabaseService _dbService;
    private readonly IPreferencesService _prefs;

    private bool _isExporting;

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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DefaultFolderPathLabel.Text = GetDefaultArchiveFolder();
        LoadArchivesList();
    }

    private string GetDefaultArchiveFolder()
    {
#if ANDROID
        var downloads = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath ?? FileSystem.AppDataDirectory;
        var folder = Path.Combine(downloads, "Foodbook");
        Directory.CreateDirectory(folder);
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

    private void LoadArchivesList()
    {
        try
        {
            var folder = GetDefaultArchiveFolder();
            var items = Directory.EnumerateFiles(folder, "*.fbk", SearchOption.TopDirectoryOnly)
                                 .Concat(Directory.EnumerateFiles(folder, "*.zip", SearchOption.TopDirectoryOnly))
                                 .Select(p => new ArchiveItem(Path.GetFileName(p), p, File.GetLastWriteTimeUtc(p)))
                                 .OrderByDescending(i => i.ModifiedUtc)
                                 .ToList();
            ArchivesCollection.ItemsSource = items;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading list: {ex.Message}";
        }
    }

    private async void OnRefreshListClicked(object sender, EventArgs e)
    {
        LoadArchivesList();
        await Task.CompletedTask;
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

            // Target folder and name
            var targetDir = GetDefaultArchiveFolder();
            var customNameRaw = ArchiveNameEntry.Text?.Trim();
            var safeName = string.IsNullOrWhiteSpace(customNameRaw)
                ? $"backup_{DateTime.Now:yyyyMMdd_HHmmss}"
                : SanitizeFileName(customNameRaw);
            var outName = safeName.EndsWith(".fbk", StringComparison.OrdinalIgnoreCase) ? safeName : safeName + ".fbk";
            var outPath = Path.Combine(targetDir, outName);
            if (File.Exists(outPath))
            {
                outName = $"{Path.GetFileNameWithoutExtension(outName)}_{DateTime.Now:HHmmss}.fbk";
                outPath = Path.Combine(targetDir, outName);
            }

            using (var zip = ZipFile.Open(outPath, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(dbPath, "database/foodbookapp.db");
                if (File.Exists(dbPath + "-shm")) zip.CreateEntryFromFile(dbPath + "-shm", "database/foodbookapp.db-shm");
                if (File.Exists(dbPath + "-wal")) zip.CreateEntryFromFile(dbPath + "-wal", "database/foodbookapp.db-wal");
                zip.CreateEntryFromFile(prefsExport, "prefs.json");
            }

            StatusLabel.Text = $"Saved: {outName}";
            LoadArchivesList();

            // Success alert localized if possible
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
            var latest = Directory.EnumerateFiles(folder, "*.fbk").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (latest == null)
            {
                await DisplayAlert("Info", GetLocalizedText("DataArchivizationPageResources", "NoArchivesInDefault", "No archives in default folder. Please pick a file."), "OK");
                var result = await FilePicker.PickAsync();
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
                zip.ExtractToDirectory(tmpDir);
            }

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");
            var backup = dbPath + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            if (File.Exists(dbPath)) File.Move(dbPath, backup);
            if (File.Exists(dbPath + "-shm")) File.Delete(dbPath + "-shm");
            if (File.Exists(dbPath + "-wal")) File.Delete(dbPath + "-wal");
            var srcDb = Path.Combine(tmpDir, "database", "foodbookapp.db");
            File.Copy(srcDb, dbPath, overwrite: true);

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
