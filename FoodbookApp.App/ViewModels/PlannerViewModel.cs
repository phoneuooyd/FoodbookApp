using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class PlannerViewModel : INotifyPropertyChanged
{
    private readonly IPlannerService _plannerService;
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;

    public ObservableCollection<Recipe> Recipes { get; } = new();
    public ObservableCollection<PlannerDay> Days { get; } = new();

    // Cache properties
    private bool _isDataCached = false;
    private DateTime _cachedStartDate;
    private DateTime _cachedEndDate;
    private int _cachedMealsPerDay;
    private List<Recipe> _cachedRecipes = new();
    private List<PlannerDay> _cachedDays = new();

    private bool _isEditing;
    private Guid? _editingPlanId;
    
    // NEW: Flag to suppress auto-reload when user is actively editing
    private bool _suppressAutoReload = false;
    
    public bool IsEditing
    {
        get => _isEditing;
        private set { if (_isEditing == value) return; _isEditing = value; OnPropertyChanged(); }
    }

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
            
            // FIXED: Don't auto-reload when editing
            if (!_suppressAutoReload && !IsEditing)
            {
                _ = LoadAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] StartDate changed but auto-reload suppressed (editing mode)");
            }
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
            
            // FIXED: Don't auto-reload when editing
            if (!_suppressAutoReload && !IsEditing)
            {
                _ = LoadAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] EndDate changed but auto-reload suppressed (editing mode)");
            }
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
            
            // FIXED: Only adjust meals structure, don't reload data
            if (!_suppressAutoReload)
            {
                AdjustMealsPerDay();
            }
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
            try 
            {
                var plan = await SaveAsync();
                if (plan != null)
                {
                    await Shell.Current.DisplayAlert(
                        "Zapisano",
                        $"Zapisano planer: {plan.Name} ({plan.StartDate:dd.MM.yyyy} - {plan.EndDate:dd.MM.yyyy})",
                        "OK");
                    
                    // Notify shopping lists/plans
                    Foodbook.Services.AppEvents.RaisePlanChanged();
                    
                    // Reset widoku po udanym zapisie i wyjście
                    await ResetAsync();
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (PlanLimitExceededException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Plan limit in SaveCommand: {ex.Message}");
                await Shell.Current.DisplayAlert("Limit planu", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in SaveCommand: {ex.Message}");
                await Shell.Current.DisplayAlert("Błąd", "Wystąpił nieoczekiwany błąd podczas zapisywania.", "OK");
            }
        });
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    /// <summary>
    /// Initialize view model for editing an existing plan. This only sets editing flags and dates;
    /// actual data loading can be triggered via LoadAsync or LoadForEditAsync.
    /// </summary>
    public async Task InitializeForEditAsync(Guid planId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] InitializeForEditAsync: planId={planId}");

            var plan = await _planService.GetPlanAsync(planId);
            if (plan == null)
            {
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] Plan not found");
                return;
            }

            _editingPlanId = plan.Id;
            IsEditing = true;

            _suppressAutoReload = true;

            _startDate = plan.StartDate;
            _endDate = plan.EndDate;
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Edit mode initialized: {plan.StartDate:yyyy-MM-dd} to {plan.EndDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] InitializeForEditAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Fully load planner data for the currently selected edit plan (uses StartDate/EndDate set earlier).
    /// This method forces a reload and is intended to be used when entering edit mode to ensure
    /// data is loaded specifically for the plan being edited.
    /// </summary>
    public async Task LoadForEditAsync()
    {
        System.Diagnostics.Debug.WriteLine("[PlannerViewModel] LoadForEditAsync called");
        
        // Force reload to ensure we don't use stale cache when editing
        await LoadAsync(forceReload: true);
        
        // IMPORTANT: After loading edit data, keep suppression ON to prevent auto-reloads during editing
        _suppressAutoReload = true;
        
        System.Diagnostics.Debug.WriteLine("[PlannerViewModel] LoadForEditAsync complete - auto-reload suppressed for editing");
    }

    /// <summary>
    /// Initialize a new empty planner (no planned meals) for the current StartDate/EndDate and MealsPerDay.
    /// This is intended to be used after saving a plan to present a fresh planner to the user.
    /// </summary>
    public async Task InitializeEmptyPlannerAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[PlannerViewModel] InitializeEmptyPlannerAsync called");
            
            // Detach handlers from existing meals
            foreach (var day in Days)
            {
                foreach (var meal in day.Meals)
                {
                    meal.PropertyChanged -= OnMealRecipeChanged;
                }
            }

            // Reset collections and state
            Days.Clear();
            Recipes.Clear();

            _editingPlanId = null;
            IsEditing = false;
            _suppressAutoReload = false; // Re-enable auto-reload for new planner

            _startDate = DateTime.Today;
            _endDate = DateTime.Today.AddDays(6);
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            MealsPerDay = 3;

            ClearCache();

            // Load recipes so the picker has data, but do not load planned meals from the service
            var rec = await _recipeService.GetRecipesAsync();
            foreach (var r in rec)
                Recipes.Add(r);

            // Create empty days with empty meals
            for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            {
                var day = new PlannerDay(d);
                for (int i = 0; i < MealsPerDay; i++)
                {
                    var meal = new PlannedMeal { Date = d, Portions = 1 };
                    meal.PropertyChanged += OnMealRecipeChanged;
                    day.Meals.Add(meal);
                }
                Days.Add(day);
            }
            
            System.Diagnostics.Debug.WriteLine("[PlannerViewModel] Empty planner initialized - auto-reload enabled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error initializing empty planner: {ex.Message}");
        }
    }

    public async Task LoadAsync(bool forceReload = false)
    {
        // FIXED: Don't reload if we're in edit mode and reload is suppressed
        if (_suppressAutoReload && !forceReload)
        {
            System.Diagnostics.Debug.WriteLine("[PlannerViewModel] LoadAsync skipped - auto-reload suppressed (editing mode)");
            return;
        }
        
        // Check if we can use cached data
        if (!forceReload && _isDataCached && CanUseCachedData())
        {
            RestoreFromCache();
            return;
        }

        if (IsLoading)
            return;
        
        IsLoading = true;
        LoadingProgress = 0;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] LoadAsync started - forceReload={forceReload}, isEditing={IsEditing}");
            
            // Etap 1: Czyszczenie danych
            LoadingStatus = "Przygotowywanie danych...";
            LoadingProgress = 0.1;
            await Task.Delay(50);

            Days.Clear();
            Recipes.Clear();

            // Etap 2: Ładowanie przepisów
            LoadingStatus = "Ładowanie przepisów...";
            LoadingProgress = 0.2;
            await Task.Delay(50);

            var rec = await _recipeService.GetRecipesAsync();
            
            const int batchSize = 20;
            for (int i = 0; i < rec.Count; i += batchSize)
            {
                var batch = rec.Skip(i).Take(batchSize);
                foreach (var r in batch)
                    Recipes.Add(r);
                
                var recipeProgress = 0.2 + (0.3 * (double)(i + batchSize) / Math.Max(1, rec.Count));
                LoadingProgress = Math.Min(recipeProgress, 0.5);
                
                if (i + batchSize < rec.Count)
                    await Task.Delay(10);
            }

            // Etap 3: Ładowanie istniejących posiłków
            LoadingStatus = "Ładowanie zaplanowanych posiłków...";
            LoadingProgress = 0.5;
            await Task.Delay(50);

            List<PlannedMeal> existingMeals;
            if (_editingPlanId.HasValue)
            {
                // When editing a specific plan, load its meals by PlanId only
                existingMeals = await _plannerService.GetPlannedMealsAsync(_editingPlanId.Value);
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Loaded {existingMeals.Count} meals for plan {_editingPlanId.Value}");
            }
            else
            {
                // For new planner, we do not load meals from other plans; keep empty
                existingMeals = new List<PlannedMeal>();
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] New planner - no existing meals");
            }
            
            // Etap 4: Tworzenie dni planera
            LoadingStatus = "Przygotowywanie kalendarza...";
            LoadingProgress = 0.7;
            await Task.Delay(50);

            var totalDays = (EndDate.Date - StartDate.Date).Days + 1;
            var currentDay = 0;

            int maxMeals = 0;
            for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            {
                var day = new PlannerDay(d);
                Days.Add(day);

                // Dodaj istniejące posiłki dla tego dnia (for current plan only)
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
                        Portions = existingMeal.Portions,
                        PlanId = existingMeal.PlanId
                    };
                    meal.PropertyChanged += OnMealRecipeChanged;
                    day.Meals.Add(meal);
                }
                maxMeals = Math.Max(maxMeals, day.Meals.Count);

                currentDay++;
                var dayProgress = 0.7 + (0.2 * (double)currentDay / Math.Max(1, totalDays));
                LoadingProgress = dayProgress;
                if (currentDay % 3 == 0)
                    await Task.Delay(10);
            }

            // Etap 5: Finalizacja
            LoadingStatus = "Finalizowanie...";
            LoadingProgress = 0.9;
            await Task.Delay(50);

            if (maxMeals > 0)
            {
                _suppressAutoReload = true; // Temporarily suppress to avoid triggering reload
                MealsPerDay = Math.Max(MealsPerDay, maxMeals);
                _suppressAutoReload = IsEditing; // Keep suppressed if editing
            }
            
            // IMPORTANT: Suppress auto-reload for empty slots adjustment in edit mode
            bool wasSuppressed = _suppressAutoReload;
            if (IsEditing) _suppressAutoReload = true;
            
            AdjustMealsPerDay();
            
            if (IsEditing) _suppressAutoReload = wasSuppressed;
            
            LoadingProgress = 1.0;
            await Task.Delay(100);

            // Cache the loaded data
            CacheCurrentData();
            
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] LoadAsync complete - {Days.Count} days, {existingMeals.Count} meals");
        }
        catch (Exception ex)
        {
            LoadingStatus = "Błąd ładowania danych";
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error loading planner data: {ex.Message}");
            
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

    private bool CanUseCachedData()
    {
        return _cachedStartDate == StartDate && 
               _cachedEndDate == EndDate && 
               _cachedMealsPerDay == MealsPerDay;
    }

    private void CacheCurrentData()
    {
        try
        {
            _cachedStartDate = StartDate;
            _cachedEndDate = EndDate;
            _cachedMealsPerDay = MealsPerDay;
            
            _cachedRecipes = Recipes.ToList();
            
            _cachedDays.Clear();
            foreach (var day in Days)
            {
                var cachedDay = new PlannerDay(day.Date);
                foreach (var meal in day.Meals)
                {
                    var cachedMeal = new PlannedMeal
                    {
                        Id = meal.Id,
                        RecipeId = meal.RecipeId,
                        Recipe = meal.Recipe,
                        Date = meal.Date,
                        Portions = meal.Portions,
                        PlanId = meal.PlanId
                    };
                    cachedMeal.PropertyChanged += OnMealRecipeChanged;
                    cachedDay.Meals.Add(cachedMeal);
                }
                _cachedDays.Add(cachedDay);
            }
            
            _isDataCached = true;
            System.Diagnostics.Debug.WriteLine("✅ Planner data cached successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error caching planner data: {ex.Message}");
        }
    }

    private void RestoreFromCache()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("🔄 Restoring planner data from cache");
            
            Days.Clear();
            Recipes.Clear();
            
            foreach (var recipe in _cachedRecipes)
            {
                Recipes.Add(recipe);
            }
            
            foreach (var cachedDay in _cachedDays)
            {
                var day = new PlannerDay(cachedDay.Date);
                foreach (var cachedMeal in cachedDay.Meals)
                {
                    var meal = new PlannedMeal
                    {
                        Id = cachedMeal.Id,
                        RecipeId = cachedMeal.RecipeId,
                        Recipe = cachedMeal.Recipe,
                        Date = cachedMeal.Date,
                        Portions = cachedMeal.Portions,
                        PlanId = cachedMeal.PlanId
                    };
                    meal.PropertyChanged += OnMealRecipeChanged;
                    day.Meals.Add(meal);
                }
                Days.Add(day);
            }
            
            System.Diagnostics.Debug.WriteLine("✅ Planner data restored from cache");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error restoring from cache: {ex.Message}");
            _ = LoadAsync(forceReload: true);
        }
    }

    private void ClearCache()
    {
        _isDataCached = false;
        _cachedRecipes.Clear();
        _cachedDays.Clear();
        System.Diagnostics.Debug.WriteLine("🗑️ Planner cache cleared");
    }

    private void AddMeal(PlannerDay? day)
    {
        if (day == null) return;
        var meal = new PlannedMeal { Date = day.Date, Portions = 1, PlanId = _editingPlanId };
        meal.PropertyChanged += OnMealRecipeChanged;
        day.Meals.Add(meal);
        
        System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Meal added to {day.Date:yyyy-MM-dd}");
    }

    private void RemoveMeal(PlannedMeal? meal)
    {
        if (meal == null) return;
        var day = Days.FirstOrDefault(d => d.Meals.Contains(meal));
        if (day != null)
        {
            meal.PropertyChanged -= OnMealRecipeChanged;
            day.Meals.Remove(meal);
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] RemoveMeal called: Removed meal from {day.Date:yyyy-MM-dd}");
        }
    }

    private void AdjustMealsPerDay()
    {
        System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] AdjustMealsPerDay: target={MealsPerDay}");
        
        foreach (var day in Days)
        {
            while (day.Meals.Count < MealsPerDay)
            {
                var meal = new PlannedMeal { Date = day.Date, Portions = 1, PlanId = _editingPlanId };
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
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Recipe changed: {meal.Recipe.Name} - auto-adjusting portions");
            meal.Portions = meal.Recipe.IloscPorcji;
        }
    }

    /// <summary>
    /// Reset planner to a fresh empty state. Use ResetAsync when awaiting is required.
    /// </summary>
    private async Task ResetAsync()
    {
        System.Diagnostics.Debug.WriteLine("[PlannerViewModel] ResetAsync called");
        await InitializeEmptyPlannerAsync();
    }

    private async Task<string?> PromptForPlannerNameAsync(string? currentName)
    {
        try
        {
            return await Shell.Current.DisplayPromptAsync(
                "Nazwa planu",
                "Podaj nazwę planu (opcjonalnie)",
                accept: "Zapisz",
                cancel: "Anuluj",
                placeholder: "Planner",
                initialValue: currentName ?? string.Empty,
                maxLength: 50,
                keyboard: Microsoft.Maui.Keyboard.Text);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] PromptForPlannerNameAsync error: {ex.Message}");
            return null;
        }
    }

    private async Task<Plan?> SaveAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] SaveAsync started - IsEditing={IsEditing}, PlanId={_editingPlanId}");

            // Ask for planner name first. Cancel => abort save.
            string? existingName = null;
            if (_editingPlanId.HasValue)
            {
                var existingPlan = await _planService.GetPlanAsync(_editingPlanId.Value);
                existingName = existingPlan?.PlannerName;
            }

            var enteredName = await PromptForPlannerNameAsync(existingName);
            if (enteredName == null)
                return null;

            var finalName = string.IsNullOrWhiteSpace(enteredName) ? "Planner" : enteredName.Trim();

            // Check for exact existing plan in the same period (ignore archived and the plan being edited)
            var allPlans = await _planService.GetPlansAsync();
            var conflictingPlan = allPlans.FirstOrDefault(p =>
                !p.IsArchived &&
                (p.Id != _editingPlanId) &&
                p.StartDate.Date == StartDate.Date &&
                p.EndDate.Date == EndDate.Date &&
                p.Type == PlanType.Planner);

            if (conflictingPlan != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Conflicting plan found: {conflictingPlan.Id}");
                
                // Ask user how to proceed: overwrite, merge or cancel
                var choice = await Shell.Current.DisplayActionSheet(
                    "Plan na podane daty już istnieje.",
                    "Anuluj",
                    null,
                    "Nadpisz",
                    "Scal",
                    "Zapisz i utwórz nową listę"
                );

                if (string.IsNullOrEmpty(choice) || choice == "Anuluj")
                {
                    return null;
                }

                if (choice == "Scal")
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[PlannerViewModel] User chose to merge plans");
                        
                        // Merge: add only meals that do not already exist in the existing plan
                        var existingMeals = await _plannerService.GetPlannedMealsAsync(conflictingPlan.Id);
                        var existingKeys = new HashSet<string>(existingMeals.Select(m => $"{m.Date.Date:yyyy-MM-dd}|{m.RecipeId}"));

                        foreach (var day in Days)
                        {
                            foreach (var meal in day.Meals)
                            {
                                if (meal.RecipeId == Guid.Empty) continue;
                                var key = $"{meal.Date.Date:yyyy-MM-dd}|{meal.RecipeId}";
                                if (existingKeys.Contains(key))
                                    continue;

                                await _plannerService.AddPlannedMealAsync(new PlannedMeal
                                {
                                    RecipeId = meal.RecipeId,
                                    Date = meal.Date,
                                    Portions = meal.Portions,
                                    PlanId = conflictingPlan.Id
                                });
                            }
                        }

                        await Shell.Current.DisplayAlert(
                            "Scalono",
                            $"Posiłki zostały scalone z istniejącym planem ({conflictingPlan.StartDate:dd.MM.yyyy} - {conflictingPlan.EndDate:dd.MM.yyyy}).",
                            "OK");

                        // Notify and reset/navigate back similar to normal save
                        Foodbook.Services.AppEvents.RaisePlanChanged();
                        await ResetAsync();
                        await Shell.Current.GoToAsync("..");

                        return conflictingPlan;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error during merge: {ex.Message}");
                        await Shell.Current.DisplayAlert("Błąd", "Scalanie planów nie powiodło się.", "OK");
                        return null;
                    }
                }

                if (choice == "Nadpisz")
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[PlannerViewModel] User chose to overwrite existing plan");
                        
                        // Remove the conflicting plan entity first
                        await _planService.RemovePlanAsync(conflictingPlan.Id);
                        // continue to normal save flow
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Failed to remove existing plan during overwrite: {ex.Message}");
                        await Shell.Current.DisplayAlert("Błąd", "Nie udało się usunąć istniejącego planu.", "OK");
                        return null;
                    }
                }
            }

            Plan plan;
            if (_editingPlanId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Updating existing plan: {_editingPlanId.Value}");
                
                plan = await _planService.GetPlanAsync(_editingPlanId.Value) ?? new Plan { Type = PlanType.Planner };
                plan.StartDate = StartDate;
                plan.EndDate = EndDate;
                plan.PlannerName = finalName;
                if (plan.Id == Guid.Empty)
                    await _planService.AddPlanAsync(plan);
                else
                    await _planService.UpdatePlanAsync(plan);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] Creating new plan");
                
                plan = new Plan 
                { 
                    StartDate = StartDate, 
                    EndDate = EndDate,
                    Type = PlanType.Planner,
                    PlannerName = finalName
                };
                await _planService.AddPlanAsync(plan);
                _editingPlanId = plan.Id;
                IsEditing = true;
            }

            // Remove meals only for this plan (if existing) or for this date range old entries of this plan
            if (plan.Id != Guid.Empty)
            {
                var existing = await _plannerService.GetPlannedMealsAsync(plan.Id);
                foreach (var m in existing)
                    await _plannerService.RemovePlannedMealAsync(m.Id);

                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Removed {existing.Count} old meals");
            }

            // Add meals for current plan only
            int savedMealsCount = 0;
            foreach (var day in Days)
            {
                foreach (var meal in day.Meals)
                {
                    if (meal.Recipe != null)
                        meal.RecipeId = meal.Recipe.Id;
                    if (meal.RecipeId != Guid.Empty)
                    {
                        await _plannerService.AddPlannedMealAsync(new PlannedMeal
                        {
                            RecipeId = meal.RecipeId,
                            Date = meal.Date,
                            Portions = meal.Portions,
                            PlanId = plan.Id
                        });
                        savedMealsCount++;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Saved {savedMealsCount} meals for plan {plan.Id}");

            // Ask user about shopping list handling
            await HandleShoppingListDialogAsync(plan);

            ClearCache();
            
            return plan;
        }
        catch (PlanLimitExceededException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Plan limit exceeded: {ex.Message}");
            await Shell.Current.DisplayAlert("Limit planu", ex.Message, "OK");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] ❌ Error saving meal plan: {ex.Message}\n{ex.StackTrace}");
            // Show detailed error to help diagnosing why save fails
            var message = ex.Message;
            if (ex.InnerException != null)
                message += "\n" + ex.InnerException.Message;
            await Shell.Current.DisplayAlert("Błąd", $"Wystąpił problem podczas zapisywania planu:\n{message}", "OK");
            return null;
        }
    }

    /// <summary>
    /// Handle shopping list creation/update through user dialog
    /// </summary>
    private async Task HandleShoppingListDialogAsync(Plan plan)
    {
        try
        {
            // Check if this planner already has a linked shopping list
            Plan? existingShoppingList = null;
            Plan? manualShoppingList = null;
            
            if (plan.LinkedShoppingListPlanId.HasValue)
            {
                existingShoppingList = await _planService.GetPlanAsync(plan.LinkedShoppingListPlanId.Value);
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Found linked shopping list: {existingShoppingList?.Id}");
            }

            // Find a manual (unlinked) shopping list for the same date range
            var allPlans = await _planService.GetPlansAsync();
            manualShoppingList = allPlans.FirstOrDefault(p =>
                !p.IsArchived &&
                p.Type == PlanType.ShoppingList &&
                p.StartDate.Date == plan.StartDate.Date &&
                p.EndDate.Date == plan.EndDate.Date &&
                (!p.LinkedShoppingListPlanId.HasValue || p.LinkedShoppingListPlanId == Guid.Empty)
            );
            if (manualShoppingList != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Found manual shopping list: {manualShoppingList.Id}");
            }

            string? choice = null;
            if (existingShoppingList != null && !existingShoppingList.IsArchived)
            {
                // Shopping list exists and is linked - offer merge or overwrite options
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Existing linked shopping list found: {existingShoppingList.Id}");
                choice = await Shell.Current.DisplayActionSheet(
                    "Czy chcesz zaktualizować powiązaną listę zakupów?",
                    "Anuluj",
                    null,
                    "Tylko zapisz planer",
                    "Zapisz i scal z listą",
                    "Zapisz i nadpisz listę"
                );
            }
            else if (manualShoppingList != null)
            {
                // Manual shopping list exists for this date range
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Manual shopping list found: {manualShoppingList.Id}");
                choice = await Shell.Current.DisplayActionSheet(
                    "Znaleziono ręcznie utworzoną listę zakupów dla tego zakresu dat. Co chcesz zrobić?",
                    "Anuluj",
                    null,
                    "Tylko zapisz planer",
                    "Scal z ręcznie utworzoną listą",
                    "Zapisz i utwórz nową listę"
                );
            }
            else
            {
                // No shopping list exists or it was archived - offer to create one
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] No linked shopping list found for this planner");
                choice = await Shell.Current.DisplayActionSheet(
                    "Czy chcesz stworzyć listę zakupów?",
                    "Anuluj",
                    null,
                    "Tylko zapisz planer",
                    "Zapisz i stwórz listę"
                );
            }

            // Handle user choice
            if (string.IsNullOrEmpty(choice) || choice == "Anuluj" || choice == "Tylko zapisz planer")
            {
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] User chose to skip shopping list operation");
                return;
            }

            if (choice == "Zapisz i stwórz listę" || choice == "Zapisz i utwórz nową listę")
            {
                await CreateShoppingListPlanAsync(plan);
            }
            else if (choice == "Zapisz i scal z listą")
            {
                await MergeWithShoppingListAsync(existingShoppingList!, plan);
            }
            else if (choice == "Zapisz i nadpisz listę")
            {
                await OverwriteShoppingListAsync(existingShoppingList!, plan);
            }
            else if (choice == "Scal z ręcznie utworzoną listą")
            {
                // Link manual list to this planner and update
                plan.LinkedShoppingListPlanId = manualShoppingList!.Id;
                await _planService.UpdatePlanAsync(plan);
                System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Linked manual shopping list {manualShoppingList.Id} to planner {plan.Id}");
                await MergeWithShoppingListAsync(manualShoppingList, plan);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error handling shopping list: {ex.Message}");
            // Don't throw - shopping list is optional
        }
    }

    /// <summary>
    /// Create a new shopping list plan for the same date range and link it
    /// </summary>
    private async Task CreateShoppingListPlanAsync(Plan plan)
    {
        try
        {
            var newShoppingPlan = new Plan
            {
                StartDate = plan.StartDate,
                EndDate = plan.EndDate,
                Type = PlanType.ShoppingList,
                IsArchived = false
            };
            await _planService.AddPlanAsync(newShoppingPlan);
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Created new shopping list plan with ID: {newShoppingPlan.Id}");
            
            // Link the shopping list to this planner
            plan.LinkedShoppingListPlanId = newShoppingPlan.Id;
            await _planService.UpdatePlanAsync(plan);
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Linked shopping list {newShoppingPlan.Id} to planner {plan.Id}");
        }
        catch (PlanLimitExceededException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Shopping list limit exceeded: {ex.Message}");
            await Shell.Current.DisplayAlert("Limit planu", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error creating shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("Błąd", "Nie udało się stworzyć listy zakupów", "OK");
        }
    }

    /// <summary>
    /// Merge with existing shopping list (keep existing items, update dates)
    /// </summary>
    private async Task MergeWithShoppingListAsync(Plan shoppingPlan, Plan mealPlan)
    {
        try
        {
            // Update dates to match planner
            bool needsUpdate = false;
            
            if (shoppingPlan.StartDate != mealPlan.StartDate || shoppingPlan.EndDate != mealPlan.EndDate)
            {
                shoppingPlan.StartDate = mealPlan.StartDate;
                shoppingPlan.EndDate = mealPlan.EndDate;
                needsUpdate = true;
            }

            if (shoppingPlan.IsArchived)
            {
                shoppingPlan.IsArchived = false;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                await _planService.UpdatePlanAsync(shoppingPlan);
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] Merged with existing shopping list (updated dates)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PlannerViewModel] Shopping list already up to date");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error merging shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("Błąd", "Nie udało się scalić listy zakupów", "OK");
        }
    }

    /// <summary>
    /// Overwrite existing shopping list (recreate from scratch)
    /// </summary>
    private async Task OverwriteShoppingListAsync(Plan shoppingPlan, Plan mealPlan)
    {
        try
        {
            // Remove the old shopping list
            await _planService.RemovePlanAsync(shoppingPlan.Id);
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Removed old shopping list: {shoppingPlan.Id}");
            
            // Create a new one (which will also update the link)
            await CreateShoppingListPlanAsync(mealPlan);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] Error overwriting shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("Błąd", "Nie udało się nadpisać listy zakupów", "OK");
        }
    }

    private void IncreasePortions(PlannedMeal? meal)
    {
        if (meal != null && meal.Portions < 20)
        {
            meal.Portions++;
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] IncreasePortions called: Portions = {meal.Portions}");
        }
    }

    private void DecreasePortions(PlannedMeal? meal)
    {
        if (meal != null && meal.Portions > 1)
        {
            meal.Portions--;
            System.Diagnostics.Debug.WriteLine($"[PlannerViewModel] DecreasePortions called: Portions = {meal.Portions}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
