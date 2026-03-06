using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace Foodbook.Models.DTOs
{
    [Table("planned_meals")]
    public class PlannedMealDto : BaseModel
    {
        [PrimaryKey("id", true)]
        [JsonPropertyName("id"), JsonProperty("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id"), JsonProperty("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("recipe_id"), JsonProperty("recipe_id")] public Guid? RecipeId { get; set; }
        [JsonPropertyName("plan_id"), JsonProperty("plan_id")] public Guid? PlanId { get; set; }
        [JsonPropertyName("date"), JsonProperty("date")] public DateTime? Date { get; set; }
        [JsonPropertyName("portions"), JsonProperty("portions")] public int? Portions { get; set; }
        [JsonPropertyName("created_at"), JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at"), JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted"), JsonProperty("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at"), JsonProperty("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}