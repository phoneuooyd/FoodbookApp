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

    private string _loadingStatus = "Ładowanie...";
    public string LoadingStatus
    {
        get => _loadingStatus;
        set
        {
            if (_loadingStatus == value) return;
            _loadingStatus = value;
            OnPropertyChanged();
        }
    }

    private double _loadingProgress = 0;
    public double LoadingProgress
    {
        get => _loadingProgress;
        set
        {
            if (Math.Abs(_loadingProgress - value) < 0.01) return;
            _loadingProgress = value;
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
        LoadingProgress = 0;
        
        try
        {
            // Etap 1: Czyszczenie danych
            LoadingStatus = "Przygotowywanie danych...";
            LoadingProgress = 0.1;
            await Task.Delay(50); // Pozwól UI się odświeżyć

            Days.Clear();
            Recipes.Clear();

            // Etap 2: Ładowanie przepisów
            LoadingStatus = "Ładowanie przepisów...";
            LoadingProgress = 0.2;
            await Task.Delay(50);

            var rec = await _recipeService.GetRecipesAsync();
            
            // Dodaj przepisy w pakietach aby nie blokować UI
            const int batchSize = 20;
            for (int i = 0; i < rec.Count; i += batchSize)
            {
                var batch = rec.Skip(i).Take(batchSize);
                foreach (var r in batch)
                    Recipes.Add(r);
                
                // Update progress podczas dodawania przepisów
                var recipeProgress = 0.2 + (0.3 * (double)(i + batchSize) / rec.Count);
                LoadingProgress = Math.Min(recipeProgress, 0.5);
                
                if (i + batchSize < rec.Count)
                    await Task.Delay(10); // Krótkie opóźnienie dla UI
            }

            // Etap 3: Ładowanie istniejących posiłków
            LoadingStatus = "Ładowanie zaplanowanych posiłków...";
            LoadingProgress = 0.5;
            await Task.Delay(50);

            var existingMeals = await _plannerService.GetPlannedMealsAsync(StartDate, EndDate);
            
            // Etap 4: Tworzenie dni planera
            LoadingStatus = "Przygotowywanie kalendarza...";
            LoadingProgress = 0.7;
            await Task.Delay(50);

            var totalDays = (EndDate.Date - StartDate.Date).Days + 1;
            var currentDay = 0;

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

                // Update progress podczas tworzenia dni
                currentDay++;
                var dayProgress = 0.7 + (0.2 * (double)currentDay / totalDays);
                LoadingProgress = dayProgress;
                
                // Pozwól UI się odświeżyć co kilka dni
                if (currentDay % 3 == 0)
                    await Task.Delay(10);
            }

            // Etap 5: Finalizacja
            LoadingStatus = "Finalizowanie...";
            LoadingProgress = 0.9;
            await Task.Delay(50);

            AdjustMealsPerDay();
            
            LoadingProgress = 1.0;
            await Task.Delay(100); // Krótkie pokazanie 100%
        }
        catch (Exception ex)
        {
            LoadingStatus = "Błąd ładowania danych";
            System.Diagnostics.Debug.WriteLine($"Error loading planner data: {ex.Message}");
            
            // Show user-friendly error message
            await Shell.Current.DisplayAlert(
                "Błąd", 
                "Wystąpił problem podczas ładowania danych planera. Spróbuj ponownie.", 
                "OK");
        }
        finally
        {
            IsLoading = false;
            LoadingStatus = "Ładowanie...";
            LoadingProgress = 0;
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
