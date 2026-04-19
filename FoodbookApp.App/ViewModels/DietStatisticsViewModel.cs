using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Models;
using Foodbook.Models.DTOs;
using FoodbookApp.Interfaces;
using Foodbook.Services;
using Foodbook.Views.Components;
using Microsoft.Maui.Graphics;

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
    public Guid MealId { get; init; }

    public DateTime MealDate { get; init; }

    public bool IsManualMeal { get; init; }

    public Guid? PlannedMealId { get; init; }

    public Guid? ManualMealId { get; init; }

    public string MealName { get; init; } = string.Empty;

    public string IngredientsSummary { get; init; } = string.Empty;

    public string PortionsSummary { get; init; } = string.Empty;

    public double Calories { get; init; }

    public PlannedMeal? SourceMeal { get; init; }
}

public sealed record MacroNutritionCardData(
    double ConsumedCarbs,
    double ConsumedFat,
    double ConsumedProtein,
    double RecommendedCarbsPercent,
    double RecommendedFatPercent,
    double RecommendedProteinPercent,
    double ActualCarbsPercent,
    double ActualFatPercent,
    double ActualProteinPercent)
{
    public static MacroNutritionCardData Empty { get; } = new(0, 0, 0, 55, 20, 25, 0, 0, 0);
}

public sealed record CalorieSummaryCardData(
    double ConsumedCalories,
    double GoalCalories,
    double CaloriesProgressRatio,
    double CaloriesMin,
    double CaloriesMax,
    double TargetRangeStart,
    double TargetRangeEnd)
{
    public static CalorieSummaryCardData Empty { get; } = new(0, 0, 0, 0, 1, 0, 0);
}

public sealed record MealSlotListData(IReadOnlyList<MealSlotViewModel> MealSlots)
{
    public static MealSlotListData Empty { get; } = new(Array.Empty<MealSlotViewModel>());

    public bool IsEmpty => MealSlots.Count == 0;
}

/// <summary>
/// View model for DietStatisticsPage. Aggregates nutrition metrics and meal slots for selected filter/date range.
/// </summary>
public class DietStatisticsViewModel : INotifyPropertyChanged
{
    private readonly IPlannerService _plannerService;
    private readonly IPlanService _planService;
    private readonly IRecipeService _recipeService;
    private readonly IIngredientService _ingredientService;
    private readonly ILocalizationService _localizationService;
    private readonly IPreferencesService _preferencesService;
    private readonly Func<Task> _planChangedHandler;

