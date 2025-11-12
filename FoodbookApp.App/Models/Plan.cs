using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    public enum PlanType
    {
        Planner,
        ShoppingList
    }

    public class Plan
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsArchived { get; set; } = false;

        // Type of plan - determines if it's a planner or shopping list
        public PlanType Type { get; set; } = PlanType.Planner;

        // Link to associated shopping list plan (for Planner type)
        // This creates a one-to-one relationship between meal planner and its shopping list
        public int? LinkedShoppingListPlanId { get; set; }

        // Custom name for the planner (optional, user-defined)
        [NotMapped]
        public string? PlannerName { get; set; }

        // Display name for UI - shows custom name or default based on type
        [NotMapped]
        public string Name => !string.IsNullOrWhiteSpace(PlannerName)
            ? PlannerName
            : (Type == PlanType.Planner ? "Planner" : "Lista zakupów");

        // Label used for display - based on plan type
        [NotMapped]
        public string Label => Type == PlanType.Planner ? "Planner" : "Lista zakupów";
    }
}
