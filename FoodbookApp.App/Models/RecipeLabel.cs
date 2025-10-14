using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    public class RecipeLabel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Optional color in hex format (#RRGGBB or #AARRGGBB)
        [MaxLength(9)]
        public string? ColorHex { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // UI helper (not persisted): selection state when assigning labels in UI
        [NotMapped]
        public bool IsSelected { get; set; }
    }
}
