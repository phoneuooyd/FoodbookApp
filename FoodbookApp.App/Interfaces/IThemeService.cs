using Foodbook.Models;

namespace FoodbookApp.Interfaces
{
    public interface IThemeService
    {
        /// <summary>
        /// Raised after theme or color palette is applied to application resources
        /// </summary>
        event EventHandler? ThemeChanged;

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

        /// <summary>
        /// Refreshes system status/navigation bar colors after external changes
        /// </summary>
        void UpdateSystemBars();

        /// <summary>
        /// Sets whether to use colorful page backgrounds instead of neutral gray
        /// </summary>
        /// <param name="useColorfulBackground">True for colorful, false for gray</param>
        void SetColorfulBackground(bool useColorfulBackground);
        
        /// <summary>
        /// Gets whether colorful page backgrounds are enabled
        /// </summary>
        /// <returns>True if colorful backgrounds are enabled</returns>
        bool GetIsColorfulBackgroundEnabled();

        /// <summary>
        /// Enables or disables the wallpaper background option
        /// </summary>
        /// <param name="isEnabled">True to enable wallpaper background, false to disable</param>
        void EnableWallpaperBackground(bool isEnabled);

        /// <summary>
        /// Checks if the wallpaper background option is enabled
        /// </summary>
        /// <returns>True if wallpaper background is enabled</returns>
        bool IsWallpaperBackgroundEnabled();

        /// <summary>
        /// Returns whether wallpapers are available for a given color theme.
        /// A theme is considered supported if at least a single wallpaper file is defined
        /// (either a single file for both modes or light/dark variants).
        /// </summary>
        /// <param name="colorTheme">Color theme to check</param>
        /// <returns>True if wallpaper is available for the theme</returns>
        bool IsWallpaperAvailableFor(AppColorTheme colorTheme);
    }
}