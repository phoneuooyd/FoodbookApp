using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;
using FoodbookApp.Services.Supabase;

namespace Foodbook.Models.DTOs
{
    [Table(SupabaseTableResolver.TEST_ENDPOINTS ? "shopping_list_items_test" : "shopping_list_items")]
    public class ShoppingListItemDto : BaseModel
    {
        [PrimaryKey("id", true)]
        [JsonPropertyName("id"), JsonProperty("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id"), JsonProperty("owner_id")] public string? OwnerId { get; set; }
        [Column("plan_id"), JsonPropertyName("plan_id"), JsonProperty("plan_id")] public Guid? PlanId { get; set; }
        [JsonPropertyName("ingredient_name"), JsonProperty("ingredient_name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("unit"), JsonProperty("unit")] public int? Unit { get; set; }
        [JsonPropertyName("is_checked"), JsonProperty("is_checked")] public bool? IsChecked { get; set; }
        [JsonPropertyName("quantity"), JsonProperty("quantity")] public double? Quantity { get; set; }
        [JsonPropertyName("order"), JsonProperty("order")] public int? Order { get; set; }
        [JsonPropertyName("created_at"), JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at"), JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted"), JsonProperty("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at"), JsonProperty("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}