using System;
using System.Text.Json.Serialization;

namespace Foodbook.Models.DTOs
{
    public class RecipeLabelDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("color_hex")] public string? ColorHex { get; set; }
        [JsonPropertyName("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}