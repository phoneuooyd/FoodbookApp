using System.ComponentModel;
using System.Runtime.CompilerServices;
using Foodbook.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using FoodbookApp.Localization;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using FoodbookApp.Interfaces;
using Foodbook.Views;

namespace Foodbook.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;
    private readonly ILocalizationService _localizationService;
    private bool _isRecipeIngredientsPopupOpen = false; // Protection against multiple opens
    private bool _isMealsPopupOpen = false; // Protection against multiple opens for meals popup

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

    private bool _isLoading = true; // Zaczynamy z true, �eby pokaza� loader
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

    // Planned meals period settings
    private PlannedMealsPeriod _selectedPlannedMealsPeriod = PlannedMealsPeriod.Today;
    public PlannedMealsPeriod SelectedPlannedMealsPeriod
    {
        get => _selectedPlannedMealsPeriod;
        set 
        { 
            if (_selectedPlannedMealsPeriod != value) 
            { 
                _selectedPlannedMealsPeriod = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(PlannedMealsPeriodDisplay));
                OnPropertyChanged(nameof(PlannedMealsMaxHeight));
                _ = LoadPlannedMealsAsync(); 
            } 
        }
    }

    // Dynamic height for planned meals panel (expand on Today)
    public double PlannedMealsMaxHeight => SelectedPlannedMealsPeriod == PlannedMealsPeriod.Today ? 10000 : 250;

    private DateTime _plannedMealsCustomStartDate = DateTime.Today;
    public DateTime PlannedMealsCustomStartDate
    {
        get => _plannedMealsCustomStartDate;
        set { if (_plannedMealsCustomStartDate != value) { _plannedMealsCustomStartDate = value; OnPropertyChanged(); if (SelectedPlannedMealsPeriod == PlannedMealsPeriod.Custom) _ = LoadPlannedMealsAsync(); } }
    }

    private DateTime _plannedMealsCustomEndDate = DateTime.Today.AddDays(7);
    public DateTime PlannedMealsCustomEndDate
    {
        get => _plannedMealsCustomEndDate;
        set { if (_plannedMealsCustomEndDate != value) { _plannedMealsCustomEndDate = value; OnPropertyChanged(); if (SelectedPlannedMealsPeriod == PlannedMealsPeriod.Custom) _ = LoadPlannedMealsAsync(); } }
    }

    public string PlannedMealsPeriodDisplay => GetPlannedMealsPeriodDisplay();

    // Available plans for filtering nutrition stats
    private ObservableCollection<Plan> _availablePlans = new();
    public ObservableCollection<Plan> AvailablePlans
    {
        get => _availablePlans;
        set { if (_availablePlans != value) { _availablePlans = value; OnPropertyChanged(); } }
    }

    private Plan? _selectedPlan;
    public Plan? SelectedPlan
    {
        get => _selectedPlan;
        set 
        { 
            if (_selectedPlan != value) 
            { 
                _selectedPlan = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(NutritionPeriodDisplay));
                
                // ENHANCED: More robust refresh with logging
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] SelectedPlan changed to: {(_selectedPlan?.StartDate.ToString("dd.MM.yyyy") ?? "null")}");
                _ = Task.Run(async () => await RefreshNutritionDataAsync()); // Force async refresh
            } 
        }
    }

    // Nutrition statistics properties
    private double _totalCalories;
    public double TotalCalories
    {
        get => _totalCalories;
        set { if (Math.Abs(_totalCalories - value) > 0.1) { _totalCalories = value; OnPropertyChanged(); OnPropertyChanged(nameof(CaloriesDisplay)); } }
    }

    private double _totalProtein;
    public double TotalProtein
    {
        get => _totalProtein;
        set { if (Math.Abs(_totalProtein - value) > 0.1) { _totalProtein = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProteinDisplay)); } }
    }

    private double _totalFat;
    public double TotalFat
    {
        get => _totalFat;
        set { if (Math.Abs(_totalFat - value) > 0.1) { _totalFat = value; OnPropertyChanged(); OnPropertyChanged(nameof(FatDisplay)); } }
    }

    private double _totalCarbs;
    public double TotalCarbs
    {
        get => _totalCarbs;
        set { if (Math.Abs(_totalCarbs - value) > 0.1) { _totalCarbs = value; OnPropertyChanged(); OnPropertyChanged(nameof(CarbsDisplay)); } }
    }

    // Nutrition period settings - SIMPLIFIED (removed Custom from main menu)
    private NutritionPeriod _selectedNutritionPeriod = NutritionPeriod.Day;
    public NutritionPeriod SelectedNutritionPeriod
    {
        get => _selectedNutritionPeriod;
        set 
        { 
            if (_selectedNutritionPeriod != value) 
            { 
                _selectedNutritionPeriod = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(NutritionPeriodDisplay));
                
                // Reset selected plan when switching away from Plan mode
                if (value != NutritionPeriod.Plan)
                {
                    _selectedPlan = null;
                    OnPropertyChanged(nameof(SelectedPlan));
                }
                
                _ = RefreshNutritionDataAsync(); // Auto refresh data
            } 
        }
    }

    private DateTime _customStartDate = DateTime.Today;
    public DateTime CustomStartDate
    {
        get => _customStartDate;
        set { if (_customStartDate != value) { _customStartDate = value; OnPropertyChanged(); if (SelectedNutritionPeriod == NutritionPeriod.Custom) _ = RefreshNutritionDataAsync(); } }
    }

    private DateTime _customEndDate = DateTime.Today;
    public DateTime CustomEndDate
    {
        get => _customEndDate;
        set { if (_customEndDate != value) { _customEndDate = value; OnPropertyChanged(); if (SelectedNutritionPeriod == NutritionPeriod.Custom) _ = RefreshNutritionDataAsync(); } }
    }

    private bool _hasNutritionData;
    public bool HasNutritionData
    {
        get => _hasNutritionData;
        set { if (_hasNutritionData != value) { _hasNutritionData = value; OnPropertyChanged(); } }
    }

    // Display properties for nutrition
    public string CaloriesDisplay => HasNutritionData ? $"{TotalCalories:F0}" : "---";
    public string ProteinDisplay => HasNutritionData ? $"{TotalProtein:F0}g" : "---";
    public string FatDisplay => HasNutritionData ? $"{TotalFat:F0}g" : "---";
    public string CarbsDisplay => HasNutritionData ? $"{TotalCarbs:F0}g" : "---";
    public string NutritionPeriodDisplay => GetNutritionPeriodDisplay();

    // Commands
    public ICommand ShowRecipeIngredientsCommand { get; }
    public ICommand ShowMealsPopupCommand { get; }
    public ICommand ShowMealDetailsPopupCommand { get; }
    public ICommand ChangeNutritionPeriodCommand { get; }
    public ICommand ChangePlannedMealsPeriodCommand { get; }
    public ICommand OpenDietStatisticsCommand { get; }

    // Portion adjustment from Home popups
    public ICommand IncreaseMealPortionsCommand { get; }
    public ICommand DecreaseMealPortionsCommand { get; }

    public HomeViewModel(IRecipeService recipeService, IPlanService planService, IPlannerService plannerService, ILocalizationService localizationService)
    {
        _recipeService = recipeService;
        _planService = planService;
        _plannerService = plannerService;
        _localizationService = localizationService;
        
        // Subscribe to culture changes to refresh display properties
        _localizationService.CultureChanged += OnCultureChanged;
        
        // Subscribe to plan change events (new/updated/archived plans or planned meals changes)
        Foodbook.Services.AppEvents.PlanChangedAsync += OnPlanChangedAsync;
        
        ShowRecipeIngredientsCommand = new Command<PlannedMeal>(async (meal) => await ShowRecipeIngredientsAsync(meal), (meal) => !_isRecipeIngredientsPopupOpen);
        ShowMealsPopupCommand = new Command(async () => await ShowMealsPopupAsync(), () => !_isMealsPopupOpen);
        ShowMealDetailsPopupCommand = new Command<PlannedMeal>(async (meal) => await ShowMealDetailsPopupAsync(meal), (meal) => !_isMealsPopupOpen);
        ChangeNutritionPeriodCommand = new Command(async () => await ShowNutritionPeriodPickerAsync());
        ChangePlannedMealsPeriodCommand = new Command(async () => await ShowPlannedMealsPeriodPickerAsync());
        OpenDietStatisticsCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(DietStatisticsPage)));

        IncreaseMealPortionsCommand = new Command<PlannedMeal>(async (meal) => await IncreaseMealPortionsAsync(meal), CanIncreaseMealPortions);
        DecreaseMealPortionsCommand = new Command<PlannedMeal>(async (meal) => await DecreaseMealPortionsAsync(meal), CanDecreaseMealPortions);
    }

    private bool CanIncreaseMealPortions(PlannedMeal? meal) => meal != null && meal.Portions < 20;
    private bool CanDecreaseMealPortions(PlannedMeal? meal) => meal != null && meal.Portions > 1;

    private async Task IncreaseMealPortionsAsync(PlannedMeal? meal)
    {
        if (meal == null) return;
        try
        {
            if (meal.Portions < 20)
            {
                meal.Portions++;
                // Persist the change
                await _plannerService.UpdatePlannedMealAsync(meal);
                // Update nutrition stats if visible period includes this meal
                await RefreshNutritionDataAsync();
                (IncreaseMealPortionsCommand as Command<PlannedMeal>)?.ChangeCanExecute();
                (DecreaseMealPortionsCommand as Command<PlannedMeal>)?.ChangeCanExecute();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Error increasing portions: {ex.Message}");
        }
    }

    private async Task DecreaseMealPortionsAsync(PlannedMeal? meal)
    {
        if (meal == null) return;
        try
        {
            if (meal.Portions > 1)
            {
                meal.Portions--;
                // Persist the change
                await _plannerService.UpdatePlannedMealAsync(meal);
                // Update nutrition stats if visible period includes this meal
                await RefreshNutritionDataAsync();
                (IncreaseMealPortionsCommand as Command<PlannedMeal>)?.ChangeCanExecute();
                (DecreaseMealPortionsCommand as Command<PlannedMeal>)?.ChangeCanExecute();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Error decreasing portions: {ex.Message}");
        }
    }

    /// <summary>
    /// Centralized method to refresh nutrition data - called after every filter change
    /// </summary>
    private async Task RefreshNutritionDataAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] RefreshNutritionDataAsync started - Period: {SelectedNutritionPeriod}, Plan: {(_selectedPlan?.StartDate.ToString("dd.MM") ?? "null")}");
            
            await CalculateNutritionStatsAsync();
            
            // Force UI update by notifying all display properties
            OnPropertyChanged(nameof(CaloriesDisplay));
            OnPropertyChanged(nameof(ProteinDisplay));
            OnPropertyChanged(nameof(FatDisplay));
            OnPropertyChanged(nameof(CarbsDisplay));
            OnPropertyChanged(nameof(HasNutritionData));
            OnPropertyChanged(nameof(NutritionPeriodDisplay));
            
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Nutrition data refreshed - Calories: {CaloriesDisplay}, HasData: {HasNutritionData}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Error refreshing nutrition data: {ex.Message}");
            ResetNutritionStats();
        }
    }

    private async Task OnPlanChangedAsync()
    {
        try
        {
            // Reload available plans
            await LoadAvailablePlansAsync();
            
            // Reload planned meals and nutrition stats silently (without toggling IsLoading)
            await LoadPlannedMealsAsync();
            await RefreshNutritionDataAsync(); // Use centralized refresh method
            
            // Recount plans (active/archived) in case of archive action
            var allPlans = await _planService.GetPlansAsync();
            if (allPlans != null)
            {
                PlanCount = allPlans.Count(p => !p.IsArchived);
                ArchivedPlanCount = allPlans.Count(p => p.IsArchived);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error handling plan changed event: {ex.Message}");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            IsLoading = true;

            // �aduj przepisy
            var recipes = await _recipeService.GetRecipesAsync();
            RecipeCount = recipes?.Count ?? 0;

            // �aduj plany
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

            // Load available plans for filtering
            await LoadAvailablePlansAsync();

            // �aduj zaplanowane posi�ki
            await LoadPlannedMealsAsync();
            
            // Oblicz statystyki �ywieniowe
            await RefreshNutritionDataAsync(); // Use centralized refresh method
        }
        catch (Exception ex)
        {
            // W przypadku b��du, ustaw warto�ci na 0
            RecipeCount = 0;
            PlanCount = 0;
            ArchivedPlanCount = 0;
            PlannedMealHistory.Clear();
            HasPlannedMeals = false;
            AvailablePlans.Clear();
            ResetNutritionStats();
            
            // Log b��du (opcjonalne)
            System.Diagnostics.Debug.WriteLine($"Error loading home data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAvailablePlansAsync()
    {
        try
        {
            if (_planService == null)
                return;

            // Load all plans from service and filter to Planner type only
            var all = await _planService.GetPlansAsync();
            var plannerOnly = all
                .Where(p => p.Type == PlanType.Planner)
                .OrderByDescending(p => p.StartDate)
                .ToList();

            // AvailablePlans should expose only planner-type plans
            AvailablePlans = new ObservableCollection<Plan>(plannerOnly);

            // PlanCount should represent active (non-archived) planner plans only
            PlanCount = plannerOnly.Count(p => !p.IsArchived);

            // ArchivedPlanCount should represent archived planner plans only
            ArchivedPlanCount = plannerOnly.Count(p => p.IsArchived);

            // Notify property changes if needed
            OnPropertyChanged(nameof(AvailablePlans));
            OnPropertyChanged(nameof(PlanCount));
            OnPropertyChanged(nameof(ArchivedPlanCount));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] LoadAvailablePlansAsync failed: {ex.Message}");
        }
    }

    private async Task CalculateNutritionStatsAsync()
    {
        try
        {
            var (startDate, endDate) = GetNutritionDateRange();
            
            var plannedMeals = await _plannerService.GetPlannedMealsAsync(startDate, endDate);
            var mealsWithRecipes = plannedMeals?.Where(m => m.Recipe != null).ToList() ?? new List<PlannedMeal>();

            if (!mealsWithRecipes.Any())
            {
                ResetNutritionStats();
                return;
            }

            double totalCalories = 0;
            double totalProtein = 0;
            double totalFat = 0;
            double totalCarbs = 0;

            foreach (var meal in mealsWithRecipes)
            {
                if (meal.Recipe == null) continue;

                // Pobierz pe�ny przepis ze sk�adnikami
                var fullRecipe = await _recipeService.GetRecipeAsync(meal.Recipe.Id);
                if (fullRecipe == null) continue;

                // Oblicz warto�ci na podstawie liczby porcji
                var portionMultiplier = (double)meal.Portions / fullRecipe.IloscPorcji;

                totalCalories += fullRecipe.Calories * portionMultiplier;
                totalProtein += fullRecipe.Protein * portionMultiplier;
                totalFat += fullRecipe.Fat * portionMultiplier;
                totalCarbs += fullRecipe.Carbs * portionMultiplier;
            }

            TotalCalories = totalCalories;
            TotalProtein = totalProtein;
            TotalFat = totalFat;
            TotalCarbs = totalCarbs;
            HasNutritionData = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error calculating nutrition stats: {ex.Message}");
            ResetNutritionStats();
        }
    }

    private (DateTime startDate, DateTime endDate) GetNutritionDateRange()
    {
        return SelectedNutritionPeriod switch
        {
            NutritionPeriod.Day => (DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1)),
            NutritionPeriod.Week => (DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek), DateTime.Today.AddDays(7 - (int)DateTime.Today.DayOfWeek).AddSeconds(-1)),
            NutritionPeriod.Plan => SelectedPlan != null ? (SelectedPlan.StartDate, SelectedPlan.EndDate.AddDays(1).AddSeconds(-1)) : (DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1)),
            NutritionPeriod.Custom => (CustomStartDate, CustomEndDate.AddDays(1).AddSeconds(-1)),
            _ => (DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1))
        };
    }

    private void ResetNutritionStats()
    {
        TotalCalories = 0;
        TotalProtein = 0;
        TotalFat = 0;
        TotalCarbs = 0;
        HasNutritionData = false;
    }

    private string GetNutritionPeriodDisplay()
    {
        return SelectedNutritionPeriod switch
        {
            NutritionPeriod.Day => ButtonResources.Today,
            NutritionPeriod.Week => ButtonResources.ThisWeek,
            NutritionPeriod.Plan => SelectedPlan != null ? $"Plan: {SelectedPlan.StartDate:dd.MM} - {SelectedPlan.EndDate:dd.MM}" : "Plan: Wybierz plan",
            NutritionPeriod.Custom => $"{CustomStartDate:dd.MM} - {CustomEndDate:dd.MM}",
            _ => ButtonResources.Today
        };
    }

    /// <summary>
    /// RESTORED: Added Custom option back to main picker with simplified date input
    /// </summary>
    private async Task ShowNutritionPeriodPickerAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null) return;

        var options = new List<string>
        {
            ButtonResources.Today,
            ButtonResources.ThisWeek
        };
        
        // Add plan option if plans are available
        if (AvailablePlans.Any())
        {
            options.Add(GetHomeText("PlanOptionLabel", "Plan"));
        }
        
        // RESTORED: Add custom date option back
        options.Add(ButtonResources.CustomDate);

        var choice = await page.DisplayActionSheet(
            GetHomeText("StatsPeriodPickerTitle", "Select period for statistics"),
            ButtonResources.Cancel,
            null, 
            options.ToArray());

        switch (choice)
        {
            case var today when today == ButtonResources.Today:
                SelectedNutritionPeriod = NutritionPeriod.Day;
                break;
            case var thisWeek when thisWeek == ButtonResources.ThisWeek:
                SelectedNutritionPeriod = NutritionPeriod.Week;
                break;
            case var planOption when planOption == GetHomeText("PlanOptionLabel", "Plan"):
                await ShowPlanPickerAsync();
                break;
            case var customDate when customDate == ButtonResources.CustomDate:
                await ShowCustomDateRangePickerAsync();
                break;
        }
    }

    private async Task ShowPlanPickerAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null) return;

        if (!AvailablePlans.Any())
        {
            await page.DisplayAlert(
                GetHomeText("NoPlansTitle", "No plans"),
                GetHomeText("NoPlansMessage", "There are no plans available to choose from."),
                ButtonResources.OK);
            return;
        }

        var planOptions = AvailablePlans.Select(p => $"{p.StartDate:dd.MM.yyyy} - {p.EndDate:dd.MM.yyyy}").ToArray();
        
        var choice = await page.DisplayActionSheet(
            GetHomeText("PlanPickerTitle", "Select plan"),
            ButtonResources.Cancel,
            null, 
            planOptions);

        if (!string.IsNullOrEmpty(choice) && choice != ButtonResources.Cancel)
        {
            var selectedIndex = Array.IndexOf(planOptions, choice);
            if (selectedIndex >= 0 && selectedIndex < AvailablePlans.Count)
            {
                SelectedPlan = AvailablePlans[selectedIndex];
                SelectedNutritionPeriod = NutritionPeriod.Plan;
                
                // FIXED: Force immediate refresh after plan selection
                await RefreshNutritionDataAsync();
                
                System.Diagnostics.Debug.WriteLine($"[HomeViewModel] Plan selected: {SelectedPlan.StartDate:dd.MM} - {SelectedPlan.EndDate:dd.MM}");
            }
        }
    }

    /// <summary>
    /// SIMPLIFIED: Simple custom date picker using basic input dialogs
    /// </summary>
    private async Task ShowCustomDateRangePickerAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null) return;

        try
        {
            // Simple input dialog approach for better reliability
            var startDateInput = await page.DisplayPromptAsync(
                GetHomeText("CustomDateStartTitle", "Start date"),
                GetHomeText("CustomDateStartPrompt", "Enter start date (dd.mm.yyyy):"),
                ButtonResources.OK,
                ButtonResources.Cancel,
                placeholder: GetHomeText("CustomDateStartPlaceholder", "e.g. 01.01.2024"),
                initialValue: CustomStartDate.ToString("dd.MM.yyyy"));

            if (string.IsNullOrEmpty(startDateInput))
                return;

            if (DateTime.TryParseExact(startDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var startDate))
            {
                var endDateInput = await page.DisplayPromptAsync(
                    GetHomeText("CustomDateEndTitle", "End date"),
                    GetHomeText("CustomDateEndPrompt", "Enter end date (dd.mm.yyyy):"),
                    ButtonResources.OK,
                    ButtonResources.Cancel,
                    placeholder: GetHomeText("CustomDateEndPlaceholder", "e.g. 07.01.2024"),
                    initialValue: CustomEndDate.ToString("dd.MM.yyyy"));

                if (string.IsNullOrEmpty(endDateInput))
                    return;

                if (DateTime.TryParseExact(endDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var endDate))
                {
                    if (endDate >= startDate)
                    {
                        CustomStartDate = startDate;
                        CustomEndDate = endDate;
                        SelectedNutritionPeriod = NutritionPeriod.Custom;
                        
                        // FORCE refresh after setting custom dates
                        await RefreshNutritionDataAsync();
                        
                        await page.DisplayAlert(
                            GetHomeText("CustomDateRangeSetTitle", "Range set"),
                            string.Format(
                                GetHomeText("CustomDateRangeSetMessageFormat", "Filter set to:{0}{1} - {2}"),
                                Environment.NewLine,
                                startDate.ToString("dd.MM.yyyy"),
                                endDate.ToString("dd.MM.yyyy")),
                            ButtonResources.OK);
                    }
                    else
                    {
                        await page.DisplayAlert(
                            GetHomeText("ErrorTitle", "Error"),
                            GetHomeText("CustomDateRangeInvalidOrderMessage", "End date must be greater than or equal to start date."),
                            ButtonResources.OK);
                    }
                }
                else
                {
                    await page.DisplayAlert(
                        GetHomeText("ErrorTitle", "Error"),
                        GetHomeText("CustomDateRangeInvalidEndFormatMessage", "Invalid end date format. Use dd.mm.yyyy."),
                        ButtonResources.OK);
                }
            }
            else
            {
                await page.DisplayAlert(
                    GetHomeText("ErrorTitle", "Error"),
                    GetHomeText("CustomDateRangeInvalidStartFormatMessage", "Invalid start date format. Use dd.mm.yyyy."),
                    ButtonResources.OK);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing custom date range picker: {ex.Message}");
            await page.DisplayAlert(
                GetHomeText("ErrorTitle", "Error"),
                GetHomeText("CustomDateRangeGeneralErrorMessage", "An error occurred while setting date range."),
                ButtonResources.OK);
        }
    }

    private async Task LoadPlannedMealsAsync()
    {
        try
        {
            var (startDate, endDate) = GetPlannedMealsDateRange();
            
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

    private (DateTime startDate, DateTime endDate) GetPlannedMealsDateRange()
    {
        return SelectedPlannedMealsPeriod switch
        {
            PlannedMealsPeriod.Today => (DateTime.Today, DateTime.Today.AddDays(1).AddSeconds(-1)),
            PlannedMealsPeriod.Week => (DateTime.Today, DateTime.Today.AddDays(7)),
            PlannedMealsPeriod.Custom => (PlannedMealsCustomStartDate, PlannedMealsCustomEndDate.AddDays(1).AddSeconds(-1)),
            _ => (DateTime.Today, DateTime.Today.AddDays(7))
        };
    }

    private string GetPlannedMealsPeriodDisplay()
    {
        return SelectedPlannedMealsPeriod switch
        {
            PlannedMealsPeriod.Today => ButtonResources.Today,
            PlannedMealsPeriod.Week => ButtonResources.ThisWeek,
            PlannedMealsPeriod.Custom => $"{PlannedMealsCustomStartDate:dd.MM} - {PlannedMealsCustomEndDate:dd.MM}",
            _ => ButtonResources.ThisWeek
        };
    }

    // Restored helper used by LoadPlannedMealsAsync
    private string GetDateLabel(DateTime date)
    {
        return date.ToString("dddd, dd.MM.yyyy");
    }

    // Restored helper used by ShowRecipeIngredientsAsync
    private string GetUnitText(Unit unit)
    {
        return unit switch
        {
            Unit.Gram => FoodbookApp.Localization.UnitResources.GramShort,
            Unit.Milliliter => FoodbookApp.Localization.UnitResources.MilliliterShort,
            Unit.Piece => FoodbookApp.Localization.UnitResources.PieceShort,
            _ => string.Empty
        };
    }

    // Restored method for showing ingredients with protection
    private async Task ShowRecipeIngredientsAsync(PlannedMeal meal)
    {
        // Protection against multiple opens
        if (_isRecipeIngredientsPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Recipe ingredients popup already open, ignoring request");
            return;
        }

        if (meal?.Recipe == null)
            return;

        var page = Application.Current?.MainPage;
        if (page == null) return;

        try
        {
            _isRecipeIngredientsPopupOpen = true;
            ((Command<PlannedMeal>)ShowRecipeIngredientsCommand).ChangeCanExecute();

            System.Diagnostics.Debug.WriteLine($"?? HomeViewModel: Opening recipe ingredients popup for {meal.Recipe.Name}");

            var fullRecipe = await _recipeService.GetRecipeAsync(meal.Recipe.Id);
            if (fullRecipe == null)
            {
                await page.DisplayAlert(
                    GetHomeText("ErrorTitle", "Error"),
                    GetHomeText("RecipeIngredientsLoadErrorMessage", "Could not load recipe."),
                    ButtonResources.OK);
                return;
            }

            var title = $"{fullRecipe.Name}";
            if (meal.Portions != fullRecipe.IloscPorcji)
            {
                title += string.Format(GetHomeText("PortionsFormat", "({0} portions)"), meal.Portions);
            }

            var content = "";

            if (fullRecipe.Ingredients != null && fullRecipe.Ingredients.Any())
            {
                content += $"{GetHomeText("IngredientsSectionTitle", "INGREDIENTS")}:\n";
                var ingredientsList = fullRecipe.Ingredients
                    .Select(ing =>
                    {
                        var adjustedQuantity = (ing.Quantity * meal.Portions) / fullRecipe.IloscPorcji;
                        var unitText = GetUnitText(ing.Unit);
                        return $"• {ing.Name}: {adjustedQuantity:F1} {unitText}";
                    })
                    .ToList();
                content += string.Join("\n", ingredientsList);
            }
            else
            {
                content += $"{GetHomeText("IngredientsSectionTitle", "INGREDIENTS")}:\n{GetHomeText("IngredientsSectionEmpty", "No ingredients defined.")}";
            }

            if (!string.IsNullOrWhiteSpace(fullRecipe.Description))
            {
                content += $"\n\n{GetHomeText("InstructionsSectionTitle", "INSTRUCTIONS")}:\n";
                content += fullRecipe.Description;
            }

            await page.DisplayAlert(title, content, ButtonResources.OK);

            System.Diagnostics.Debug.WriteLine($"? HomeViewModel: Recipe ingredients popup closed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? HomeViewModel: Error showing recipe ingredients: {ex.Message}");

            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Attempting to handle popup blocking");

                try
                {
                    if (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"?? HomeViewModel: Could not handle popup blocking: {modalEx.Message}");
                }
            }

            await page.DisplayAlert(
                GetHomeText("ErrorTitle", "Error"),
                GetHomeText("RecipeDetailsLoadErrorMessage", "Could not load recipe details."),
                ButtonResources.OK);
        }
        finally
        {
            _isRecipeIngredientsPopupOpen = false;
            ((Command<PlannedMeal>)ShowRecipeIngredientsCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Recipe ingredients popup protection released");
        }
    }

    private async Task ShowMealDetailsPopupAsync(PlannedMeal meal)
    {
        if (_isMealsPopupOpen) { System.Diagnostics.Debug.WriteLine("Meals popup already open, ignoring request"); return; }
        if (meal?.Recipe == null) return; var page = Application.Current?.MainPage; if (page == null) return;
        try
        {
            _isMealsPopupOpen = true; ((Command)ShowMealsPopupCommand).ChangeCanExecute(); ((Command<PlannedMeal>)ShowMealDetailsPopupCommand).ChangeCanExecute();
            var fullRecipe = await _recipeService.GetRecipeAsync(meal.Recipe.Id); if (fullRecipe == null) return;
            var items = new List<object>();
            // Date header (kept minimal)
            items.Add(new Views.Components.SimpleListPopup.SectionHeader { Text = GetDateLabel(meal.Date.Date) });
            // Title with +/- (also shows name and portions) and dynamic ingredients below
            items.Add(new Views.Components.SimpleListPopup.MealPreviewBlock
            {
                Meal = meal,
                Recipe = fullRecipe,
                IncreaseCommand = IncreaseMealPortionsCommand,
                DecreaseCommand = DecreaseMealPortionsCommand
            });
            // Macros always for 1 portion (below the title and ingredients)
            var perPortion = Math.Max(fullRecipe.IloscPorcji, 1);
            var onePortionMultiplier = 1.0 / perPortion;
            items.Add(new Views.Components.SimpleListPopup.MacroRow
            {
                Calories = fullRecipe.Calories * onePortionMultiplier,
                Protein = fullRecipe.Protein * onePortionMultiplier,
                Fat = fullRecipe.Fat * onePortionMultiplier,
                Carbs = fullRecipe.Carbs * onePortionMultiplier
            });
            // Description at the bottom
            if (!string.IsNullOrWhiteSpace(fullRecipe.Description))
            {
                items.Add(new Views.Components.SimpleListPopup.Description { Text = fullRecipe.Description! });
            }
            var popup = new Views.Components.SimpleListPopup { TitleText = GetDateLabel(meal.Date.Date), Items = items, IsBulleted = false };
            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            await Task.WhenAny(showTask, resultTask);
            var _ = resultTask.IsCompleted ? await resultTask : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing meal details popup: {ex.Message}");
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                try { if (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0) await Application.Current.MainPage.Navigation.PopModalAsync(false); }
                catch { }
            }
        }
        finally
        {
            _isMealsPopupOpen = false; ((Command)ShowMealsPopupCommand).ChangeCanExecute(); ((Command<PlannedMeal>)ShowMealDetailsPopupCommand).ChangeCanExecute();
        }
    }

    // Restored planned meals period picker
    private async Task ShowPlannedMealsPeriodPickerAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null) return;

        var options = new[]
        {
            ButtonResources.Today,
            ButtonResources.ThisWeek,
            ButtonResources.CustomDate
        };

        var choice = await page.DisplayActionSheet(
            GetHomeText("PlannedMealsPeriodPickerTitle", "Select period for planned meals"),
            ButtonResources.Cancel,
            null,
            options);

        switch (choice)
        {
            case var today when today == ButtonResources.Today:
                SelectedPlannedMealsPeriod = PlannedMealsPeriod.Today;
                break;
            case var thisWeek when thisWeek == ButtonResources.ThisWeek:
                SelectedPlannedMealsPeriod = PlannedMealsPeriod.Week;
                break;
            case var customDate when customDate == ButtonResources.CustomDate:
                await ShowCustomPlannedMealsDateRangePickerAsync();
                break;
        }
    }

    private async Task ShowCustomPlannedMealsDateRangePickerAsync()
    {
        var page = Application.Current?.MainPage;
        if (page == null) return;

        var startDateInput = await page.DisplayPromptAsync(
            GetHomeText("PlannedMealsStartDateTitle", "Start date"),
            GetHomeText("PlannedMealsStartDatePrompt", "Enter start date (dd.mm.yyyy):"),
            ButtonResources.OK,
            ButtonResources.Cancel,
            initialValue: PlannedMealsCustomStartDate.ToString("dd.MM.yyyy"));

        if (DateTime.TryParseExact(startDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var startDate))
        {
            var endDateInput = await page.DisplayPromptAsync(
                GetHomeText("PlannedMealsEndDateTitle", "End date"),
                GetHomeText("PlannedMealsEndDatePrompt", "Enter end date (dd.mm.yyyy):"),
                ButtonResources.OK,
                ButtonResources.Cancel,
                initialValue: PlannedMealsCustomEndDate.ToString("dd.MM.yyyy"));

            if (DateTime.TryParseExact(endDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var endDate))
            {
                if (endDate >= startDate) { PlannedMealsCustomStartDate = startDate; PlannedMealsCustomEndDate = endDate; SelectedPlannedMealsPeriod = PlannedMealsPeriod.Custom; OnPropertyChanged(nameof(PlannedMealsMaxHeight)); }
                else
                    await page.DisplayAlert(
                        GetHomeText("ErrorTitle", "Error"),
                        GetHomeText("PlannedMealsInvalidOrderMessage", "End date must be greater than or equal to start date."),
                        ButtonResources.OK);
            }
        }
    }

    private async Task ShowMealsPopupAsync()
    {
        // Protection against multiple opens
        if (_isMealsPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Meals popup already open, ignoring request");
            return;
        }

        try
        {
            _isMealsPopupOpen = true;
            ((Command)ShowMealsPopupCommand).ChangeCanExecute();

            var page = Application.Current?.MainPage;
            if (page == null) return;

            var items = new List<object>();
            foreach (var group in PlannedMealHistory)
            {
                // Date section header
                items.Add(new Views.Components.SimpleListPopup.SectionHeader { Text = group.DateLabel });

                foreach (var meal in group.Meals)
                {
                    if (meal?.Recipe == null) continue; var fullRecipe = await _recipeService.GetRecipeAsync(meal.Recipe.Id); if (fullRecipe == null) continue;
                    
                    // Keep summary list lightweight (no inline +/- here)
                    var title = meal.Portions != fullRecipe.IloscPorcji
                        ? $"{fullRecipe.Name} ({meal.Portions} porcji)"
                        : fullRecipe.Name;
                    items.Add(new Views.Components.SimpleListPopup.MealTitle { Text = title });

                    // Macro row scaled by portions
                    var portionMultiplier = (double)meal.Portions / Math.Max(fullRecipe.IloscPorcji, 1);
                    items.Add(new Views.Components.SimpleListPopup.MacroRow
                    {
                        Calories = fullRecipe.Calories * portionMultiplier,
                        Protein = fullRecipe.Protein * portionMultiplier,
                        Fat = fullRecipe.Fat * portionMultiplier,
                        Carbs = fullRecipe.Carbs * portionMultiplier
                    });

                    // Description (optional)
                    if (!string.IsNullOrWhiteSpace(fullRecipe.Description))
                    {
                        items.Add(new Views.Components.SimpleListPopup.Description { Text = fullRecipe.Description! });
                    }
                }

                // Spacer between days
                items.Add(string.Empty);
            }

            var popup = new Views.Components.SimpleListPopup
            {
                TitleText = GetHomeText("PlannedMealsHeader", "Planned meals"),
                Items = items,
                IsBulleted = false
            };

            System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Opening meals popup");

            // Use CommunityToolkit popup extension method
            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            
            // Wait for either the popup to be dismissed or a result to be set
            await Task.WhenAny(showTask, resultTask);
            
            // Get the result (even though SimpleListPopup doesn't return meaningful data)
            var result = resultTask.IsCompleted ? await resultTask : null;
            
            System.Diagnostics.Debug.WriteLine($"? HomeViewModel: Meals popup closed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? HomeViewModel: Error showing meals popup: {ex.Message}");
            
            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Attempting to handle popup blocking");
                
                try
                {
                    // Try to dismiss any existing modal pages
                    if (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"?? HomeViewModel: Could not handle popup blocking: {modalEx.Message}");
                }
            }
        }
        finally
        {
            _isMealsPopupOpen = false;
            ((Command)ShowMealsPopupCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Meals popup protection released");
        }
    }

    /// <summary>
    /// Handles culture change events to refresh localized display properties
    /// </summary>
    private void OnCultureChanged(object? sender, EventArgs e)
    {
        try
        {
            // Refresh all display properties that depend on localized resources
            OnPropertyChanged(nameof(NutritionPeriodDisplay));
            OnPropertyChanged(nameof(PlannedMealsPeriodDisplay));
            OnPropertyChanged(nameof(PlannedMealsMaxHeight));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnCultureChanged: {ex.Message}");
        }
    }

    private static string GetHomeText(string key, string fallback)
        => HomePageResources.ResourceManager.GetString(key, HomePageResources.Culture) ?? fallback;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    ~HomeViewModel()
    {
        // Unsubscribe from events to prevent memory leaks
        _localizationService.CultureChanged -= OnCultureChanged;
        Foodbook.Services.AppEvents.PlanChangedAsync -= OnPlanChangedAsync;
    }
}

// Klasa pomocnicza do grupowania zaplanowanych posi�k�w
public class PlannedMealGroup
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public ObservableCollection<PlannedMeal> Meals { get; set; } = new();
}

// SIMPLIFIED: Enum dla okres�w statystyk �ywieniowych - Custom handled separately
public enum NutritionPeriod
{
    Day,
    Week,
    Plan,      // Filter by specific saved plan
    Custom     // Now handled separately from main menu
}

// Enum dla okres�w zaplanowanych posi�k�w
public enum PlannedMealsPeriod
{
    Today,
    Week,
    Custom
}

