using Foodbook.Models;

namespace Foodbook.Services;

/// <summary>
/// Implementation of preferences service using Microsoft.Maui.Storage.Preferences
/// </summary>
public class PreferencesService : IPreferencesService
{
    private const string SelectedCultureKey = "SelectedCulture";
    private const string SelectedThemeKey = "SelectedTheme";
    private const string SelectedFontFamilyKey = "SelectedFontFamily";
    private const string SelectedFontSizeKey = "SelectedFontSize";
    
    private static readonly string[] SupportedCultures = { "en", "pl-PL" };

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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PreferencesService] Failed to save theme preference: {ex.Message}");
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
}