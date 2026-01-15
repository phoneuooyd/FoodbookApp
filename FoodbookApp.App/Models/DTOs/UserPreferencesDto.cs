using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace Foodbook.Models.DTOs
{
    /// <summary>
    /// DTO for user preferences stored in Supabase
    /// </summary>
    [Table("user_preferences")]
    public class UserPreferencesDto : BaseModel
    {
        [JsonPropertyName("id"), JsonProperty("id")] 
        public Guid? Id { get; set; }
        
        [JsonPropertyName("theme"), JsonProperty("theme")] 
        public string Theme { get; set; } = "System";
        
        [JsonPropertyName("color_theme"), JsonProperty("color_theme")] 
        public string ColorTheme { get; set; } = "Default";
        
        [JsonPropertyName("is_colorful_background"), JsonProperty("is_colorful_background")] 
        public bool IsColorfulBackground { get; set; }
        
        [JsonPropertyName("is_wallpaper_enabled"), JsonProperty("is_wallpaper_enabled")] 
        public bool IsWallpaperEnabled { get; set; }
        
        [JsonPropertyName("font_family"), JsonProperty("font_family")] 
        public string FontFamily { get; set; } = "Default";
        
        [JsonPropertyName("font_size"), JsonProperty("font_size")] 
        public string FontSize { get; set; } = "Default";
        
        [JsonPropertyName("language"), JsonProperty("language")] 
        public string Language { get; set; } = "en";
        
        [JsonPropertyName("created_at"), JsonProperty("created_at")] 
        public DateTime? CreatedAt { get; set; }
        
        [JsonPropertyName("updated_at"), JsonProperty("updated_at")] 
        public DateTime? UpdatedAt { get; set; }
    }
}
