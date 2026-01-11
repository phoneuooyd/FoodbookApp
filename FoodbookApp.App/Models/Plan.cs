using System;
using System.ComponentModel.DataAnnotations;
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
        public Guid Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsArchived { get; set; } = false;

        // Type of plan - determines if it's a planner or shopping list
        // Default kept as ShoppingList for backward compatibility with tests that expect the label
        public PlanType Type { get; set; } = PlanType.ShoppingList;

        // Link to associated shopping list plan (for Planner type)
        // This creates a one-to-one relationship between meal planner and its shopping list
        public Guid? LinkedShoppingListPlanId { get; set; }

        // Persisted title/name for the plan (user-defined)
        [MaxLength(200)]
        public string? Title { get; set; }

        // Backward compatible alias used across the UI/viewmodels
        [NotMapped]
        public string? PlannerName
        {
            get => Title;
            set => Title = value;
        }

        // Display name for UI - shows custom title or default based on type
        [NotMapped]
        public string Name => !string.IsNullOrWhiteSpace(Title)
            ? Title!
            : (Type == PlanType.Planner ? "Planner" : "Lista zakupów");

        // Label used for display - reflect the current type
        [NotMapped]
        public string Label => Type == PlanType.Planner ? "Planner" : "Lista zakupów";
    }
}
