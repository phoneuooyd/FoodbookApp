using System;
using System.Text.Json.Serialization;

namespace Foodbook.Models.DTOs
{
    /// <summary>
    /// DTO for user preferences stored in Supabase.
    /// Plain POCO used with REST API to avoid Supabase ORM serialization issues.
    /// </summary>
    public class UserPreferencesDto
    {
        [JsonPropertyName("id")] 
        public Guid? Id { get; set; }
        
        [JsonPropertyName("theme")] 
        public string Theme { get; set; } = "System";
        
        [JsonPropertyName("color_theme")] 
        public string ColorTheme { get; set; } = "Default";
        
        [JsonPropertyName("is_colorful_background")] 
        public bool IsColorfulBackground { get; set; }
        
        [JsonPropertyName("is_wallpaper_enabled")] 
        public bool IsWallpaperEnabled { get; set; }
        
        [JsonPropertyName("font_family")] 
        public string FontFamily { get; set; } = "Default";
        
        [JsonPropertyName("font_size")] 
        public string FontSize { get; set; } = "Default";
        
        [JsonPropertyName("language")] 
        public string Language { get; set; } = "en";
        
        [JsonPropertyName("created_at")] 
        public DateTime? CreatedAt { get; set; }
        
        [JsonPropertyName("updated_at")] 
        public DateTime? UpdatedAt { get; set; }
    }
}
