using Foodbook.Models;

namespace Foodbook.Services;

/// <summary>
/// Service for managing font settings and dynamic font changes
/// </summary>
public interface IFontService
{
    /// <summary>
    /// Event raised when font settings change
    /// </summary>
    event EventHandler<FontSettingsChangedEventArgs> FontSettingsChanged;
    
    /// <summary>
    /// Gets the current font settings
    /// </summary>
    FontSettings CurrentFontSettings { get; }
    
    /// <summary>
    /// Gets all available font families
    /// </summary>
    AppFontFamily[] GetAvailableFontFamilies();
    
    /// <summary>
    /// Gets all available font sizes
    /// </summary>
    AppFontSize[] GetAvailableFontSizes();
    
    /// <summary>
    /// Sets the font family and applies it globally
    /// </summary>
    /// <param name="fontFamily">The font family to apply</param>
    void SetFontFamily(AppFontFamily fontFamily);
    
    /// <summary>
    /// Sets the font size and applies it globally
    /// </summary>
    /// <param name="fontSize">The font size to apply</param>
    void SetFontSize(AppFontSize fontSize);
    
    /// <summary>
    /// Sets the complete font settings and applies them globally
    /// </summary>
    /// <param name="settings">The font settings to apply</param>
    void SetFontSettings(FontSettings settings);
    
    /// <summary>
    /// Loads saved font settings from preferences
    /// </summary>
    void LoadSavedSettings();
    
    /// <summary>
    /// Applies the current font settings to the application resources
    /// </summary>
    void ApplyFontSettings();
}

/// <summary>
/// Event arguments for font settings changes
/// </summary>
public class FontSettingsChangedEventArgs : EventArgs
{
    public FontSettings OldSettings { get; }
    public FontSettings NewSettings { get; }
    
    public FontSettingsChangedEventArgs(FontSettings oldSettings, FontSettings newSettings)
    {
        OldSettings = oldSettings;
        NewSettings = newSettings;
    }
}