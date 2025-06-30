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

        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
    }
}