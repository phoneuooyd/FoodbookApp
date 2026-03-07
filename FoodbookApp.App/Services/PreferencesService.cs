using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Foodbook.Data;
using Foodbook.Models.DTOs;
using System.Threading;

namespace Foodbook.Services;

/// <summary>
/// Implementation of preferences service using Microsoft.Maui.Storage.Preferences
/// </summary>
public class PreferencesService : IPreferencesService
{
    private readonly IAuthTokenStore _tokenStore;
    private bool _suppressCloudSync; // Flag to prevent sync during cloud load
    private CancellationTokenSource? _syncDebounce;

    private const string SelectedCultureKey = "SelectedCulture";
    private const string SelectedThemeKey = "SelectedTheme";
    private const string SelectedColorThemeKey = "SelectedColorTheme";
    private const string ColorfulBackgroundEnabledKey = "ColorfulBackgroundEnabled"; // NEW
    private const string WallpaperEnabledKey = "WallpaperEnabled"; // NEW: wallpaper
    private const string SelectedFontFamilyKey = "SelectedFontFamily";
    private const string SelectedFontSizeKey = "SelectedFontSize";
    private const string IsFirstLaunchKey = "IsFirstLaunch";
    private const string InstallBasicIngredientsKey = "InstallBasicIngredients";
    private const string PlanChoiceKey = "PlanChoice";
    
    private static readonly string[] SupportedCultures = { "en", "pl-PL", "de-DE", "es-ES", "fr-FR", "ko-KR" };

    public PreferencesService(IAuthTokenStore tokenStore)
    {
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    }

