using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace Foodbook.Models.DTOs
{
    [Table("ingredients")]
    public class IngredientDto : BaseModel
    {
        [JsonPropertyName("id"), JsonProperty("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id"), JsonProperty("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("name"), JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("quantity"), JsonProperty("quantity")] public double? Quantity { get; set; }
        [JsonPropertyName("unit"), JsonProperty("unit")] public int? Unit { get; set; }
        [JsonPropertyName("unit_weight"), JsonProperty("unit_weight")] public double? UnitWeight { get; set; }
        [JsonPropertyName("calories"), JsonProperty("calories")] public double? Calories { get; set; }
        [JsonPropertyName("protein"), JsonProperty("protein")] public double? Protein { get; set; }
        [JsonPropertyName("fat"), JsonProperty("fat")] public double? Fat { get; set; }
        [JsonPropertyName("carbs"), JsonProperty("carbs")] public double? Carbs { get; set; }
        [JsonPropertyName("recipe_id"), JsonProperty("recipe_id")] public Guid? RecipeId { get; set; }
        [JsonPropertyName("created_at"), JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at"), JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted"), JsonProperty("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at"), JsonProperty("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}