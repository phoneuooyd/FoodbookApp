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

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}