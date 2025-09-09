using System.Collections.Generic;

namespace Foodbook.Models
{
    public class Recipe
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Fat { get; set; }
        public double Carbs { get; set; }
        public int IloscPorcji { get; set; } = 2; // Domyœlna wartoœæ 2 porcje

        // Folder relation
        public int? FolderId { get; set; }
        public Folder? Folder { get; set; }

        // Drag & Drop helpers (UI state)
        public bool IsBeingDragged { get; set; }
        public bool IsBeingDraggedOver { get; set; }

        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
    }
}