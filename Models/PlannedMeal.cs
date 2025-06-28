using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Foodbook.Models
{
    public class PlannedMeal : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public Recipe? Recipe { get; set; }
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