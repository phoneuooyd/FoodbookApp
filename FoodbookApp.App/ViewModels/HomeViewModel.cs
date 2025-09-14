using System.ComponentModel;
using System.Runtime.CompilerServices;
using Foodbook.Services;
using Foodbook.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using FoodbookApp.Localization;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;

namespace Foodbook.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;
    private readonly ILocalizationService _localizationService;
    private bool _isRecipeIngredientsPopupOpen = false; // Protection against multiple opens

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

    // Planned meals period settings
    private PlannedMealsPeriod _selectedPlannedMealsPeriod = PlannedMealsPeriod.Week;
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
                _ = LoadPlannedMealsAsync(); 
            } 
        }
    }

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
    public ICommand ChangeNutritionPeriodCommand { get; }
    public ICommand ChangePlannedMealsPeriodCommand { get; }

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
        ShowMealsPopupCommand = new Command(async () => await ShowMealsPopupAsync());
        ChangeNutritionPeriodCommand = new Command(async () => await ShowNutritionPeriodPickerAsync());
        ChangePlannedMealsPeriodCommand = new Command(async () => await ShowPlannedMealsPeriodPickerAsync());
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

            // Load available plans for filtering
            await LoadAvailablePlansAsync();

            // £aduj zaplanowane posi³ki
            await LoadPlannedMealsAsync();
            
            // Oblicz statystyki ¿ywieniowe
            await RefreshNutritionDataAsync(); // Use centralized refresh method
        }
        catch (Exception ex)
        {
            // W przypadku b³êdu, ustaw wartoœci na 0
            RecipeCount = 0;
            PlanCount = 0;
            ArchivedPlanCount = 0;
            PlannedMealHistory.Clear();
            HasPlannedMeals = false;
            AvailablePlans.Clear();
            ResetNutritionStats();
            
            // Log b³êdu (opcjonalne)
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
            var plans = await _planService.GetPlansAsync();
            var activePlans = plans?.Where(p => !p.IsArchived).OrderByDescending(p => p.StartDate).ToList() ?? new List<Plan>();
            
            AvailablePlans.Clear();
            foreach (var plan in activePlans)
            {
                AvailablePlans.Add(plan);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading available plans: {ex.Message}");
            AvailablePlans.Clear();
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

                // Pobierz pe³ny przepis ze sk³adnikami
                var fullRecipe = await _recipeService.GetRecipeAsync(meal.Recipe.Id);
                if (fullRecipe == null) continue;

                // Oblicz wartoœci na podstawie liczby porcji
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
            options.Add("Plan");
        }
        
        // RESTORED: Add custom date option back
        options.Add(ButtonResources.CustomDate);

        var choice = await page.DisplayActionSheet(
            "Wybierz okres dla statystyk", 
            "Anuluj", 
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
            case "Plan":
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
            await page.DisplayAlert("Brak planów", "Nie ma dostêpnych planów do wyboru.", "OK");
            return;
        }

        var planOptions = AvailablePlans.Select(p => $"{p.StartDate:dd.MM.yyyy} - {p.EndDate:dd.MM.yyyy}").ToArray();
        
        var choice = await page.DisplayActionSheet(
            "Wybierz plan", 
            "Anuluj", 
            null, 
            planOptions);

        if (!string.IsNullOrEmpty(choice) && choice != "Anuluj")
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
                "Data pocz¹tkowa", 
                "Podaj datê pocz¹tkow¹ (dd.mm.yyyy):", 
                "OK",
                "Anuluj",
                placeholder: "np. 01.01.2024",
                initialValue: CustomStartDate.ToString("dd.MM.yyyy"));

            if (string.IsNullOrEmpty(startDateInput))
                return;

            if (DateTime.TryParseExact(startDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var startDate))
            {
                var endDateInput = await page.DisplayPromptAsync(
                    "Data koñcowa", 
                    "Podaj datê koñcow¹ (dd.mm.yyyy):", 
                    "OK",
                    "Anuluj",
                    placeholder: "np. 07.01.2024",
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
                            "Zakres ustawiony", 
                            $"Filtr ustawiony na okres:\n{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}", 
                            "OK");
                    }
                    else
                    {
                        await page.DisplayAlert("B³¹d", "Data koñcowa musi byæ póŸniejsza lub równa dacie pocz¹tkowej", "OK");
                    }
                }
                else
                {
                    await page.DisplayAlert("B³¹d", "Nieprawid³owy format daty koñcowej. U¿yj formatu dd.mm.yyyy", "OK");
                }
            }
            else
            {
                await page.DisplayAlert("B³¹d", "Nieprawid³owy format daty pocz¹tkowej. U¿yj formatu dd.mm.yyyy", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing custom date range picker: {ex.Message}");
            await page.DisplayAlert("B³¹d", "Wyst¹pi³ b³¹d podczas ustawiania zakresu dat", "OK");
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
            "Wybierz okres dla zaplanowanych posi³ków", 
            "Anuluj", 
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
            "Data pocz¹tkowa", 
            "Podaj datê pocz¹tkow¹ (dd.mm.yyyy):", 
            initialValue: PlannedMealsCustomStartDate.ToString("dd.MM.yyyy"));

        if (DateTime.TryParseExact(startDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var startDate))
        {
            var endDateInput = await page.DisplayPromptAsync(
                "Data koñcowa", 
                "Podaj datê koñcow¹ (dd.mm.yyyy):", 
                initialValue: PlannedMealsCustomEndDate.ToString("dd.MM.yyyy"));

            if (DateTime.TryParseExact(endDateInput, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var endDate))
            {
                if (endDate >= startDate)
                {
                    PlannedMealsCustomStartDate = startDate;
                    PlannedMealsCustomEndDate = endDate;
                    SelectedPlannedMealsPeriod = PlannedMealsPeriod.Custom;
                }
                else
                {
                    await page.DisplayAlert("B³¹d", "Data koñcowa musi byæ póŸniejsza ni¿ pocz¹tkowa", "OK");
                }
            }
        }
    }

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

            // Pobierz pe³ny przepis ze sk³adnikami z serwisu
            var fullRecipe = await _recipeService.GetRecipeAsync(meal.Recipe.Id);
            if (fullRecipe == null)
            {
                await page.DisplayAlert(
                    "B³¹d", 
                    "Nie uda³o siê pobraæ przepisu.", 
                    "OK");
                return;
            }

            // Przygotuj nag³ówek
            var title = $"{fullRecipe.Name}";
            if (meal.Portions != fullRecipe.IloscPorcji)
            {
                title += $" ({meal.Portions} porcji)";
            }

            // Przygotuj treœæ
            var content = "";

            // Dodaj sk³adniki je¿eli istniej¹
            if (fullRecipe.Ingredients != null && fullRecipe.Ingredients.Any())
            {
                content += "SK£ADNIKI:\n";
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
                content += "SK£ADNIKI:\nBrak zdefiniowanych sk³adników.";
            }

            // Dodaj sposób wykonania je¿eli istnieje
            if (!string.IsNullOrWhiteSpace(fullRecipe.Description))
            {
                content += "\n\nSPOSÓB WYKONANIA:\n";
                content += fullRecipe.Description;
            }

            await page.DisplayAlert(title, content, "OK");
            
            System.Diagnostics.Debug.WriteLine($"? HomeViewModel: Recipe ingredients popup closed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? HomeViewModel: Error showing recipe ingredients: {ex.Message}");
            
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
            
            await page.DisplayAlert(
                "B³¹d", 
                "Nie uda³o siê pobraæ szczegó³ów przepisu.", 
                "OK");
        }
        finally
        {
            _isRecipeIngredientsPopupOpen = false;
            ((Command<PlannedMeal>)ShowRecipeIngredientsCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? HomeViewModel: Recipe ingredients popup protection released");
        }
    }

    private string GetUnitText(Unit unit)
    {
        return unit switch
        {
            Unit.Gram => FoodbookApp.Localization.UnitResources.GramShort,
            Unit.Milliliter => FoodbookApp.Localization.UnitResources.MilliliterShort, 
            Unit.Piece => FoodbookApp.Localization.UnitResources.PieceShort,
            _ => ""
        };
    }

    private string GetDateLabel(DateTime date)
    {
        // Zawsze pokazuj datê zamiast s³ów "dzisiaj", "jutro"
        return date.ToString("dddd, dd.MM.yyyy");
    }

    private async Task ShowMealsPopupAsync()
    {
        try
        {
            var page = Application.Current?.MainPage;
            if (page == null) return;

            var lines = new List<string>();
            foreach (var group in PlannedMealHistory)
            {
                lines.Add(group.DateLabel);
                foreach (var meal in group.Meals)
                {
                    var text = $"{meal.Recipe?.Name} ({meal.Portions} porcji)";
                    lines.Add(text);
                }
                lines.Add(string.Empty);
            }

            var popup = new Views.Components.SimpleListPopup
            {
                TitleText = "Zaplanowane posi³ki",
                Items = lines,
                IsBulleted = true
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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnCultureChanged: {ex.Message}");
        }
    }

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

// Klasa pomocnicza do grupowania zaplanowanych posi³ków
public class PlannedMealGroup
{
    public DateTime Date { get; set; }
    public string DateLabel { get; set; } = string.Empty;
    public ObservableCollection<PlannedMeal> Meals { get; set; } = new();
}

// SIMPLIFIED: Enum dla okresów statystyk ¿ywieniowych - Custom handled separately
public enum NutritionPeriod
{
    Day,
    Week,
    Plan,      // Filter by specific saved plan
    Custom     // Now handled separately from main menu
}

// Enum dla okresów zaplanowanych posi³ków
public enum PlannedMealsPeriod
{
    Today,
    Week,
    Custom
}

