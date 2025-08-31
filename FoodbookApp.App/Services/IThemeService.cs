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
    }
}