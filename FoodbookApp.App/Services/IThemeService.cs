using Foodbook.Models;

namespace Foodbook.Services
{
    public interface IThemeService
    {
        /// <summary>
        /// Sets the application theme
        /// </summary>
        /// <param name="theme">The theme to apply</param>
        void SetTheme(Foodbook.Models.AppTheme theme);
        
        /// <summary>
        /// Gets the current theme
        /// </summary>
        /// <returns>The current theme</returns>
        Foodbook.Models.AppTheme GetCurrentTheme();
        
        /// <summary>
        /// Sets the application color theme
        /// </summary>
        /// <param name="colorTheme">The color theme to apply</param>
        void SetColorTheme(AppColorTheme colorTheme);
        
        /// <summary>
        /// Gets the current color theme
        /// </summary>
        /// <returns>The current color theme</returns>
        AppColorTheme GetCurrentColorTheme();
        
        /// <summary>
        /// Gets available color themes
        /// </summary>
        /// <returns>Dictionary of available color themes</returns>
        Dictionary<AppColorTheme, ThemeColors> GetAvailableColorThemes();
        
        /// <summary>
        /// Gets the theme colors for a specific color theme
        /// </summary>
        /// <param name="colorTheme">The color theme</param>
        /// <returns>Theme colors</returns>
        ThemeColors GetThemeColors(AppColorTheme colorTheme);
    }
}