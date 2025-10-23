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

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                    // name change does not directly change Display* but keep symmetry if external sets nutrition too
                }
            }
        }

        private double _quantity;
        public double Quantity
        {
            get => _quantity;
            set
            {
                if (Math.Abs(_quantity - value) > double.Epsilon)
                {
                    _quantity = value;
                    OnPropertyChanged();
                    RaiseDisplayNutritionChanged();
                }
            }
        }

        private Unit _unit;
        public Unit Unit
        {
            get => _unit;
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    OnPropertyChanged();
                    RaiseDisplayNutritionChanged();
                }
            }
        }

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

        // Nutritional information per 100g/100ml or per 1 piece
        private double _calories;
        public double Calories
        {
            get => _calories;
            set
            {
                if (Math.Abs(_calories - value) > double.Epsilon)
                {
                    _calories = value;
                    OnPropertyChanged();
                    RaiseDisplayNutritionChanged();
                }
            }
        }

        private double _protein;
        public double Protein
        {
            get => _protein;
            set
            {
                if (Math.Abs(_protein - value) > double.Epsilon)
                {
                    _protein = value;
                    OnPropertyChanged();
                    RaiseDisplayNutritionChanged();
                }
            }
        }

        private double _fat;
        public double Fat
        {
            get => _fat;
            set
            {
                if (Math.Abs(_fat - value) > double.Epsilon)
                {
                    _fat = value;
                    OnPropertyChanged();
                    RaiseDisplayNutritionChanged();
                }
            }
        }

        private double _carbs;
        public double Carbs
        {
            get => _carbs;
            set
            {
                if (Math.Abs(_carbs - value) > double.Epsilon)
                {
                    _carbs = value;
                    OnPropertyChanged();
                    RaiseDisplayNutritionChanged();
                }
            }
        }

        public int? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }

        // Computed nutrition for the specified Quantity and Unit
        [NotMapped]
        public double DisplayCalories => Math.Round(Calories * GetUnitFactor(Unit, Quantity), 1);
        [NotMapped]
        public double DisplayProtein => Math.Round(Protein * GetUnitFactor(Unit, Quantity), 1);
        [NotMapped]
        public double DisplayFat => Math.Round(Fat * GetUnitFactor(Unit, Quantity), 1);
        [NotMapped]
        public double DisplayCarbs => Math.Round(Carbs * GetUnitFactor(Unit, Quantity), 1);

        private static double GetUnitFactor(Unit unit, double quantity)
        {
            try
            {
                return unit switch
                {
                    Unit.Gram => quantity / 100.0,
                    Unit.Milliliter => quantity / 100.0,
                    Unit.Piece => quantity,
                    _ => quantity / 100.0
                };
            }
            catch
            {
                return 0;
            }
        }

        private void RaiseDisplayNutritionChanged()
        {
            OnPropertyChanged(nameof(DisplayCalories));
            OnPropertyChanged(nameof(DisplayProtein));
            OnPropertyChanged(nameof(DisplayFat));
            OnPropertyChanged(nameof(DisplayCarbs));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}