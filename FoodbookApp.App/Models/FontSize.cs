namespace Foodbook.Models
{
    /// <summary>
    /// Represents the available font sizes for the application
    /// </summary>
    public enum AppFontSize
    {
        /// <summary>
        /// Extra small font size (8pt) for very compact displays
        /// </summary>
        ExtraSmall,

        /// <summary>
        /// Very small font size (11pt) for compact displays
        /// </summary>
        VerySmall,

        /// <summary>
        /// Small font size (12-14pt) for compact displays
        /// </summary>
        Small,
        
        /// <summary>
        /// Default font size (16-18pt) - recommended for most users
        /// </summary>
        Default,
        
        /// <summary>
        /// Large font size (20-22pt) for better readability
        /// </summary>
        Large,
        
        /// <summary>
        /// Extra large font size (24-26pt) for accessibility needs
        /// </summary>
        ExtraLarge
    }
}