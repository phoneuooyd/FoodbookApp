using Foodbook.Models;

namespace Foodbook.Services;

/// <summary>
/// Implementation of preferences service using Microsoft.Maui.Storage.Preferences
/// </summary>
public class PreferencesService : IPreferencesService
{
    private const string SelectedCultureKey = "SelectedCulture";
    private const string SelectedThemeKey = "SelectedTheme";
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
}