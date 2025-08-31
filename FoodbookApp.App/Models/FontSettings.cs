namespace Foodbook.Models
{
    /// <summary>
    /// Represents the font settings configuration for the application
    /// </summary>
    public class FontSettings
    {
        /// <summary>
        /// The selected font family
        /// </summary>
        public AppFontFamily FontFamily { get; set; } = AppFontFamily.Default;
        
        /// <summary>
        /// The selected font size
        /// </summary>
        public AppFontSize FontSize { get; set; } = AppFontSize.Default;
        
        /// <summary>
        /// Gets the display name for the font family
        /// </summary>
        public string FontFamilyDisplayName => FontFamily switch
        {
            AppFontFamily.Default => "System Default",
            AppFontFamily.SansSerif => "Sans-serif",
            AppFontFamily.Serif => "Serif", 
            AppFontFamily.Monospace => "Monospace",
            _ => "Unknown"
        };
        
        /// <summary>
        /// Gets the display name for the font size
        /// </summary>
        public string FontSizeDisplayName => FontSize switch
        {
            AppFontSize.Small => "Small",
            AppFontSize.Default => "Default",
            AppFontSize.Large => "Large",
            AppFontSize.ExtraLarge => "Extra Large",
            _ => "Unknown"
        };
        
        /// <summary>
        /// Gets the actual font family name for MAUI
        /// </summary>
        public string? PlatformFontFamily => FontFamily switch
        {
            AppFontFamily.Default => null, // Use system default
            AppFontFamily.SansSerif => GetPlatformSansSerif(),
            AppFontFamily.Serif => GetPlatformSerif(),
            AppFontFamily.Monospace => GetPlatformMonospace(),
            _ => null
        };
        
        /// <summary>
        /// Gets the actual font size value for MAUI
        /// </summary>
        public double PlatformFontSize => FontSize switch
        {
            AppFontSize.Small => 13,
            AppFontSize.Default => 16,
            AppFontSize.Large => 20,
            AppFontSize.ExtraLarge => 24,
            _ => 16
        };
        
        private static string? GetPlatformSansSerif()
        {
#if ANDROID
            return "sans-serif";
#elif IOS
            return "Helvetica";
#elif WINDOWS
            return "Segoe UI";
#else
            return null;
#endif
        }
        
        private static string? GetPlatformSerif()
        {
#if ANDROID
            return "serif";
#elif IOS
            return "Times New Roman";
#elif WINDOWS
            return "Times New Roman";
#else
            return null;
#endif
        }
        
        private static string? GetPlatformMonospace()
        {
#if ANDROID
            return "monospace";
#elif IOS
            return "Courier";
#elif WINDOWS
            return "Consolas";
#else
            return null;
#endif
        }
        
        /// <summary>
        /// Creates a copy of the current font settings
        /// </summary>
        public FontSettings Clone()
        {
            return new FontSettings
            {
                FontFamily = this.FontFamily,
                FontSize = this.FontSize
            };
        }
        
        /// <summary>
        /// Checks if two font settings are equal
        /// </summary>
        public bool Equals(FontSettings? other)
        {
            if (other == null) return false;
            return FontFamily == other.FontFamily && FontSize == other.FontSize;
        }
        
        public override bool Equals(object? obj)
        {
            return Equals(obj as FontSettings);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(FontFamily, FontSize);
        }
    }
}