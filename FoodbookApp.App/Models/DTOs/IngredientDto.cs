using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;

namespace Foodbook.Models.DTOs
{
    public class IngredientDto : BaseModel
    {
        [JsonPropertyName("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("quantity")] public double? Quantity { get; set; }
        [JsonPropertyName("unit")] public int? Unit { get; set; }
        [JsonPropertyName("unit_weight")] public double? UnitWeight { get; set; }
        [JsonPropertyName("calories")] public double? Calories { get; set; }
        [JsonPropertyName("protein")] public double? Protein { get; set; }
        [JsonPropertyName("fat")] public double? Fat { get; set; }
        [JsonPropertyName("carbs")] public double? Carbs { get; set; }
        [JsonPropertyName("recipe_id")] public Guid? RecipeId { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}