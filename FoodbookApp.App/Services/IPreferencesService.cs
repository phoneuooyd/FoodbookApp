using Foodbook.Models;

namespace Foodbook.Services;

/// <summary>
/// Service for managing application preferences and settings
/// </summary>
public interface IPreferencesService
{
    /// <summary>
    /// Gets the saved language culture preference
    /// </summary>
    /// <returns>The saved culture code or empty string if not set</returns>
    string GetSavedLanguage();
    
    /// <summary>
    /// Saves the selected language culture preference
    /// </summary>
    /// <param name="cultureCode">The culture code to save (e.g., "en", "pl-PL")</param>
    void SaveLanguage(string cultureCode);
    
    /// <summary>
    /// Gets the list of supported culture codes
    /// </summary>
    /// <returns>Array of supported culture codes</returns>
    string[] GetSupportedCultures();
    
    /// <summary>
    /// Gets the saved theme preference
    /// </summary>
    /// <returns>The saved theme or System if not set</returns>
    Foodbook.Models.AppTheme GetSavedTheme();
    
    /// <summary>
    /// Saves the selected theme preference
    /// </summary>
    /// <param name="theme">The theme to save</param>
    void SaveTheme(Foodbook.Models.AppTheme theme);
    
    /// <summary>
    /// Gets the saved font family preference
    /// </summary>
    /// <returns>The saved font family or Default if not set</returns>
    AppFontFamily GetSavedFontFamily();
    
    /// <summary>
    /// Saves the selected font family preference
    /// </summary>
    /// <param name="fontFamily">The font family to save</param>
    void SaveFontFamily(AppFontFamily fontFamily);
    
    /// <summary>
    /// Gets the saved font size preference
    /// </summary>
    /// <returns>The saved font size or Default if not set</returns>
    AppFontSize GetSavedFontSize();
    
    /// <summary>
    /// Saves the selected font size preference
    /// </summary>
    /// <param name="fontSize">The font size to save</param>
    void SaveFontSize(AppFontSize fontSize);
    
    /// <summary>
    /// Gets the complete font settings
    /// </summary>
    /// <returns>FontSettings object with current preferences</returns>
    FontSettings GetFontSettings();
    
    /// <summary>
    /// Saves the complete font settings
    /// </summary>
    /// <param name="settings">The font settings to save</param>
    void SaveFontSettings(FontSettings settings);
}