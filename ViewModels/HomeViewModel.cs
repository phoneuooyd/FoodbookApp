using System.ComponentModel;
using System.Runtime.CompilerServices;
using Foodbook.Services;
using Foodbook.Models;
using System.Collections.ObjectModel;

namespace Foodbook.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    private int _recipeCount;
    public int RecipeCount
    {
        get => _recipeCount;
        set { if (_recipeCount != value) { _recipeCount = value; OnPropertyChanged(); } }
    }

    private int _planCount;
    public int PlanCount
    {
        get => _planCount;
        set { if (_planCount != value) { _planCount = value; OnPropertyChanged(); } }
    }

    private int _archivedPlanCount;
    public int ArchivedPlanCount
    {
        get => _archivedPlanCount;
        set { if (_archivedPlanCount != value) { _archivedPlanCount = value; OnPropertyChanged(); } }
    }

    private bool _isLoading = true; // Zaczynamy z true, ¿eby pokazaæ loader
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    private ObservableCollection<PlannedMealGroup> _plannedMealHistory = new();
    public ObservableCollection<PlannedMealGroup> PlannedMealHistory
    {
        get => _plannedMealHistory;
        set { if (_plannedMealHistory != value) { _plannedMealHistory = value; OnPropertyChanged(); } }
    }

    private bool _hasPlannedMeals;
    public bool HasPlannedMeals
    {
        get => _hasPlannedMeals;
        set { if (_hasPlannedMeals != value) { _hasPlannedMeals = value; OnPropertyChanged(); } }
    }

    public HomeViewModel(IRecipeService recipeService, IPlanService planService, IPlannerService plannerService)
    {
        _recipeService = recipeService;
        _planService = planService;
        _plannerService = plannerService;
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            // £aduj przepisy
            var recipes = await _recipeService.GetRecipesAsync();
            RecipeCount = recipes?.Count ?? 0;

            // £aduj plany
            var allPlans = await _planService.GetPlansAsync();
            if (allPlans != null)
            {
                PlanCount = allPlans.Count(p => !p.IsArchived);
                ArchivedPlanCount = allPlans.Count(p => p.IsArchived);
            }
            else
            {
                PlanCount = 0;
                ArchivedPlanCount = 0;
            }

            // £aduj zaplanowane posi³ki (od dzisiaj do przysz³oœci)
            await LoadPlannedMealsAsync();
        }
        catch (Exception ex)
        {
            // W przypadku b³êdu, ustaw wartoœci na 0
            RecipeCount = 0;
            PlanCount = 0;
            ArchivedPlanCount = 0;
            PlannedMealHistory.Clear();
            HasPlannedMeals = false;
            
            // Log b³êdu (opcjonalne)
            System.Diagnostics.Debug.WriteLine($"Error loading home data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPlannedMealsAsync()
    {
        try
        {
            // Pobierz zaplanowane posi³ki od dzisiaj do 14 dni w przód
            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(14);
            
            var plannedMeals = await _plannerService.GetPlannedMealsAsync(startDate, endDate);
            
            // Filtruj tylko te z recepturami
            var mealsWithRecipes = plannedMeals?.Where(m => m.Recipe != null).ToList() ?? new List<PlannedMeal>();
            
            if (mealsWithRecipes.Any())
            {
                // Grupuj po dacie i sortuj
                var groupedMeals = mealsWithRecipes
                    .GroupBy(m => m.Date.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new PlannedMealGroup
                    {
                        Date = g.Key,
                        DateLabel = GetDateLabel(g.Key),
                        Meals = new ObservableCollection<PlannedMeal>(g.OrderBy(m => m.Date.TimeOfDay))
                    })
                    .ToList();

                PlannedMealHistory.Clear();
                foreach (var group in groupedMeals)
                {
                    PlannedMealHistory.Add(group);
                }
                
                HasPlannedMeals = true;
            }
            else
            {
                PlannedMealHistory.Clear();
                HasPlannedMeals = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading planned meals: {ex.Message}");
            PlannedMealHistory.Clear();
            HasPlannedMeals = false;
        }
    }

    private string GetDateLabel(DateTime date)
    {
        var today = DateTime.Today;
        
        if (date.Date == today)
            return "Dzisiaj";
        else if (date.Date == today.AddDays(1))
            return "Jutro";
        else if (date.Date == today.AddDays(2))
            return "Pojutrze";
        else
            return date.ToString("dddd, dd.MM.yyyy");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// Klasa pomocnicza do grupowania zaplanowanych posi³ków
public class PlannedMealGroup
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public ObservableCollection<PlannedMeal> Meals { get; set; } = new();
}

