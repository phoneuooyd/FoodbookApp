using System;
using System.Text.Json.Serialization;

namespace Foodbook.Models.DTOs
{
    public class PlannedMealDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("recipe_id")] public string? RecipeId { get; set; }
        [JsonPropertyName("plan_id")] public string? PlanId { get; set; }
        [JsonPropertyName("date")] public DateTime? Date { get; set; }
        [JsonPropertyName("portions")] public int? Portions { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}