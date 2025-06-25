using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.ViewModels
{
    public class PlannerViewModel
    {
        private readonly IPlannerService _plannerService;

        public ObservableCollection<PlannedMeal> PlannedMeals { get; } = new();

        public ICommand AddMealCommand { get; }
        public ICommand RemoveMealCommand { get; }

        public PlannerViewModel(IPlannerService plannerService)
        {
            _plannerService = plannerService ?? throw new ArgumentNullException(nameof(plannerService));

            AddMealCommand = new Command<PlannedMeal>(async meal => await AddMealAsync(meal));
            RemoveMealCommand = new Command<PlannedMeal>(async meal => await RemoveMealAsync(meal));
        }

        public async Task LoadMealsAsync(DateTime from, DateTime to)
        {
            PlannedMeals.Clear();
            var meals = await _plannerService.GetPlannedMealsAsync(from, to);
            foreach (var meal in meals)
                PlannedMeals.Add(meal);
        }

        private async Task AddMealAsync(PlannedMeal meal)
        {
            if (meal == null)
                return;

            await _plannerService.AddPlannedMealAsync(meal);
            PlannedMeals.Add(meal);
        }

        private async Task RemoveMealAsync(PlannedMeal meal)
        {
            if (meal == null)
                return;

            await _plannerService.RemovePlannedMealAsync(meal.Id);
            PlannedMeals.Remove(meal);
        }
    }
}