using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;

namespace Foodbook.Models.DTOs
{
    public class PlanDto : BaseModel
    {
        [JsonPropertyName("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("start_date")] public DateTime? StartDate { get; set; }
        [JsonPropertyName("end_date")] public DateTime? EndDate { get; set; }
        [JsonPropertyName("is_archived")] public bool? IsArchived { get; set; }
        [JsonPropertyName("type")] public int? Type { get; set; }
        [JsonPropertyName("planner_name")] public string? PlannerName { get; set; }
        [JsonPropertyName("linked_shopping_list_plan_id")] public Guid? LinkedShoppingListPlanId { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}