    private FilterMode _selectedFilter = FilterMode.Week;
    private DateTime _filterStartDate = DateTime.Today.AddDays(-6);
    private DateTime _filterEndDate = DateTime.Today;
    private Plan? _selectedPlan;
    private double _consumedCalories;
    private double _goalCalories = 2000;
    private double _baseDailyCalorieLimit = 2000;
    private double _targetRangeLowerRatio = 0.9;
    private double _targetRangeUpperRatio = 1.1;
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
    private bool _isListening;
    private bool _suppressAutoReload;
    private MacroNutritionCardData _macroCardData = MacroNutritionCardData.Empty;
    private CalorieSummaryCardData _calorieSummaryData = CalorieSummaryCardData.Empty;
    private MealSlotListData _mealSlotListData = MealSlotListData.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="DietStatisticsViewModel"/> class.
    /// </summary>
    public DietStatisticsViewModel(
        IPlannerService plannerService,
        IPlanService planService,
        IRecipeService recipeService,
        IIngredientService ingredientService,
        ILocalizationService localizationService,
        IPreferencesService preferencesService)
    {
        _plannerService = plannerService;
        _planService = planService;
        _recipeService = recipeService;
        _ingredientService = ingredientService;
        _localizationService = localizationService;
        _preferencesService = preferencesService;

        AvailablePlans = new ObservableCollection<Plan>();
        MealSlots = new ObservableCollection<MealSlotViewModel>();
        DateRange = new ObservableCollection<DateItem>();

        SelectDateCommand = new Command<DateItem>(async item => await SelectDateAsync(item));
        SelectFilterCommand = new Command<FilterMode>(async mode => await SelectFilterAsync(mode));
        SelectPlanCommand = new Command<Plan>(async plan => await SelectPlanAsync(plan));
        AddMealCommand = new Command<MealSlotViewModel>(async item => await AddManualMealAsync(item));
        OpenMealDetailCommand = new Command<MealSlotViewModel>(OnOpenMealDetail);
        SelectCustomRangeCommand = new Command(async () => await SelectCustomRangeAsync());
        OpenCalorieSettingsCommand = new Command(async () => await OpenCalorieSettingsAsync());
        _planChangedHandler = OnPlanChangedAsync;

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
            OnPropertyChanged(nameof(IsDayFilterActive));
            OnPropertyChanged(nameof(IsPlanPickerVisible));
            OnPropertyChanged(nameof(IsCustomRangeVisible));
            OnPropertyChanged(nameof(ChipDayColor));
            OnPropertyChanged(nameof(ChipDayTextColor));
            OnPropertyChanged(nameof(ChipWeekColor));
            OnPropertyChanged(nameof(ChipWeekTextColor));
            OnPropertyChanged(nameof(ChipMonthColor));
            OnPropertyChanged(nameof(ChipMonthTextColor));
            OnPropertyChanged(nameof(ChipCustomColor));
            OnPropertyChanged(nameof(ChipCustomTextColor));
            OnPropertyChanged(nameof(ChipPlanColor));
            OnPropertyChanged(nameof(ChipPlanTextColor));

            TriggerAutoReload();
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
            OnPropertyChanged(nameof(HeaderSubtitle));

            if (SelectedFilter == FilterMode.Custom)
            {
                TriggerAutoReload();
            }
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
            OnPropertyChanged(nameof(HeaderSubtitle));

            if (SelectedFilter == FilterMode.Custom)
            {
                TriggerAutoReload();
            }
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
            TriggerAutoReload();
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

    public MacroNutritionCardData MacroCardData
    {
        get => _macroCardData;
        private set
        {
            if (Equals(_macroCardData, value))
            {
                return;
            }

            _macroCardData = value;
            OnPropertyChanged();
        }
    }

    public CalorieSummaryCardData CalorieSummaryData
    {
        get => _calorieSummaryData;
        private set
        {
            if (Equals(_calorieSummaryData, value))
            {
                return;
            }

            _calorieSummaryData = value;
            OnPropertyChanged();
        }
    }

    public MealSlotListData MealSlotListData
    {
        get => _mealSlotListData;
        private set
        {
            if (Equals(_mealSlotListData, value))
            {
                return;
            }

            _mealSlotListData = value;
            OnPropertyChanged();
        }
    }

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

    public ICommand OpenCalorieSettingsCommand { get; }

    public double BaseDailyCalorieLimit => _baseDailyCalorieLimit;

    public double TargetRangeLowerRatio => _targetRangeLowerRatio;

    public double TargetRangeUpperRatio => _targetRangeUpperRatio;

    /// <summary>
    /// Indicates whether day filter UI sections should be visible.
    /// </summary>
    public bool IsDayFilter => SelectedFilter == FilterMode.Day;

    public bool IsDayFilterActive => IsDayFilter;

    public string HeaderTitle => _localizationService.GetString("DietStatisticsPageResources", "PageTitle");

    public string HeaderSubtitle => FilterRangeText;

    /// <summary>
    /// Indicates whether plan picker is visible.
    /// </summary>
    public bool IsPlanPickerVisible => SelectedFilter == FilterMode.Plan;

    /// <summary>
    /// Indicates whether custom date range pickers are visible.
    /// </summary>
    public bool IsCustomRangeVisible => SelectedFilter == FilterMode.Custom;

    public Color ChipDayColor => GetChipBackground(FilterMode.Day);

    public Color ChipDayTextColor => GetChipTextColor(FilterMode.Day);

    public Color ChipWeekColor => GetChipBackground(FilterMode.Week);

    public Color ChipWeekTextColor => GetChipTextColor(FilterMode.Week);

    public Color ChipMonthColor => GetChipBackground(FilterMode.Month);

    public Color ChipMonthTextColor => GetChipTextColor(FilterMode.Month);

    public Color ChipCustomColor => GetChipBackground(FilterMode.Custom);

    public Color ChipCustomTextColor => GetChipTextColor(FilterMode.Custom);

    public Color ChipPlanColor => GetChipBackground(FilterMode.Plan);

    public Color ChipPlanTextColor => GetChipTextColor(FilterMode.Plan);

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
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            _suppressAutoReload = true;

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

            if (SelectedPlan == null || AvailablePlans.All(p => p.Id != SelectedPlan.Id))
            {
                _selectedPlan = AvailablePlans.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedPlan));
            }

            var (start, end) = GetDateRangeForFilter();
            if (end < start)
            {
                (start, end) = (end, start);
            }

            FilterStartDate = start.Date;
            FilterEndDate = end.Date;

            List<PlannedMeal> plannedMeals;
            if (SelectedFilter == FilterMode.Plan && SelectedPlan != null)
            {
                plannedMeals = await _plannerService.GetPlannedMealsAsync(SelectedPlan.Id);
            }
            else
            {
                plannedMeals = await _plannerService.GetPlannedMealsAsync(start.Date, end.Date.AddDays(1).AddTicks(-1));
            }

            plannedMeals = plannedMeals
                .Where(m => m.Date.Date >= start.Date && m.Date.Date <= end.Date)
                .ToList();

            var recipes = await _recipeService.GetRecipesAsync();
            var recipeMap = recipes.ToDictionary(r => r.Id);

            var allManualMeals = (_preferencesService.GetDietStatisticsMeals() ?? Array.Empty<DietStatisticsMealDto>())
                .OrderBy(m => m.Date)
                .ThenBy(m => m.CreatedAt)
                .ToList();

            var manualMeals = allManualMeals
                .Where(m => m.Date.Date >= start.Date && m.Date.Date <= end.Date)
                .ToList();

            var todayStart = DateTime.Today;
            var todayEnd = DateTime.Today.AddDays(1).AddTicks(-1);

            var todaysPlannedMeals = await _plannerService.GetPlannedMealsAsync(todayStart, todayEnd);
            todaysPlannedMeals = todaysPlannedMeals
                .Where(m => m.Date.Date == DateTime.Today)
                .OrderBy(m => m.Date)
                .ThenBy(m => m.Id)
                .ToList();

            var todaysManualMeals = allManualMeals
                .Where(m => m.Date.Date == DateTime.Today)
                .ToList();

            double totalCalories = 0;
            double totalCarbs = 0;
            double totalFat = 0;
            double totalProtein = 0;

            foreach (var meal in plannedMeals)
            {
                var recipe = meal.Recipe;

                if (recipe == null && meal.RecipeId != Guid.Empty)
                {
                    recipeMap.TryGetValue(meal.RecipeId, out recipe);
                }

                if (recipe == null && meal.RecipeId != Guid.Empty)
                {
                    recipe = await _recipeService.GetRecipeAsync(meal.RecipeId);
                    if (recipe != null)
                    {
                        recipeMap[meal.RecipeId] = recipe;
                    }
                }

                if (recipe == null)
                {
                    continue;
                }

                double portions = meal.Portions > 0 ? meal.Portions : 1;
                double recipePortions = recipe.IloscPorcji > 0 ? recipe.IloscPorcji : 1;
                double portionMultiplier = portions / recipePortions;

                totalCalories += recipe.Calories * portionMultiplier;
                totalCarbs += recipe.Carbs * portionMultiplier;
                totalFat += recipe.Fat * portionMultiplier;
                totalProtein += recipe.Protein * portionMultiplier;
            }

            foreach (var manualMeal in manualMeals)
            {
                totalCalories += manualMeal.Calories;
                totalCarbs += manualMeal.Carbs;
                totalFat += manualMeal.Fat;
                totalProtein += manualMeal.Protein;
            }

            ConsumedCalories = Math.Round(totalCalories, 1);
            ConsumedCarbs = Math.Round(totalCarbs, 1);
            ConsumedFat = Math.Round(totalFat, 1);
            ConsumedProtein = Math.Round(totalProtein, 1);

            double macroTotal = totalCarbs + totalFat + totalProtein;
            if (macroTotal > 0)
            {
                ActualCarbsPercent = Math.Round(totalCarbs / macroTotal * 100, 1);
                ActualFatPercent = Math.Round(totalFat / macroTotal * 100, 1);
                ActualProteinPercent = Math.Round(totalProtein / macroTotal * 100, 1);
            }
            else
            {
                ActualCarbsPercent = 0;
                ActualFatPercent = 0;
                ActualProteinPercent = 0;
            }

            GoalCalories = Math.Max(1, Math.Round(_baseDailyCalorieLimit));
            CaloriesMin = 0;
            CaloriesMax = Math.Max(GoalCalories * 1.3, ConsumedCalories * 1.1);
            TargetRangeStart = GoalCalories * _targetRangeLowerRatio;
            TargetRangeEnd = GoalCalories * _targetRangeUpperRatio;

            await BuildMealSlotsAsync(todaysPlannedMeals, todaysManualMeals, recipeMap);

            MacroCardData = new MacroNutritionCardData(
                ConsumedCarbs,
                ConsumedFat,
                ConsumedProtein,
                RecommendedCarbsPercent,
                RecommendedFatPercent,
                RecommendedProteinPercent,
                ActualCarbsPercent,
                ActualFatPercent,
                ActualProteinPercent);

            CalorieSummaryData = new CalorieSummaryCardData(
                ConsumedCalories,
                GoalCalories,
                CaloriesProgressRatio,
                CaloriesMin,
                CaloriesMax,
                TargetRangeStart,
                TargetRangeEnd);

            MealSlotListData = new MealSlotListData(MealSlots.ToList());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DietStatisticsViewModel] LoadAsync error: {ex}");
        }
        finally
        {
            _suppressAutoReload = false;
            IsLoading = false;
        }
    }

    private void TriggerAutoReload()
    {
        if (_suppressAutoReload)
        {
            return;
        }

        _ = LoadAsync();
    }

    public void StartListening()
    {
        if (_isListening)
        {
            return;
        }

        _localizationService.CultureChanged += OnCultureChanged;
        AppEvents.PlanChangedAsync += _planChangedHandler;
        _isListening = true;
    }

    public void StopListening()
    {
        if (!_isListening)
        {
            return;
        }

        _localizationService.CultureChanged -= OnCultureChanged;
        AppEvents.PlanChangedAsync -= _planChangedHandler;
        _isListening = false;
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
        _suppressAutoReload = true;
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
                FilterStartDate = DateTime.Today.AddDays(-6);
                FilterEndDate = DateTime.Today;
                break;

            case FilterMode.Month:
                FilterStartDate = DateTime.Today.AddDays(-29);
                FilterEndDate = DateTime.Today;
                break;

            case FilterMode.Custom:
                if (FilterEndDate < FilterStartDate)
                {
                    FilterEndDate = FilterStartDate;
                }
                break;

            case FilterMode.Plan:
                if (SelectedPlan == null && AvailablePlans.Count > 0)
                {
                    SelectedPlan = AvailablePlans[0];
                }
                break;
        }

        _suppressAutoReload = false;
        await LoadAsync();
    }

    private async Task SelectPlanAsync(Plan? plan)
    {
        if (plan == null)
        {
            return;
        }

        _suppressAutoReload = true;
        SelectedPlan = plan;
        SelectedFilter = FilterMode.Plan;
        _suppressAutoReload = false;

        await LoadAsync();
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

        await LoadAsync();
    }

    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private (DateTime start, DateTime end) GetDateRangeForFilter()
    {
        return SelectedFilter switch
        {
            FilterMode.Day =>
                SelectedDate != null
                    ? (SelectedDate.Date, SelectedDate.Date)
                    : (FilterStartDate.Date, FilterStartDate.Date),
            FilterMode.Week => (FilterStartDate.Date, FilterEndDate.Date),
            FilterMode.Month => (FilterStartDate.Date, FilterEndDate.Date),
            FilterMode.Custom => (FilterStartDate.Date, FilterEndDate.Date),
            FilterMode.Plan => SelectedPlan != null
                ? (SelectedPlan.StartDate.Date, SelectedPlan.EndDate.Date)
                : (FilterStartDate.Date, FilterEndDate.Date),
            _ => (FilterStartDate.Date, FilterEndDate.Date)
        };
    }

    private async Task BuildMealSlotsAsync(
        IEnumerable<PlannedMeal> plannedMeals,
        IReadOnlyList<DietStatisticsMealDto> manualMeals,
        Dictionary<Guid, Recipe> recipeMap)
    {
        MealSlots.Clear();

        foreach (var meal in plannedMeals.OrderBy(m => m.Date).ThenBy(m => m.Id))
        {
            double kcal = 0;
            string mealName = _localizationService.GetString("DietStatisticsPageResources", "MealSlot");
            string portionsSummary = string.Empty;

            var recipe = meal.Recipe;
            if (recipe == null && meal.RecipeId != Guid.Empty)
            {
                recipeMap.TryGetValue(meal.RecipeId, out recipe);
            }

            if (recipe == null && meal.RecipeId != Guid.Empty)
            {
                recipe = await _recipeService.GetRecipeAsync(meal.RecipeId);
                if (recipe != null)
                {
                    recipeMap[meal.RecipeId] = recipe;
                }
            }

            if (recipe != null)
            {
                double portions = meal.Portions > 0 ? meal.Portions : 1;
                double recipePortions = recipe.IloscPorcji > 0 ? recipe.IloscPorcji : 1;
                double portionMultiplier = portions / recipePortions;
                kcal = Math.Round(recipe.Calories * portionMultiplier, 0);
                mealName = recipe.Name;
                portionsSummary = $"x{Math.Round(portions, 1):0.#}";
            }

            MealSlots.Add(new MealSlotViewModel
            {
                MealId = meal.Id,
                MealDate = meal.Date.Date,
                IsManualMeal = false,
                PlannedMealId = meal.Id,
                MealName = mealName,
                IngredientsSummary = _localizationService.GetString("DietStatisticsPageResources", "MealPlannerEntrySubtitle"),
                PortionsSummary = portionsSummary,
                Calories = kcal,
                SourceMeal = meal
            });
        }

        foreach (var manualMeal in manualMeals.OrderBy(m => m.CreatedAt))
        {
            MealSlots.Add(new MealSlotViewModel
            {
                MealId = manualMeal.Id,
                MealDate = manualMeal.Date.Date,
                IsManualMeal = true,
                ManualMealId = manualMeal.Id,
                MealName = manualMeal.Name,
                IngredientsSummary = _localizationService.GetString("DietStatisticsPageResources", "MealManualEntrySubtitle"),
                PortionsSummary = string.Empty,
                Calories = Math.Round(manualMeal.Calories, 0),
                SourceMeal = null
            });
        }

        await Task.CompletedTask;
    }

    private async Task OpenCalorieSettingsAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new CalorieLimitSettingsPage(this), true);
    }

    public async Task UpdateCalorieLimitSettingsAsync(double dailyLimit, double lowerRatio, double upperRatio)
    {
        _baseDailyCalorieLimit = Math.Max(1, dailyLimit);
        _targetRangeLowerRatio = Math.Clamp(lowerRatio, 0.5, 1.5);
        _targetRangeUpperRatio = Math.Clamp(upperRatio, _targetRangeLowerRatio, 2.0);

        OnPropertyChanged(nameof(BaseDailyCalorieLimit));
        OnPropertyChanged(nameof(TargetRangeLowerRatio));
        OnPropertyChanged(nameof(TargetRangeUpperRatio));

        await RefreshAsync();
    }

    private Color GetChipBackground(FilterMode chipMode)
        => SelectedFilter == chipMode ? Color.FromArgb("#8B72FF") : Color.FromArgb("#338B72FF");

    private Color GetChipTextColor(FilterMode chipMode)
        => SelectedFilter == chipMode ? Colors.White : Color.FromArgb("#AAAAAA");

    private void BuildDateRange(DateTime centerDate)
    {
        DateRange.Clear();
        var start = centerDate.Date.AddDays(-6);

        for (var i = 0; i < 13; i++)
        {
            DateRange.Add(new DateItem(start.AddDays(i)));
        }
    }

    private async Task AddManualMealAsync(MealSlotViewModel? sourceMeal)
    {
        var page = Application.Current?.MainPage;
        if (page == null)
        {
            return;
        }

        _ = sourceMeal;
        var targetDate = DateTime.Today;

        try
        {
            var popup = new AddDietMealPopup();

            var showTask = page.ShowPopupAsync(popup);
            await Task.WhenAny(showTask, popup.ResultTask);
            var popupResult = await popup.ResultTask;
            await showTask;
            if (popupResult == null)
            {
                return;
            }

            var name = popupResult.MealName?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                await page.DisplayAlert(
                    _localizationService.GetString("DietStatisticsPageResources", "AddMealPopupTitle"),
                    _localizationService.GetString("DietStatisticsPageResources", "AddMealPopupInvalidName"),
                    "OK");
                return;
            }

            var caloriesText = popupResult.Calories;

            if (string.IsNullOrWhiteSpace(caloriesText) ||
                (!double.TryParse(caloriesText, NumberStyles.Float, CultureInfo.CurrentCulture, out var calories) &&
                 !double.TryParse(caloriesText, NumberStyles.Float, CultureInfo.InvariantCulture, out calories)) ||
                calories < 0)
            {
                await page.DisplayAlert(
                    _localizationService.GetString("DietStatisticsPageResources", "AddMealPopupTitle"),
                    _localizationService.GetString("DietStatisticsPageResources", "AddMealPopupInvalidCalories"),
                    "OK");
                return;
            }

            var meals = _preferencesService.GetDietStatisticsMeals().ToList();
            meals.Add(new DietStatisticsMealDto
            {
                Date = targetDate.Date,
                Name = name.Trim(),
                Calories = calories,
                Carbs = 0,
                Fat = 0,
                Protein = 0,
                CreatedAt = DateTime.UtcNow
            });

            _preferencesService.SaveDietStatisticsMeals(meals);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DietStatisticsViewModel] AddManualMealAsync error: {ex.Message}");
            await page.DisplayAlert(
                _localizationService.GetString("DietStatisticsPageResources", "AddMealPopupTitle"),
                _localizationService.GetString("DietStatisticsPageResources", "AddMealPopupSaveError"),
                "OK");
        }
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
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
        });

        await LoadAsync();
    }

    private async Task OnPlanChangedAsync()
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await LoadAsync();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DietStatisticsViewModel] OnPlanChangedAsync error: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    ~DietStatisticsViewModel()
    {
        StopListening();
    }
}