    /// <inheritdoc/>
    public string GetSavedLanguage()
    {
        try
        {
            var savedCulture = Preferences.Get(SelectedCultureKey, string.Empty);
            
            if (!string.IsNullOrEmpty(savedCulture) && SupportedCultures.Contains(savedCulture))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved saved culture: {savedCulture}");
                return savedCulture;
            }
            
            System.Diagnostics.Debug.WriteLine("[PreferencesService] No valid saved culture found");
            return string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get saved language: {ex.Message}");
            return string.Empty;
        }
    }

    /// <inheritdoc/>
    public void SaveLanguage(string cultureCode)
    {
        try
        {
            if (string.IsNullOrEmpty(cultureCode) || !SupportedCultures.Contains(cultureCode))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Invalid culture code: {cultureCode}");
                return;
            }
            
            Preferences.Set(SelectedCultureKey, cultureCode);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved culture preference: {cultureCode}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save language preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public string[] GetSupportedCultures()
    {
        return SupportedCultures;
    }

    /// <inheritdoc/>
    public Foodbook.Models.AppTheme GetSavedTheme()
    {
        try
        {
            var savedThemeString = Preferences.Get(SelectedThemeKey, Foodbook.Models.AppTheme.System.ToString());
            
            if (Enum.TryParse<Foodbook.Models.AppTheme>(savedThemeString, out var theme))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved saved theme: {theme}");
                return theme;
            }
            
            System.Diagnostics.Debug.WriteLine("[PreferencesService] No valid saved theme found, using System");
            return Foodbook.Models.AppTheme.System;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get saved theme: {ex.Message}");
            return Foodbook.Models.AppTheme.System;
        }
    }

    /// <inheritdoc/>
    public void SaveTheme(Foodbook.Models.AppTheme theme)
    {
        try
        {
            Preferences.Set(SelectedThemeKey, theme.ToString());
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved theme preference: {theme}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save theme preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public AppColorTheme GetSavedColorTheme()
    {
        try
        {
            var savedColorThemeString = Preferences.Get(SelectedColorThemeKey, AppColorTheme.Default.ToString());
            
            if (Enum.TryParse<AppColorTheme>(savedColorThemeString, out var colorTheme))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved saved color theme: {colorTheme}");
                return colorTheme;
            }
            
            System.Diagnostics.Debug.WriteLine("[PreferencesService] No valid saved color theme found, using Default");
            return AppColorTheme.Default;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get saved color theme: {ex.Message}");
            return AppColorTheme.Default;
        }
    }

    /// <inheritdoc/>
    public void SaveColorTheme(AppColorTheme colorTheme)
    {
        try
        {
            Preferences.Set(SelectedColorThemeKey, colorTheme.ToString());
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved color theme preference: {colorTheme}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save color theme preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public bool GetIsColorfulBackgroundEnabled()
    {
        try
        {
            var isEnabled = Preferences.Get(ColorfulBackgroundEnabledKey, false); // Default to false (gray backgrounds)
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved colorful background setting: {isEnabled}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get colorful background setting: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public void SaveColorfulBackground(bool isEnabled)
    {
        try
        {
            Preferences.Set(ColorfulBackgroundEnabledKey, isEnabled);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved colorful background preference: {isEnabled}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save colorful background preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public bool GetIsWallpaperEnabled()
    {
        try
        {
            var isEnabled = Preferences.Get(WallpaperEnabledKey, false);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved wallpaper enabled: {isEnabled}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get wallpaper enabled: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public void SaveWallpaperEnabled(bool isEnabled)
    {
        try
        {
            Preferences.Set(WallpaperEnabledKey, isEnabled);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved wallpaper enabled: {isEnabled}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save wallpaper enabled: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public AppFontFamily GetSavedFontFamily()
    {
        try
        {
            var savedFontFamilyString = Preferences.Get(SelectedFontFamilyKey, AppFontFamily.Default.ToString());
            
            if (Enum.TryParse<AppFontFamily>(savedFontFamilyString, out var fontFamily))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved saved font family: {fontFamily}");
                return fontFamily;
            }
            
            System.Diagnostics.Debug.WriteLine("[PreferencesService] No valid saved font family found, using Default");
            return AppFontFamily.Default;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get saved font family: {ex.Message}");
            return AppFontFamily.Default;
        }
    }

    /// <inheritdoc/>
    public void SaveFontFamily(AppFontFamily fontFamily)
    {
        try
        {
            Preferences.Set(SelectedFontFamilyKey, fontFamily.ToString());
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved font family preference: {fontFamily}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save font family preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public AppFontSize GetSavedFontSize()
    {
        try
        {
            var savedFontSizeString = Preferences.Get(SelectedFontSizeKey, AppFontSize.Default.ToString());
            
            if (Enum.TryParse<AppFontSize>(savedFontSizeString, out var fontSize))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved saved font size: {fontSize}");
                return fontSize;
            }
            
            System.Diagnostics.Debug.WriteLine("[PreferencesService] No valid saved font size found, using Default");
            return AppFontSize.Default;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get saved font size: {ex.Message}");
            return AppFontSize.Default;
        }
    }

    /// <inheritdoc/>
    public void SaveFontSize(AppFontSize fontSize)
    {
        try
        {
            Preferences.Set(SelectedFontSizeKey, fontSize.ToString());
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved font size preference: {fontSize}");
            
            ScheduleCloudSync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save font size preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public FontSettings GetFontSettings()
    {
        try
        {
            var fontFamily = GetSavedFontFamily();
            var fontSize = GetSavedFontSize();
            
            var settings = new FontSettings
            {
                FontFamily = fontFamily,
                FontSize = fontSize
            };
            
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Retrieved font settings: {fontFamily}, {fontSize}");
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get font settings: {ex.Message}");
            return new FontSettings(); // Returns default settings
        }
    }

    /// <inheritdoc/>
    public void SaveFontSettings(FontSettings settings)
    {
        try
        {
            if (settings == null)
            {
                System.Diagnostics.Debug.WriteLine("[PreferencesService] Cannot save null font settings");
                return;
            }
            
            SaveFontFamily(settings.FontFamily);
            SaveFontSize(settings.FontSize);
            
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved complete font settings: {settings.FontFamily}, {settings.FontSize}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save font settings: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public bool IsFirstLaunch()
    {
        try
        {
            // Domylnie true - je¿eli nie ma zapisanej preferencji, to oznacza pierwszy start
            var isFirstLaunch = Preferences.Get(IsFirstLaunchKey, true);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Is first launch: {isFirstLaunch}");
            return isFirstLaunch;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to check first launch: {ex.Message}");
            return true; // W razie b³êdu, zak³adamy ¿e to pierwszy start
        }
    }

    /// <inheritdoc/>
    public void MarkInitialSetupCompleted()
    {
        try
        {
            Preferences.Set(IsFirstLaunchKey, false);
            System.Diagnostics.Debug.WriteLine("[PreferencesService] Marked initial setup as completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to mark setup completed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void ResetToFirstLaunch()
    {
        try
        {
            Preferences.Set(IsFirstLaunchKey, true);
            System.Diagnostics.Debug.WriteLine("[PreferencesService] Reset application to first launch state");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to reset to first launch: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public bool GetInstallBasicIngredients()
    {
        try
        {
            var install = Preferences.Get(InstallBasicIngredientsKey, true); // Default to true
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Install basic ingredients: {install}");
            return install;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to get install basic ingredients preference: {ex.Message}");
            return true;
        }
    }

    /// <inheritdoc/>
    public void SaveInstallBasicIngredients(bool install)
    {
        try
        {
            Preferences.Set(InstallBasicIngredientsKey, install);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved install basic ingredients preference: {install}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save install basic ingredients preference: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void SavePlanChoice(string planChoice)
    {
        try
        {
            var safeChoice = string.IsNullOrWhiteSpace(planChoice) ? "Free" : planChoice.Trim();
            Preferences.Set(PlanChoiceKey, safeChoice);
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Saved plan choice: {safeChoice}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save plan choice: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public string GetPlanChoice()
    {
        try
        {
            var choice = Preferences.Get(PlanChoiceKey, "Free");
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Loaded plan choice: {choice}");
            return string.IsNullOrWhiteSpace(choice) ? "Free" : choice;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to read plan choice: {ex.Message}");
            return "Free";
        }
    }

    /// <inheritdoc/>
    public void SuppressCloudSync()
    {
        _suppressCloudSync = true;
    }

    /// <inheritdoc/>
    public void ResumeCloudSync()
    {
        _suppressCloudSync = false;
    }

    private void ScheduleCloudSync()
    {
        _syncDebounce?.Cancel();
        _syncDebounce?.Dispose();
        _syncDebounce = new CancellationTokenSource();
        var ct = _syncDebounce.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(800, ct);
                await SyncToCloudAsync();
            }
            catch (OperationCanceledException)
            {
                // Debounced by newer preference change
            }
        }, ct);
    }

    /// <inheritdoc/>
    public void ResetAllToDefaults()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[PreferencesService] Clearing all preferences and setting first launch");
            Preferences.Clear();
            // Ensure first-launch flow on next app start
            Preferences.Set(IsFirstLaunchKey, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to reset all preferences to defaults: {ex.Message}");
        }
    }

    /// <summary>
    /// Automatically syncs current preferences to Supabase cloud if user is logged in.
    /// Fire-and-forget - errors are logged but not thrown.
    /// </summary>
    private async Task SyncToCloudAsync()
    {
        try
        {
            // Skip if sync is suppressed (loading from cloud)
            if (_suppressCloudSync)
            {
                System.Diagnostics.Debug.WriteLine("[PreferencesService] Cloud sync suppressed (loading from cloud)");
                return;
            }

            // Check if user is logged in
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[PreferencesService] No active account - skipping cloud sync");
                return;
            }

            using var scope = FoodbookApp.MauiProgram.ServiceProvider!.CreateScope();

            // Respect user choice: only sync preferences when cloud sync is enabled from ProfilePage checkbox
            var syncService = scope.ServiceProvider.GetService<ISupabaseSyncService>();
            if (syncService == null)
            {
                System.Diagnostics.Debug.WriteLine("[PreferencesService] Sync service not available - skipping cloud sync");
                return;
            }

            if (!await syncService.IsCloudSyncEnabledAsync())
            {
                System.Diagnostics.Debug.WriteLine("[PreferencesService] Cloud sync disabled - skipping user_preferences sync");
                return;
            }

            // Get Supabase user ID from account
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.AuthAccounts.FindAsync(accountId.Value);

            if (account == null || string.IsNullOrWhiteSpace(account.SupabaseUserId))
            {
                System.Diagnostics.Debug.WriteLine("[PreferencesService] Account not found or missing Supabase ID - skipping cloud sync");
                return;
            }

            if (!Guid.TryParse(account.SupabaseUserId, out var supabaseUserId))
            {
                System.Diagnostics.Debug.WriteLine($"[PreferencesService] Invalid Supabase user ID format: {account.SupabaseUserId}");
                return;
            }

            // Create DTO and queue for sync instead of direct API call
            var dto = new UserPreferencesDto
            {
                Id = supabaseUserId,
                Theme = GetSavedTheme().ToString(),
                ColorTheme = GetSavedColorTheme().ToString(),
                IsColorfulBackground = GetIsColorfulBackgroundEnabled(),
                IsWallpaperEnabled = GetIsWallpaperEnabled(),
                FontFamily = GetSavedFontFamily().ToString(),
                FontSize = GetSavedFontSize().ToString(),
                Language = GetSavedLanguage(),
                UpdatedAt = DateTime.UtcNow
            };

            // Queue for sync through normal sync queue
            await syncService.QueueForSyncAsync(dto, SyncOperationType.Insert);
            System.Diagnostics.Debug.WriteLine("[PreferencesService] ? Auto-synced preferences to cloud (queued for sync)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Cloud sync failed (non-fatal): {ex.Message}");
            // Don't throw - local preferences are already saved
        }
    }
}