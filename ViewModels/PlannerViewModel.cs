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
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

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
    public ICommand IncreasePortionsCommand { get; }
    public ICommand DecreasePortionsCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public PlannerViewModel(IPlannerService plannerService, IRecipeService recipeService, IPlanService planService)
    {
        _plannerService = plannerService ?? throw new ArgumentNullException(nameof(plannerService));
        _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));

        AddMealCommand = new Command<PlannerDay>(AddMeal);
        RemoveMealCommand = new Command<PlannedMeal>(RemoveMeal);
        IncreasePortionsCommand = new Command<PlannedMeal>(IncreasePortions);
        DecreasePortionsCommand = new Command<PlannedMeal>(DecreasePortions);
        SaveCommand = new Command(async () =>
        {
            var plan = await SaveAsync();
            if (plan != null)
            {
                await Shell.Current.DisplayAlert(
                    "Zapisano",
                    $"Zapisano listę zakupów ({plan.StartDate:dd.MM.yyyy} - {plan.EndDate:dd.MM.yyyy})",
                    "OK");
            }
        });
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
            return;
        
        IsLoading = true;
        try
        {
            Days.Clear();
            Recipes.Clear();

            var rec = await _recipeService.GetRecipesAsync();
            foreach (var r in rec)
                Recipes.Add(r);

            // Wczytaj istniejące posiłki z bazy
            var existingMeals = await _plannerService.GetPlannedMealsAsync(StartDate, EndDate);

            for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            {
                var day = new PlannerDay(d);
                Days.Add(day);

                // Dodaj istniejące posiłki dla tego dnia
                var mealsForDay = existingMeals.Where(m => m.Date.Date == d.Date).ToList();
                foreach (var existingMeal in mealsForDay)
                {
                    var recipe = Recipes.FirstOrDefault(r => r.Id == existingMeal.RecipeId);
                    var meal = new PlannedMeal
                    {
                        Id = existingMeal.Id,
                        RecipeId = existingMeal.RecipeId,
                        Recipe = recipe,
                        Date = existingMeal.Date,
                        Portions = existingMeal.Portions
                    };
                    meal.PropertyChanged += OnMealRecipeChanged;

                    //day.Meals.Add(meal); // This adds sample meals for the days from top to bottom
                                           // will be used later when an AI planner is implemented
                }
            }

            AdjustMealsPerDay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading planner data: {ex.Message}");
            // Could show user-friendly error message here
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddMeal(PlannerDay? day)
    {
        if (day == null) return;
        var meal = new PlannedMeal { Date = day.Date, Portions = 1 };
        meal.PropertyChanged += OnMealRecipeChanged;
        day.Meals.Add(meal);
    }

    private void RemoveMeal(PlannedMeal? meal)
    {
        if (meal == null) return;
        var day = Days.FirstOrDefault(d => d.Meals.Contains(meal));
        if (day != null)
        {
            meal.PropertyChanged -= OnMealRecipeChanged;
            day.Meals.Remove(meal);
        }
    }

    private void AdjustMealsPerDay()
    {
        foreach (var day in Days)
        {
            while (day.Meals.Count < MealsPerDay)
            {
                var meal = new PlannedMeal { Date = day.Date, Portions = 1 };
                meal.PropertyChanged += OnMealRecipeChanged;
                day.Meals.Add(meal);
            }
            while (day.Meals.Count > MealsPerDay)
            {
                var mealToRemove = day.Meals[day.Meals.Count - 1];
                mealToRemove.PropertyChanged -= OnMealRecipeChanged;
                day.Meals.RemoveAt(day.Meals.Count - 1);
            }
        }
    }

    private void OnMealRecipeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlannedMeal.Recipe) && sender is PlannedMeal meal && meal.Recipe != null)
        {
            // Ustaw domyślną liczbę porcji z przepisu
            meal.Portions = meal.Recipe.IloscPorcji;
        }
    }

    private void Reset()
    {
        // Usuń event handlery przed czyszczeniem
        foreach (var day in Days)
        {
            foreach (var meal in day.Meals)
            {
                meal.PropertyChanged -= OnMealRecipeChanged;
            }
        }

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

    private void IncreasePortions(PlannedMeal? meal)
    {
        if (meal != null && meal.Portions < 20)
        {
            meal.Portions++;
        }
    }

    private void DecreasePortions(PlannedMeal? meal)
    {
        if (meal != null && meal.Portions > 1)
        {
            meal.Portions--;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
