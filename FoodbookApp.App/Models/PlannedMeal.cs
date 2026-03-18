using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Foodbook.Models
{
    public class PlannedMeal : INotifyPropertyChanged
    {
        public Guid Id { get; set; }
        public Guid RecipeId { get; set; }

        // Optional relation to a specific plan (enables multiple planners)
        public Guid? PlanId { get; set; }

        // Navigation to parent plan (useful for filtering by type)
        public Plan? Plan { get; set; }
        
        private Recipe? _recipe;
        public Recipe? Recipe 
        { 
            get => _recipe; 
            set 
            { 
                if (_recipe != value)
                {
                    _recipe = value;
                    OnPropertyChanged();
                }
            } 
        }
        
        public DateTime Date { get; set; }
        
        private int _portions = 1;
        public int Portions 
        { 
            get => _portions; 
            set 
            { 
                if (_portions != value)
                {
                    _portions = value;
                    OnPropertyChanged();
                }
            } 
        }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}