namespace Foodbook.Models
{
    public enum Unit
    {
        Gram,
        Milliliter,
        Piece
    }

    public class Ingredient
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public Unit Unit { get; set; }

        public int? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }
    }
}