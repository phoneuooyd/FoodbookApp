using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Folder> SubFolders { get; set; } = new Collection<Folder>();
        public ICollection<Recipe> Recipes { get; set; } = new Collection<Recipe>();

        // Drag & Drop helpers
        public bool IsBeingDragged { get; set; }
        public bool IsBeingDraggedOver { get; set; }

        // UI helpers
        public int Level { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public bool IsExpanded { get; set; }
    }
}
