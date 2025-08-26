using System.ComponentModel.DataAnnotations;

namespace Foodbook.Models
{
    public class ShoppingListItem
    {
        [Key]
        public int Id { get; set; }
        
        public int PlanId { get; set; }
        public Plan Plan { get; set; } = null!;
        
        public string IngredientName { get; set; } = string.Empty;
        public Unit Unit { get; set; }
        public bool IsChecked { get; set; } = false;
        
        // Optional: store the quantity for validation purposes
        public double Quantity { get; set; }
        
        // Order for reordering functionality
        public int Order { get; set; } = 0;
    }
}