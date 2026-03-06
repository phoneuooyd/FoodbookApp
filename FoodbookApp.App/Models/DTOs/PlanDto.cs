using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace Foodbook.Models.DTOs
{
    [Table("plans")]
    public class PlanDto : BaseModel
    {
        [PrimaryKey("id", true)]
        [JsonPropertyName("id"), JsonProperty("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id"), JsonProperty("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("start_date"), JsonProperty("start_date")] public DateTime? StartDate { get; set; }
        [JsonPropertyName("end_date"), JsonProperty("end_date")] public DateTime? EndDate { get; set; }
        [JsonPropertyName("is_archived"), JsonProperty("is_archived")] public bool? IsArchived { get; set; }
        [JsonPropertyName("type"), JsonProperty("type")] public int? Type { get; set; }
        [JsonPropertyName("planner_name"), JsonProperty("planner_name")] public string? PlannerName { get; set; }
        [JsonPropertyName("linked_shopping_list_plan_id"), JsonProperty("linked_shopping_list_plan_id")] public Guid? LinkedShoppingListPlanId { get; set; }
        [JsonPropertyName("created_at"), JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at"), JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted"), JsonProperty("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at"), JsonProperty("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}