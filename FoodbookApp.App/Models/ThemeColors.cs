namespace Foodbook.Models
{
    public class ThemeColors
    {
        public string Name { get; set; } = string.Empty;
        
        // Light theme colors
        public Color PrimaryLight { get; set; }
        public Color SecondaryLight { get; set; }
        public Color TertiaryLight { get; set; }
        public Color AccentLight { get; set; }
        public Color PrimaryTextLight { get; set; }
        public Color SecondaryTextLight { get; set; }
        
        // Dark theme colors
        public Color PrimaryDark { get; set; }
        public Color SecondaryDark { get; set; }
        public Color TertiaryDark { get; set; }
        public Color AccentDark { get; set; }
        public Color PrimaryTextDark { get; set; }
        public Color SecondaryTextDark { get; set; }
    }
}