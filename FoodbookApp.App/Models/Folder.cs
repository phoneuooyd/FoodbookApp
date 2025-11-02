using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    public class Folder
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? ParentFolderId { get; set; }
        public Folder? ParentFolder { get; set; }

        // Manual ordering among siblings. Lower numbers appear first.
        public int Order { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Folder> SubFolders { get; set; } = new Collection<Folder>();
        public ICollection<Recipe> Recipes { get; set; } = new Collection<Recipe>();

        // Drag & Drop helpers (UI-only, do not persist)
        [NotMapped]
        public bool IsBeingDragged { get; set; }
        [NotMapped]
        public bool IsBeingDraggedOver { get; set; }

        // UI helpers (do not persist)
        [NotMapped]
        public int Level { get; set; }
        [NotMapped]
        public string DisplayName { get; set; } = string.Empty;
        [NotMapped]
        public bool IsExpanded { get; set; }
    }
}
