using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace Foodbook.Models
{
    public enum Unit
    {
        Gram,
        Milliliter,
        Piece
    }

    public class Ingredient : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public Unit Unit { get; set; }

        private bool _isChecked;
        [NotMapped]
        public bool IsChecked 
        { 
            get => _isChecked; 
            set 
            { 
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            } 
        }

        // Order for shopping list items (used for reordering functionality)
        public int Order { get; set; } = 0;

        // Nutritional information per specified amount/unit
        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Fat { get; set; }
        public double Carbs { get; set; }
        public int? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}