using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Foodbook.Models
{
    public class PlannerDay : INotifyPropertyChanged
    {
        public DateTime Date { get; }
        public ObservableCollection<PlannedMeal> Meals { get; } = new();

        public PlannerDay(DateTime date)
        {
            Date = date;
            Meals.CollectionChanged += OnMealsCollectionChanged;
        }

        private void OnMealsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Unsubscribe from old items
            if (e.OldItems != null)
            {
                foreach (PlannedMeal meal in e.OldItems)
                {
                    meal.PropertyChanged -= OnMealPropertyChanged;
                }
            }

            // Subscribe to new items
            if (e.NewItems != null)
            {
                foreach (PlannedMeal meal in e.NewItems)
                {
                    meal.PropertyChanged += OnMealPropertyChanged;
                }
            }

            // Recalculate totals
            RaiseNutritionalTotalsChanged();
        }

        private void OnMealPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Recalculate when meal recipe or portions change
            if (e.PropertyName == nameof(PlannedMeal.Recipe) || 
                e.PropertyName == nameof(PlannedMeal.Portions))
            {
                RaiseNutritionalTotalsChanged();
            }
        }

        private void RaiseNutritionalTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalCalories));
            OnPropertyChanged(nameof(TotalProtein));
            OnPropertyChanged(nameof(TotalFat));
            OnPropertyChanged(nameof(TotalCarbs));
        }

        // Computed totals for the day - values are now reported per 1 portion (do NOT multiply by meal.Portions)
        public double TotalCalories => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Calories / Math.Max(m.Recipe.IloscPorcji, 1)));

        public double TotalProtein => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Protein / Math.Max(m.Recipe.IloscPorcji, 1)));

        public double TotalFat => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Fat / Math.Max(m.Recipe.IloscPorcji, 1)));

        public double TotalCarbs => Meals
            .Where(m => m.Recipe != null)
            .Sum(m => (m.Recipe!.Carbs / Math.Max(m.Recipe.IloscPorcji, 1)));

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
