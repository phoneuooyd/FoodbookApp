using System;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;
using Newtonsoft.Json;

namespace Foodbook.Models.DTOs
{
    [Table("folders")]
    public class FolderDto : BaseModel
    {
        [JsonPropertyName("id"), JsonProperty("id")] public Guid? Id { get; set; }
        [JsonPropertyName("owner_id"), JsonProperty("owner_id")] public string? OwnerId { get; set; }
        [JsonPropertyName("name"), JsonProperty("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("description"), JsonProperty("description")] public string? Description { get; set; }
        [JsonPropertyName("parent_folder_id"), JsonProperty("parent_folder_id")] public Guid? ParentFolderId { get; set; }
        [JsonPropertyName("order"), JsonProperty("order")] public int Order { get; set; }
        [JsonPropertyName("created_at"), JsonProperty("created_at")] public DateTime? CreatedAt { get; set; }
        [JsonPropertyName("updated_at"), JsonProperty("updated_at")] public DateTime? UpdatedAt { get; set; }
        [JsonPropertyName("is_deleted"), JsonProperty("is_deleted")] public bool? IsDeleted { get; set; }
        [JsonPropertyName("deleted_at"), JsonProperty("deleted_at")] public DateTime? DeletedAt { get; set; }
    }
}