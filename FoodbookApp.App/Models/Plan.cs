using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Foodbook.Models
{
    public enum PlanType
    {
        Planner,
        ShoppingList,
        Foodbook
    }

    public class Plan
    {
        public Guid Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsArchived { get; set; } = false;

        // Type of plan - determines if it's a planner, shopping list, or foodbook
        public PlanType Type { get; set; } = PlanType.ShoppingList;

        // Link to associated shopping list plan (for Planner type)
        // This creates a one-to-one relationship between meal planner and its shopping list
        public Guid? LinkedShoppingListPlanId { get; set; }

        // Persisted title/name for the plan (user-defined)
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(100)]
        public string? AccentColor { get; set; }

        [MaxLength(8)]
        public string? Emoji { get; set; }

        public int DurationDays { get; set; } = 7;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Backward compatible alias used across the UI/viewmodels
        [NotMapped]
        public string? PlannerName
        {
            get => Title;
            set => Title = value;
        }

        [NotMapped]
        public bool IsFoodbook => Type == PlanType.Foodbook;

        [NotMapped]
        public string DisplayEmoji => string.IsNullOrWhiteSpace(Emoji) ? "??" : Emoji;

        [NotMapped]
        public string DisplayColor => string.IsNullOrWhiteSpace(AccentColor) ? "#5B3FE8" : AccentColor;

        // Display name for UI - shows custom title or type key; localization is handled by the UI layer
        [NotMapped]
        public string Name => !string.IsNullOrWhiteSpace(Title) ? Title! : Type.ToString();

        // Label used for display - returns the PlanType key; localization is handled by the UI layer
        [NotMapped]
        public string Label => Type.ToString();
    }
}
