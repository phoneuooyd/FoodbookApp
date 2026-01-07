using System;
using System.Text.Json.Serialization;

namespace Foodbook.Models.DTOs
{
    public class RecipeDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("calories")] public double? Calories { get; set; }
        [JsonPropertyName("protein")] public double? Protein { get; set; }
        [JsonPropertyName("fat")] public double? Fat { get; set; }
        [JsonPropertyName("carbs")] public double? Carbs { get; set; }
        [JsonPropertyName("portions")] public int? Portions { get; set; }
        [JsonPropertyName("folder_id")] public string? FolderId { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}