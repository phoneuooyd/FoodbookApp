namespace Foodbook.Services;

/// <summary>
/// Implementation of preferences service using Microsoft.Maui.Storage.Preferences
/// </summary>
public class PreferencesService : IPreferencesService
{
    private const string SelectedCultureKey = "SelectedCulture";
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
}