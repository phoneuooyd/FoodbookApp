using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace Foodbook.ViewModels;

/// <summary>
/// Represents available filtering modes for diet statistics.
/// </summary>
public enum FilterMode
{
    Day,
    Week,
    Month,
    Custom,
    Plan
}

/// <summary>
/// Represents a date chip item for horizontal date navigation.
/// </summary>
public sealed class DateItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public DateItem(DateTime date)
    {
        Date = date.Date;
    }

    public DateTime Date { get; }

    public string DayNumber => Date.Day.ToString(CultureInfo.InvariantCulture);

    public string DayLabel => Date.ToString("ddd", CultureInfo.CurrentUICulture);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Represents one meal slot row in the diet statistics view.
/// </summary>
public sealed class MealSlotViewModel
{
    public Guid PlannedMealId { get; init; }

    public string MealName { get; init; } = string.Empty;

    public string IngredientsSummary { get; init; } = string.Empty;

    public double Calories { get; init; }

    public PlannedMeal? SourceMeal { get; init; }
}

/// <summary>
/// View model for DietStatisticsPage. Aggregates nutrition metrics and meal slots for selected filter/date range.
/// </summary>
public class DietStatisticsViewModel : INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;
    private readonly ILocalizationService _localizationService;

    private FilterMode _selectedFilter = FilterMode.Day;
    private DateTime _filterStartDate = DateTime.Today;
    private DateTime _filterEndDate = DateTime.Today;
    private Plan? _selectedPlan;
    private double _consumedCalories;
    private double _goalCalories = 2010;
    private double _caloriesMin;
    private double _caloriesMax = 2613;
    private double _targetRangeStart = 1809;
    private double _targetRangeEnd = 2211;
    private double _consumedCarbs;
    private double _consumedFat;
    private double _consumedProtein;
    private double _actualCarbsPercent;
    private double _actualFatPercent;
    private double _actualProteinPercent;
    private DateItem? _selectedDate;
    private bool _isLoading;

    /// <summary>
    /// Initializes a new instance of the <see cref="DietStatisticsViewModel"/> class.
    /// </summary>
    public DietStatisticsViewModel(
        IRecipeService recipeService,
        IPlanService planService,
        IPlannerService plannerService,
        ILocalizationService localizationService)
    {
        _recipeService = recipeService;
        _planService = planService;
        _plannerService = plannerService;
        _localizationService = localizationService;

        AvailablePlans = new ObservableCollection<Plan>();
        MealSlots = new ObservableCollection<MealSlotViewModel>();
        DateRange = new ObservableCollection<DateItem>();

        SelectDateCommand = new Command<DateItem>(async item => await SelectDateAsync(item));
        SelectFilterCommand = new Command<FilterMode>(async mode => await SelectFilterAsync(mode));
        SelectPlanCommand = new Command<Plan>(async plan => await SelectPlanAsync(plan));
        AddMealCommand = new Command<MealSlotViewModel>(OnAddMeal);
        OpenMealDetailCommand = new Command<MealSlotViewModel>(OnOpenMealDetail);
        SelectCustomRangeCommand = new Command(async () => await SelectCustomRangeAsync());

        _localizationService.CultureChanged += OnCultureChanged;

        BuildDateRange(DateTime.Today);
        if (DateRange.Count > 0)
        {
            SelectedDate = DateRange[6];
        }
    }

    /// <summary>
    /// Currently selected filter mode.
    /// </summary>
    public FilterMode SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (_selectedFilter == value)
            {
                return;
            }

            _selectedFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDayFilter));
        }
    }

    /// <summary>
    /// Filter start date.
    /// </summary>
    public DateTime FilterStartDate
    {
        get => _filterStartDate;
        set
        {
            if (_filterStartDate == value)
            {
                return;
            }

            _filterStartDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilterRangeText));
        }
    }

    /// <summary>
    /// Filter end date.
    /// </summary>
    public DateTime FilterEndDate
    {
        get => _filterEndDate;
        set
        {
            if (_filterEndDate == value)
            {
                return;
            }

            _filterEndDate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FilterRangeText));
        }
    }

    /// <summary>
    /// Selected plan when filter mode is plan.
    /// </summary>
    public Plan? SelectedPlan
    {
        get => _selectedPlan;
        set
        {
            if (_selectedPlan == value)
            {
                return;
            }

            _selectedPlan = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Available plans for Plan filter mode.
    /// </summary>
    public ObservableCollection<Plan> AvailablePlans { get; }

    /// <summary>
    /// Total consumed calories in selected range.
    /// </summary>
    public double ConsumedCalories
    {
        get => _consumedCalories;
        private set
        {
            if (Math.Abs(_consumedCalories - value) < 0.01)
            {
                return;
            }

            _consumedCalories = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CaloriesProgressRatio));
            OnPropertyChanged(nameof(SelectedDateCaloriesText));
        }
    }

    /// <summary>
    /// Daily goal calories.
    /// </summary>
    public double GoalCalories
    {
        get => _goalCalories;
        private set
        {
            if (Math.Abs(_goalCalories - value) < 0.01)
            {
                return;
            }

            _goalCalories = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CaloriesProgressRatio));
        }
    }

    /// <summary>
    /// Progress ratio for calories in range 0..1.
    /// </summary>
    public double CaloriesProgressRatio
        => GoalCalories <= 0 ? 0 : Math.Clamp(ConsumedCalories / GoalCalories, 0, 1);

    /// <summary>
    /// Minimum scale value for calorie summary card.
    /// </summary>
    public double CaloriesMin
    {
        get => _caloriesMin;
        private set
        {
            if (Math.Abs(_caloriesMin - value) < 0.01)
            {
                return;
            }

            _caloriesMin = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Maximum scale value for calorie summary card.
    /// </summary>
    public double CaloriesMax
    {
        get => _caloriesMax;
        private set
        {
            if (Math.Abs(_caloriesMax - value) < 0.01)
            {
                return;
            }

            _caloriesMax = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Start value for the target range overlay.
    /// </summary>
    public double TargetRangeStart
    {
        get => _targetRangeStart;
        private set
        {
            if (Math.Abs(_targetRangeStart - value) < 0.01)
            {
                return;
            }

            _targetRangeStart = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// End value for the target range overlay.
    /// </summary>
    public double TargetRangeEnd
    {
        get => _targetRangeEnd;
        private set
        {
            if (Math.Abs(_targetRangeEnd - value) < 0.01)
            {
                return;
            }

            _targetRangeEnd = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Consumed carbohydrates in grams.
    /// </summary>
    public double ConsumedCarbs
    {
        get => _consumedCarbs;
        private set
        {
            if (Math.Abs(_consumedCarbs - value) < 0.01)
            {
                return;
            }

            _consumedCarbs = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Consumed fat in grams.
    /// </summary>
    public double ConsumedFat
    {
        get => _consumedFat;
        private set
        {
            if (Math.Abs(_consumedFat - value) < 0.01)
            {
                return;
            }

            _consumedFat = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Consumed protein in grams.
    /// </summary>
    public double ConsumedProtein
    {
        get => _consumedProtein;
        private set
        {
            if (Math.Abs(_consumedProtein - value) < 0.01)
            {
                return;
            }

            _consumedProtein = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Recommended carbohydrates share in percent.
    /// </summary>
    public double RecommendedCarbsPercent => 55;

    /// <summary>
    /// Recommended fat share in percent.
    /// </summary>
    public double RecommendedFatPercent => 20;

    /// <summary>
    /// Recommended protein share in percent.
    /// </summary>
    public double RecommendedProteinPercent => 25;

    /// <summary>
    /// Actual carbohydrates share in percent.
    /// </summary>
    public double ActualCarbsPercent
    {
        get => _actualCarbsPercent;
        private set
        {
            if (Math.Abs(_actualCarbsPercent - value) < 0.01)
            {
                return;
            }

            _actualCarbsPercent = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Actual fat share in percent.
    /// </summary>
    public double ActualFatPercent
    {
        get => _actualFatPercent;
        private set
        {
            if (Math.Abs(_actualFatPercent - value) < 0.01)
            {
                return;
            }

            _actualFatPercent = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Actual protein share in percent.
    /// </summary>
    public double ActualProteinPercent
    {
        get => _actualProteinPercent;
        private set
        {
            if (Math.Abs(_actualProteinPercent - value) < 0.01)
            {
                return;
            }

            _actualProteinPercent = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Meal slots for current filter.
    /// </summary>
    public ObservableCollection<MealSlotViewModel> MealSlots { get; }

    /// <summary>
    /// Date range chips for day mode.
    /// </summary>
    public ObservableCollection<DateItem> DateRange { get; }

    /// <summary>
    /// Currently selected date item.
    /// </summary>
    public DateItem? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate == value)
            {
                return;
            }

            if (_selectedDate != null)
            {
                _selectedDate.IsSelected = false;
            }

            _selectedDate = value;
            if (_selectedDate != null)
            {
                _selectedDate.IsSelected = true;
                FilterStartDate = _selectedDate.Date;
                FilterEndDate = _selectedDate.Date;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedDateCaloriesText));
        }
    }

    /// <summary>
    /// Indicates loading state for async operations.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value)
            {
                return;
            }

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Command for selecting date item.
    /// </summary>
    public ICommand SelectDateCommand { get; }

    /// <summary>
    /// Command for selecting filter mode.
    /// </summary>
    public ICommand SelectFilterCommand { get; }

    /// <summary>
    /// Command for selecting plan in Plan mode.
    /// </summary>
    public ICommand SelectPlanCommand { get; }

    /// <summary>
    /// Command for adding a meal.
    /// </summary>
    public ICommand AddMealCommand { get; }

    /// <summary>
    /// Command for opening meal details.
    /// </summary>
    public ICommand OpenMealDetailCommand { get; }

    /// <summary>
    /// Command for selecting a custom date range.
    /// </summary>
    public ICommand SelectCustomRangeCommand { get; }

    /// <summary>
    /// Indicates whether day filter UI sections should be visible.
    /// </summary>
    public bool IsDayFilter => SelectedFilter == FilterMode.Day;

    /// <summary>
    /// Human-readable selected range.
    /// </summary>
    public string FilterRangeText => $"{FilterStartDate:dd.MM.yyyy} - {FilterEndDate:dd.MM.yyyy}";

    /// <summary>
    /// Calories label displayed above selected date chip.
    /// </summary>
    public string SelectedDateCaloriesText => $"{ConsumedCalories:F0}";

    /// <summary>
    /// Loads all data required by the page.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;
            await LoadAvailablePlansAsync();
            await RefreshAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SelectDateAsync(DateItem? dateItem)
    {
        if (dateItem == null)
        {
            return;
        }

        SelectedDate = dateItem;
        await SelectFilterAsync(FilterMode.Day);
    }

    private async Task SelectFilterAsync(FilterMode mode)
    {
        SelectedFilter = mode;

        switch (mode)
        {
            case FilterMode.Day:
                if (SelectedDate == null)
                {
                    BuildDateRange(DateTime.Today);
                    SelectedDate = DateRange.FirstOrDefault();
                }

                if (SelectedDate != null)
                {
                    FilterStartDate = SelectedDate.Date;
                    FilterEndDate = SelectedDate.Date;
                }
                break;

            case FilterMode.Week:
                var startOfWeek = GetStartOfWeek(DateTime.Today);
                FilterStartDate = startOfWeek;
                FilterEndDate = startOfWeek.AddDays(6);
                break;

            case FilterMode.Month:
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                FilterStartDate = monthStart;
                FilterEndDate = monthStart.AddMonths(1).AddDays(-1);
                break;

            case FilterMode.Custom:
                await SelectCustomRangeAsync();
                break;

            case FilterMode.Plan:
                if (SelectedPlan != null)
                {
                    FilterStartDate = SelectedPlan.StartDate.Date;
                    FilterEndDate = SelectedPlan.EndDate.Date;
                }
                else if (AvailablePlans.Count > 0)
                {
                    await SelectPlanAsync(AvailablePlans[0]);
                    return;
                }
                break;
        }

        await RefreshAsync();
    }

    private async Task SelectPlanAsync(Plan? plan)
    {
        if (plan == null)
        {
            return;
        }

        SelectedPlan = plan;
        SelectedFilter = FilterMode.Plan;
        FilterStartDate = plan.StartDate.Date;
        FilterEndDate = plan.EndDate.Date;

        await RefreshAsync();
    }

    private async Task SelectCustomRangeAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null)
        {
            return;
        }

        var startInput = await page.DisplayPromptAsync(
            _localizationService.GetString("DietStatisticsPageResources", "FilterCustom"),
            "dd.MM.yyyy",
            "OK",
            "Cancel",
            initialValue: FilterStartDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));

        if (string.IsNullOrWhiteSpace(startInput) ||
            !DateTime.TryParseExact(startInput, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            return;
        }

        var endInput = await page.DisplayPromptAsync(
            _localizationService.GetString("DietStatisticsPageResources", "FilterCustom"),
            "dd.MM.yyyy",
            "OK",
            "Cancel",
            initialValue: FilterEndDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));

        if (string.IsNullOrWhiteSpace(endInput) ||
            !DateTime.TryParseExact(endInput, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            return;
        }

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        FilterStartDate = startDate.Date;
        FilterEndDate = endDate.Date;
    }

    private async Task RefreshAsync()
    {
        var (from, to) = ResolveRange();
        var plannedMeals = await _plannerService.GetPlannedMealsAsync(from, to);
        var meals = plannedMeals?.Where(m => m.Recipe != null || m.RecipeId != Guid.Empty).ToList() ?? new List<PlannedMeal>();

        await PopulateMealsAndNutritionAsync(meals);
    }

    private async Task PopulateMealsAndNutritionAsync(List<PlannedMeal> meals)
    {
        MealSlots.Clear();

        double calories = 0;
        double carbs = 0;
        double fat = 0;
        double protein = 0;

        foreach (var meal in meals.OrderBy(m => m.Date))
        {
            var recipe = meal.Recipe;
            if (recipe == null && meal.RecipeId != Guid.Empty)
            {
                recipe = await _recipeService.GetRecipeAsync(meal.RecipeId);
            }

            if (recipe == null)
            {
                continue;
            }

            var portionBase = recipe.IloscPorcji <= 0 ? 1 : recipe.IloscPorcji;
            var portionMultiplier = (double)meal.Portions / portionBase;

            var mealCalories = recipe.Calories * portionMultiplier;
            var mealCarbs = recipe.Carbs * portionMultiplier;
            var mealFat = recipe.Fat * portionMultiplier;
            var mealProtein = recipe.Protein * portionMultiplier;

            calories += mealCalories;
            carbs += mealCarbs;
            fat += mealFat;
            protein += mealProtein;

            MealSlots.Add(new MealSlotViewModel
            {
                PlannedMealId = meal.Id,
                MealName = recipe.Name,
                IngredientsSummary = BuildIngredientSummary(recipe),
                Calories = mealCalories,
                SourceMeal = meal
            });
        }

        ConsumedCalories = calories;
        ConsumedCarbs = carbs;
        ConsumedFat = fat;
        ConsumedProtein = protein;

        GoalCalories = Math.Max(1800, Math.Round(GoalCalories));
        CaloriesMin = 0;
        CaloriesMax = Math.Max(Math.Ceiling(GoalCalories * 1.3), Math.Ceiling(ConsumedCalories * 1.1));
        TargetRangeStart = Math.Round(GoalCalories * 0.9);
        TargetRangeEnd = Math.Round(GoalCalories * 1.1);

        CalculateActualMacroPercentages();
    }

    private void CalculateActualMacroPercentages()
    {
        var carbsKcal = ConsumedCarbs * 4;
        var fatKcal = ConsumedFat * 9;
        var proteinKcal = ConsumedProtein * 4;
        var totalMacroKcal = carbsKcal + fatKcal + proteinKcal;

        if (totalMacroKcal <= 0)
        {
            ActualCarbsPercent = 0;
            ActualFatPercent = 0;
            ActualProteinPercent = 0;
            return;
        }

        ActualCarbsPercent = Math.Round(carbsKcal / totalMacroKcal * 100, 1);
        ActualFatPercent = Math.Round(fatKcal / totalMacroKcal * 100, 1);
        ActualProteinPercent = Math.Round(proteinKcal / totalMacroKcal * 100, 1);
    }

    private async Task LoadAvailablePlansAsync()
    {
        var plans = await _planService.GetPlansAsync();
        var plannerPlans = plans
            .Where(p => p.Type == PlanType.Planner && !p.IsArchived)
            .OrderByDescending(p => p.StartDate)
            .ToList();

        AvailablePlans.Clear();
        foreach (var plan in plannerPlans)
        {
            AvailablePlans.Add(plan);
        }
    }

    private (DateTime from, DateTime to) ResolveRange()
    {
        var from = FilterStartDate.Date;
        var to = FilterEndDate.Date.AddDays(1).AddTicks(-1);
        return (from, to);
    }

    private static DateTime GetStartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

    private static string BuildIngredientSummary(Recipe recipe)
    {
        if (recipe.Ingredients == null || recipe.Ingredients.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", recipe.Ingredients
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => i.Name)
            .Take(4));
    }

    private void BuildDateRange(DateTime centerDate)
    {
        DateRange.Clear();
        var start = centerDate.Date.AddDays(-6);

        for (var i = 0; i < 13; i++)
        {
            DateRange.Add(new DateItem(start.AddDays(i)));
        }
    }

    private void OnAddMeal(MealSlotViewModel? _)
    {
    }

    private void OnOpenMealDetail(MealSlotViewModel? _)
    {
    }

    private async void OnCultureChanged(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            foreach (var item in DateRange)
            {
                item.IsSelected = item == SelectedDate;
            }

            OnPropertyChanged(nameof(FilterRangeText));
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    ~DietStatisticsViewModel()
    {
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
