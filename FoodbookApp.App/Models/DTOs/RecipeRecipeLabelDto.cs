using System;
using System.Text.Json.Serialization;

namespace Foodbook.Models.DTOs
{
    public class RecipeRecipeLabelDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("recipe_id")] public string? RecipeId { get; set; }
        [JsonPropertyName("label_id")] public string? LabelId { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}