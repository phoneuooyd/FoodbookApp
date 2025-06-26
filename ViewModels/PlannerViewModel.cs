using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;
using Foodbook.Views;
using Foodbook.Data;

namespace Foodbook.ViewModels
{
    public class PlannerViewModel
    {
        private readonly IPlannerService _plannerService;

        public ObservableCollection<PlannedMeal> PlannedMeals { get; } = new();

        public ICommand AddMealCommand { get; }
        public ICommand EditMealCommand { get; }
        public ICommand DeleteMealCommand { get; }

        public PlannerViewModel(IPlannerService plannerService)
        {
            _plannerService = plannerService ?? throw new ArgumentNullException(nameof(plannerService));

            AddMealCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(MealFormPage)));
            EditMealCommand = new Command<PlannedMeal>(async meal =>
            {
                if (meal != null)
                    await Shell.Current.GoToAsync($"{nameof(MealFormPage)}?id={meal.Id}");
            });
            DeleteMealCommand = new Command<PlannedMeal>(async meal => await RemoveMealAsync(meal));
        }

        public async Task LoadMealsAsync(DateTime from, DateTime to)
        {
            PlannedMeals.Clear();
            var meals = await _plannerService.GetPlannedMealsAsync(from, to);
            foreach (var meal in meals)
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