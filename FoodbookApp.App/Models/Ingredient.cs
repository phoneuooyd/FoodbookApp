using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using FoodbookApp.Localization;

namespace Foodbook.Models
{
    public enum Unit
    {
        [Display(Name = nameof(UnitResources.Gram), ShortName = nameof(UnitResources.GramShort), ResourceType = typeof(UnitResources))]
        Gram,
        [Display(Name = nameof(UnitResources.Milliliter), ShortName = nameof(UnitResources.MilliliterShort), ResourceType = typeof(UnitResources))]
        Milliliter,
        [Display(Name = nameof(UnitResources.Piece), ShortName = nameof(UnitResources.PieceShort), ResourceType = typeof(UnitResources))]
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

        // Drag and drop state properties
        private bool _isBeingDragged;
        [NotMapped]
        public bool IsBeingDragged 
        { 
            get => _isBeingDragged; 
            set 
            { 
                if (_isBeingDragged != value)
                {
                    _isBeingDragged = value;
                    OnPropertyChanged();
                }
            } 
        }

        private bool _isBeingDraggedOver;
        [NotMapped]
        public bool IsBeingDraggedOver 
        { 
            get => _isBeingDraggedOver; 
            set 
            { 
                if (_isBeingDraggedOver != value)
                {
                    _isBeingDraggedOver = value;
                    OnPropertyChanged();
                }
            } 
        }

        // Order for reordering functionality in shopping lists
        [NotMapped]
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