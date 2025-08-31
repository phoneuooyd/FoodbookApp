namespace Foodbook.Models
{
    /// <summary>
    /// Represents the available font families for the application
    /// </summary>
    public enum AppFontFamily
    {
        /// <summary>
        /// System default font (Roboto on Android, SF Pro on iOS, Segoe UI on Windows)
        /// </summary>
        Default,
        
        /// <summary>
        /// Sans-serif font family for better readability
        /// </summary>
        SansSerif,
        
        /// <summary>
        /// Serif font family for elegant appearance
        /// </summary>
        Serif,
        
        /// <summary>
        /// Monospace font family for code and data display
        /// </summary>
        Monospace
    }
}