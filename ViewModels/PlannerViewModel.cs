using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class PlannerViewModel : INotifyPropertyChanged
{
    private readonly IPlannerService _plannerService;
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;

    public ObservableCollection<Recipe> Recipes { get; } = new();
    public ObservableCollection<PlannerDay> Days { get; } = new();

    private bool _isLoading;

    private DateTime _startDate = DateTime.Today;
    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate == value) return;
            _startDate = value;
            OnPropertyChanged();
            _ = LoadAsync();
        }
    }

    private DateTime _endDate = DateTime.Today.AddDays(6);
    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            if (_endDate == value) return;
            _endDate = value;
            OnPropertyChanged();
            _ = LoadAsync();
        }
    }

    private int _mealsPerDay = 3;
    public int MealsPerDay
    {
        get => _mealsPerDay;
        set
        {
            if (_mealsPerDay == value) return;
            _mealsPerDay = value;
            OnPropertyChanged();
            AdjustMealsPerDay();
        }
    }

    public ICommand AddMealCommand { get; }
    public ICommand RemoveMealCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public PlannerViewModel(IPlannerService plannerService, IRecipeService recipeService, IPlanService planService)
    {
        _plannerService = plannerService ?? throw new ArgumentNullException(nameof(plannerService));
        _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));

        AddMealCommand = new Command<PlannerDay>(AddMeal);
        RemoveMealCommand = new Command<PlannedMeal>(RemoveMeal);
        SaveCommand = new Command(async () =>
        {
            var plan = await SaveAsync();
            if (plan != null)
                await Shell.Current.GoToAsync(nameof(Foodbook.Views.ShoppingListPage));
        });
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task LoadAsync()
    {
        if (_isLoading)
            return;
        _isLoading = true;

        Days.Clear();
        Recipes.Clear();

        var rec = await _recipeService.GetRecipesAsync();
        foreach (var r in rec)
            Recipes.Add(r);

        for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
        {
            var day = new PlannerDay(d);
            Days.Add(day);
        }
        AdjustMealsPerDay();

        _isLoading = false;
    }

    private void AddMeal(PlannerDay? day)
    {
        if (day == null) return;
        day.Meals.Add(new PlannedMeal { Date = day.Date, Portions = 1 });
    }

    private void RemoveMeal(PlannedMeal? meal)
    {
        if (meal == null) return;
        var day = Days.FirstOrDefault(d => d.Meals.Contains(meal));
        if (day != null)
            day.Meals.Remove(meal);
    }

    private void AdjustMealsPerDay()
    {
        foreach (var day in Days)
        {
            while (day.Meals.Count < MealsPerDay)
                day.Meals.Add(new PlannedMeal { Date = day.Date, Portions = 1 });
            while (day.Meals.Count > MealsPerDay)
                day.Meals.RemoveAt(day.Meals.Count - 1);
        }
    }

    private void Reset()
    {
        _startDate = DateTime.Today;
        _endDate = DateTime.Today.AddDays(6);
        OnPropertyChanged(nameof(StartDate));
        OnPropertyChanged(nameof(EndDate));
        MealsPerDay = 3;
        Days.Clear();
        _ = LoadAsync();
    }

    private async Task<Plan?> SaveAsync()
    {
        if (await _planService.HasOverlapAsync(StartDate, EndDate))
        {
            await Shell.Current.DisplayAlert("Błąd", "Plan na podane daty już istnieje.", "OK");
            return null;
        }

        var plan = new Plan { StartDate = StartDate, EndDate = EndDate };
        await _planService.AddPlanAsync(plan);

        var existing = await _plannerService.GetPlannedMealsAsync(StartDate, EndDate);
        foreach (var m in existing)
            await _plannerService.RemovePlannedMealAsync(m.Id);

        foreach (var day in Days)
        {
            foreach (var meal in day.Meals)
            {
                if (meal.Recipe != null)
                    meal.RecipeId = meal.Recipe.Id;
                if (meal.RecipeId > 0)
                    await _plannerService.AddPlannedMealAsync(new PlannedMeal
                    {
                        RecipeId = meal.RecipeId,
                        Date = meal.Date,
                        Portions = meal.Portions
                    });
            }
        }

        Reset();
        return plan;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
