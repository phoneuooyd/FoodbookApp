using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;

namespace Foodbook.ViewModels
{
    public class PlannerViewModel
    {
        public ObservableCollection<PlannedMeal> PlannedMeals { get; set; } = new();
        public ICommand AddMealCommand { get; }
        public ICommand RemoveMealCommand { get; }

        public PlannerViewModel()
        {
            // Stub: initialize commands
        }
    }
}