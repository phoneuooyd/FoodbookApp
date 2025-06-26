using System;

namespace Foodbook.Models
{
    public class PlannedMeal
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }
        public DateTime Date { get; set; }
        public int Portions { get; set; } = 1;
    }
}