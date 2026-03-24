using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    public class Recipe
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Fat { get; set; }
        public double Carbs { get; set; }
        public int IloscPorcji { get; set; } = 2; 

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Folder relation
        public Guid? FolderId { get; set; }
        public Folder? Folder { get; set; }

        // Drag & Drop helpers (UI state)
        [NotMapped]
        public bool IsBeingDragged { get; set; }
        [NotMapped]
        public bool IsBeingDraggedOver { get; set; }

        [NotMapped]
        public bool HasUnmatchedIngredients { get; set; }

        [NotMapped]
        public bool WasAiFallbackUsed { get; set; }

        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();

        // Labels (many-to-many via join table RecipeRecipeLabel)
        public List<RecipeLabel> Labels { get; set; } = new();
    }
}
