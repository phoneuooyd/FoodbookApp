using System;
using System.Text.Json.Serialization;

namespace Foodbook.Models.DTOs
{
    public class ShoppingListItemDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("plan_id")] public string? PlanId { get; set; }
        [JsonPropertyName("ingredient_name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("unit")] public int? Unit { get; set; }
        [JsonPropertyName("is_checked")] public bool? IsChecked { get; set; }
        [JsonPropertyName("quantity")] public double? Quantity { get; set; }
        [JsonPropertyName("order")] public int? Order { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}