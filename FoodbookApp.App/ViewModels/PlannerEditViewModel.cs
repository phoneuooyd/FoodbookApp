using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

/// <summary>
/// Dedicated ViewModel for EDITING existing planners.
/// This ViewModel NEVER auto-reloads data - it only loads once and preserves all user changes.
/// </summary>
public class PlannerEditViewModel : INotifyPropertyChanged
{
    private readonly IPlannerService _plannerService;
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;

    // Core data collections
    public ObservableCollection<Recipe> Recipes { get; } = new();
    public ObservableCollection<PlannerDay> Days { get; } = new();

    // Plan being edited
    private int _planId;
    private Plan? _currentPlan;
    
    // Track user's MealsPerDay changes independently from database value
    private int _userDefinedMealsPerDay = 3;

    // Loading state
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

    // Date properties - NO AUTO-RELOAD on change
    private DateTime _startDate = DateTime.Today;
    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            if (_startDate == value) return;
            _startDate = value;
            OnPropertyChanged();
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] StartDate changed to {value:yyyy-MM-dd}");
            
            // When date changes, rebuild days structure (add/remove days as needed)
            RebuildDaysForDateRangeAsync();
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
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] EndDate changed to {value:yyyy-MM-dd}");
            
            // When date changes, rebuild days structure (add/remove days as needed)
            RebuildDaysForDateRangeAsync();
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
            _userDefinedMealsPerDay = value; // Remember user's choice
            OnPropertyChanged();
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] MealsPerDay changed to {value} - adjusting structure only");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Saved user preference: _userDefinedMealsPerDay = {_userDefinedMealsPerDay}");
            AdjustMealsPerDayStructure();
        }
    }

    // Commands
    public ICommand AddMealCommand { get; }
    public ICommand RemoveMealCommand { get; }
    public ICommand IncreasePortionsCommand { get; }
    public ICommand DecreasePortionsCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public PlannerEditViewModel(
        IPlannerService plannerService,
        IRecipeService recipeService,
        IPlanService planService)
    {
        _plannerService = plannerService ?? throw new ArgumentNullException(nameof(plannerService));
        _recipeService = recipeService ?? throw new ArgumentNullException(nameof(recipeService));
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));

        AddMealCommand = new Command<PlannerDay>(AddMeal);
        RemoveMealCommand = new Command<PlannedMeal>(RemoveMeal);
        IncreasePortionsCommand = new Command<PlannedMeal>(IncreasePortions);
        DecreasePortionsCommand = new Command<PlannedMeal>(DecreasePortions);
        SaveCommand = new Command(async () => await SaveAsync());
        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    /// <summary>
    /// Load plan data for editing. This is called ONCE when entering edit mode.
    /// After this, NO automatic reloads occur.
    /// </summary>
    public async Task LoadPlanForEditAsync(int planId)
    {
        if (IsLoading)
        {
            System.Diagnostics.Debug.WriteLine("[PlannerEditVM] Already loading, skipping");
            return;
        }

        IsLoading = true;
        _planId = planId;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ===== LOADING PLAN {planId} FOR EDIT =====");

            // Step 1: Load plan metadata
            _currentPlan = await _planService.GetPlanAsync(planId);
            
            if (_currentPlan == null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ERROR: Plan {planId} not found in database");
                await Shell.Current.DisplayAlert("B³¹d", "Nie znaleziono planu", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            _startDate = _currentPlan.StartDate;
            _endDate = _currentPlan.EndDate;
            OnPropertyChanged(nameof(StartDate));
            OnPropertyChanged(nameof(EndDate));

            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Plan loaded from DB:");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   ID: {_currentPlan.Id}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Type: {_currentPlan.Type}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Start: {_startDate:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   End: {_endDate:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Archived: {_currentPlan.IsArchived}");

            // Step 2: Load all recipes (for picker)
            var recipes = await _recipeService.GetRecipesAsync();
            Recipes.Clear();
            foreach (var recipe in recipes)
            {
                Recipes.Add(recipe);
            }
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Loaded {recipes.Count} recipes from DB");

            // Step 3: Load planned meals FOR THIS SPECIFIC PLAN ONLY
            // IMPORTANT: Use planId-based query, not date-based query
            var plannedMeals = await _plannerService.GetPlannedMealsAsync(planId);
            
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Loaded {plannedMeals.Count} planned meals for plan {planId}:");
            foreach (var meal in plannedMeals.OrderBy(m => m.Date))
            {
                var recipe = Recipes.FirstOrDefault(r => r.Id == meal.RecipeId);
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - {meal.Date:yyyy-MM-dd}: Recipe ID {meal.RecipeId} ({recipe?.Name ?? "NOT FOUND"}) x{meal.Portions}");
            }

            // Step 4: Build days structure
            Days.Clear();

            // Group meals by date
            var mealsByDate = plannedMeals
                .GroupBy(m => m.Date.Date)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.Id).ToList());

            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Meals grouped by date: {mealsByDate.Count} dates");

            // Create PlannerDay for each date in range, populated with meals if they exist
            for (var date = _startDate.Date; date <= _endDate.Date; date = date.AddDays(1))
            {
                var day = new PlannerDay(date);
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Created day {date:yyyy-MM-dd}");

                // Load meals for this specific date if they exist in the dictionary
                if (mealsByDate.TryGetValue(date, out var mealsForDay))
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Found {mealsForDay.Count} meals for this date");
                    
                    foreach (var meal in mealsForDay)
                    {
                        var recipe = Recipes.FirstOrDefault(r => r.Id == meal.RecipeId);
                        
                        if (recipe == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   WARNING: Recipe {meal.RecipeId} not found in Recipes collection");
                        }

                        var plannedMeal = new PlannedMeal
                        {
                            Id = meal.Id,
                            RecipeId = meal.RecipeId,
                            Recipe = recipe,
                            Date = meal.Date,
                            Portions = meal.Portions,
                            PlanId = meal.PlanId
                        };
                        
                        // Hook up change handler to auto-adjust portions when recipe changes
                        plannedMeal.PropertyChanged += OnMealRecipeChanged;
                        
                        day.Meals.Add(plannedMeal);
                        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Added meal: {recipe?.Name ?? "Unknown"} ({meal.Portions} portions)");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   No meals for this date");
                }

                Days.Add(day);
            }

            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Created {Days.Count} days");

            // Determine max meals per day from loaded data
            var maxMeals = Days.Count > 0 ? Days.Max(d => d.Meals.Count) : 0;
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Max meals in any day: {maxMeals}");

            if (maxMeals > 0)
            {
                _mealsPerDay = maxMeals;
                _userDefinedMealsPerDay = maxMeals; // Initialize user preference with loaded value
                OnPropertyChanged(nameof(MealsPerDay));
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Set MealsPerDay to {_mealsPerDay}");
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Initialized user preference: _userDefinedMealsPerDay = {_userDefinedMealsPerDay}");
            }

            // Ensure all days have the same number of meal slots (add empty slots if needed)
            AdjustMealsPerDayStructure();

            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ===== LOAD COMPLETE =====");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Final state:");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - Days: {Days.Count}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - MealsPerDay: {MealsPerDay}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - Total meals: {Days.Sum(d => d.Meals.Count)}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - Meals with recipes: {Days.Sum(d => d.Meals.Count(m => m.Recipe != null))}");
            System.Diagnostics.Debug.WriteLine("[PlannerEditVM] NO AUTO-RELOAD will occur from this point");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ERROR loading plan: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Stack trace: {ex.StackTrace}");
            await Shell.Current.DisplayAlert("B³¹d", $"Nie uda³o siê za³adowaæ planu: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            // Turn off loading indicator immediately
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine("[PlannerEditVM] Loading indicator OFF");
        }
    }

    /// <summary>
    /// Rebuild days structure when date range changes WITHOUT reloading data from database
    /// Preserves existing meals and their associations
    /// </summary>
    private async Task RebuildDaysForDateRangeAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ===== REBUILDING DAYS FOR NEW DATE RANGE =====");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] New range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Current user preference: _userDefinedMealsPerDay = {_userDefinedMealsPerDay}");

        // Save current meals in a temporary structure
        var savedMeals = new Dictionary<DateTime, List<PlannedMeal>>();
        
        foreach (var day in Days)
        {
            if (day.Meals.Count > 0)
            {
                var mealList = new List<PlannedMeal>();
                foreach (var meal in day.Meals)
                {
                    mealList.Add(new PlannedMeal
                    {
                        Id = meal.Id,
                        RecipeId = meal.RecipeId,
                        Recipe = meal.Recipe,
                        Date = meal.Date,
                        Portions = meal.Portions,
                        PlanId = meal.PlanId
                    });
                }
                savedMeals[day.Date] = mealList;
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Saved {mealList.Count} meals from {day.Date:yyyy-MM-dd}");
            }
        }

        // Clear current days
        Days.Clear();

        // Recreate days for new date range
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Creating days for new range");
        for (var date = StartDate.Date; date <= EndDate.Date; date = date.AddDays(1))
        {
            var day = new PlannerDay(date);

            // Restore meals if they exist for this date
            if (savedMeals.TryGetValue(date, out var mealsForDay))
            {
                foreach (var meal in mealsForDay)
                {
                    meal.PropertyChanged += OnMealRecipeChanged;
                    day.Meals.Add(meal);
                }
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   {date:yyyy-MM-dd}: restored {mealsForDay.Count} meals");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   {date:yyyy-MM-dd}: new day (no meals)");
            }

            Days.Add(day);
        }

        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Created {Days.Count} days");

        // Restore user's MealsPerDay preference instead of recalculating
        _mealsPerDay = _userDefinedMealsPerDay;
        OnPropertyChanged(nameof(MealsPerDay));
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Restored MealsPerDay to user preference: {_mealsPerDay}");

        // Ensure all days have the same number of meal slots
        AdjustMealsPerDayStructure();

        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ===== REBUILD COMPLETE =====");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Final state:");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - Days: {Days.Count}");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - MealsPerDay: {MealsPerDay}");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - Total meals: {Days.Sum(d => d.Meals.Count)}");
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   - Meals with recipes: {Days.Sum(d => d.Meals.Count(m => m.Recipe != null))}");
    }

    /// <summary>
    /// Adjust the number of meal slots per day WITHOUT reloading data from database
    /// </summary>
    private void AdjustMealsPerDayStructure()
    {
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Adjusting meal slots to {MealsPerDay} per day");

        foreach (var day in Days)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Day {day.Date:yyyy-MM-dd}: currently has {day.Meals.Count} meals");
            
            // Add empty slots if needed
            while (day.Meals.Count < MealsPerDay)
            {
                var meal = new PlannedMeal 
                { 
                    Date = day.Date, 
                    Portions = 1, 
                    PlanId = _planId 
                };
                meal.PropertyChanged += OnMealRecipeChanged;
                day.Meals.Add(meal);
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Added empty meal slot");
            }

            // Remove excess slots if needed (only remove empty slots without recipes)
            while (day.Meals.Count > MealsPerDay)
            {
                // Try to remove from the end, but only if it's an empty slot
                var lastMeal = day.Meals[day.Meals.Count - 1];
                
                // Only remove if meal has no recipe selected
                if (lastMeal.Recipe == null && lastMeal.RecipeId == 0)
                {
                    lastMeal.PropertyChanged -= OnMealRecipeChanged;
                    day.Meals.RemoveAt(day.Meals.Count - 1);
                    System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   Removed empty meal slot");
                }
                else
                {
                    // If last meal has a recipe, we can't remove it automatically
                    // User will need to manually remove meals or increase MealsPerDay
                    System.Diagnostics.Debug.WriteLine($"[PlannerEditVM]   WARNING: Cannot auto-remove meal with recipe '{lastMeal.Recipe?.Name}' - user must manually adjust");
                    break;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Day {day.Date:yyyy-MM-dd}: adjusted to {day.Meals.Count} meal slots");
        }
    }

    private void AddMeal(PlannerDay? day)
    {
        if (day == null) return;
        
        var meal = new PlannedMeal 
        { 
            Date = day.Date, 
            Portions = 1, 
            PlanId = _planId 
        };
        meal.PropertyChanged += OnMealRecipeChanged;
        day.Meals.Add(meal);
        
        System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Added meal to {day.Date:yyyy-MM-dd}");
    }

    private void RemoveMeal(PlannedMeal? meal)
    {
        if (meal == null) return;
        
        var day = Days.FirstOrDefault(d => d.Meals.Contains(meal));
        if (day != null)
        {
            meal.PropertyChanged -= OnMealRecipeChanged;
            day.Meals.Remove(meal);
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Removed meal from {day.Date:yyyy-MM-dd}");
        }
    }

    private void IncreasePortions(PlannedMeal? meal)
    {
        if (meal != null && meal.Portions < 20)
        {
            meal.Portions++;
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Increased portions to {meal.Portions}");
        }
    }

    private void DecreasePortions(PlannedMeal? meal)
    {
        if (meal != null && meal.Portions > 1)
        {
            meal.Portions--;
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Decreased portions to {meal.Portions}");
        }
    }

    /// <summary>
    /// Auto-adjust portions when recipe is selected in picker
    /// </summary>
    private void OnMealRecipeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlannedMeal.Recipe) && sender is PlannedMeal meal && meal.Recipe != null)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Recipe changed to '{meal.Recipe.Name}' - auto-adjusting portions to {meal.Recipe.IloscPorcji}");
            meal.Portions = meal.Recipe.IloscPorcji;
        }
    }

    /// <summary>
    /// Save all changes to the plan
    /// </summary>
    private async Task SaveAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] ===== SAVING PLAN {_planId} =====");

            if (_currentPlan == null)
            {
                await Shell.Current.DisplayAlert("B³¹d", "Nie znaleziono planu do zapisania", "OK");
                return;
            }

            // Update plan dates if changed
            _currentPlan.StartDate = StartDate;
            _currentPlan.EndDate = EndDate;
            await _planService.UpdatePlanAsync(_currentPlan);
            
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Plan updated: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}");

            // Remove ALL existing meals for this plan
            var existingMeals = await _plannerService.GetPlannedMealsAsync(_planId);
            foreach (var meal in existingMeals)
            {
                await _plannerService.RemovePlannedMealAsync(meal.Id);
            }
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Removed {existingMeals.Count} old meals");

            // Save all current meals (only those with recipes selected)
            int savedCount = 0;
            foreach (var day in Days)
            {
                foreach (var meal in day.Meals)
                {
                    // Update RecipeId from Recipe if set
                    if (meal.Recipe != null)
                    {
                        meal.RecipeId = meal.Recipe.Id;
                    }

                    // Only save meals that have a recipe selected
                    if (meal.RecipeId > 0)
                    {
                        await _plannerService.AddPlannedMealAsync(new PlannedMeal
                        {
                            RecipeId = meal.RecipeId,
                            Date = meal.Date,
                            Portions = meal.Portions,
                            PlanId = _planId
                        });
                        savedCount++;
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Saved {savedCount} meals");

            // Ask user about shopping list handling
            await HandleShoppingListDialogAsync();

            // Notify other parts of app
            Foodbook.Services.AppEvents.RaisePlanChanged();

            await Shell.Current.DisplayAlert(
                "Zapisano",
                $"Plan zosta³ zaktualizowany ({StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy})",
                "OK");

            System.Diagnostics.Debug.WriteLine("[PlannerEditVM] ===== SAVE COMPLETE =====");

            // Navigate back
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Error saving: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Stack trace: {ex.StackTrace}");
            
            var message = ex.Message;
            if (ex.InnerException != null)
                message += "\n" + ex.InnerException.Message;
            
            await Shell.Current.DisplayAlert("B³¹d", $"Nie uda³o siê zapisaæ planu:\n{message}", "OK");
        }
    }

    /// <summary>
    /// Handle shopping list creation/update through user dialog
    /// </summary>
    private async Task HandleShoppingListDialogAsync()
    {
        try
        {
            var allPlans = await _planService.GetPlansAsync();
            // Check if this planner already has a linked shopping list
            Plan? existingShoppingList = null;
            Plan? manualShoppingList = null;
            
            if (_currentPlan?.LinkedShoppingListPlanId.HasValue == true)
            {
                existingShoppingList = await _planService.GetPlanAsync(_currentPlan.LinkedShoppingListPlanId.Value);
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Found linked shopping list: {existingShoppingList?.Id}");
            }
            // Find a manual (unlinked) shopping list for the same date range
            manualShoppingList = allPlans.FirstOrDefault(p =>
                !p.IsArchived &&
                p.Type == PlanType.ShoppingList &&
                p.StartDate.Date == StartDate.Date &&
                p.EndDate.Date == EndDate.Date &&
                (!p.LinkedShoppingListPlanId.HasValue || p.LinkedShoppingListPlanId == 0)
            );
            if (manualShoppingList != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Found manual shopping list: {manualShoppingList.Id}");
            }

            string? choice = null;
            if (existingShoppingList != null && !existingShoppingList.IsArchived)
            {
                // Shopping list exists and is linked - offer merge or overwrite options
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Existing linked shopping list found: {existingShoppingList.Id}");
                choice = await Shell.Current.DisplayActionSheet(
                    "Czy chcesz zaktualizowaæ powi¹zan¹ listê zakupów?",
                    "Anuluj",
                    null,
                    "Tylko zapisz planer",
                    "Zapisz i scal z list¹",
                    "Zapisz i nadpisz listê"
                );
            }
            else if (manualShoppingList != null)
            {
                // Manual shopping list exists for this date range
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Manual shopping list found: {manualShoppingList.Id}");
                choice = await Shell.Current.DisplayActionSheet(
                    "Znaleziono rêcznie utworzon¹ listê zakupów dla tego zakresu dat. Co chcesz zrobiæ?",
                    "Anuluj",
                    null,
                    "Tylko zapisz planer",
                    "Scal z rêcznie utworzon¹ list¹",
                    "Zapisz i utwórz now¹ listê"
                );
            }
            else
            {
                // No shopping list exists or it was archived - offer to create one
                System.Diagnostics.Debug.WriteLine("[PlannerEditVM] No linked shopping list found for this planner");
                choice = await Shell.Current.DisplayActionSheet(
                    "Czy chcesz stworzyæ listê zakupów?",
                    "Anuluj",
                    null,
                    "Tylko zapisz planer",
                    "Zapisz i stwórz listê"
                );
            }

            // Handle user choice
            if (string.IsNullOrEmpty(choice) || choice == "Anuluj" || choice == "Tylko zapisz planer")
            {
                System.Diagnostics.Debug.WriteLine("[PlannerEditVM] User chose to skip shopping list operation");
                return;
            }

            if (choice == "Zapisz i stwórz listê" || choice == "Zapisz i utwórz now¹ listê")
            {
                await CreateShoppingListPlanAsync();
            }
            else if (choice == "Zapisz i scal z list¹")
            {
                await MergeWithShoppingListAsync(existingShoppingList!);
            }
            else if (choice == "Zapisz i nadpisz listê")
            {
                await OverwriteShoppingListAsync(existingShoppingList!);
            }
            else if (choice == "Scal z rêcznie utworzon¹ list¹")
            {
                // Link manual list to this planner and update
                if (_currentPlan != null && manualShoppingList != null)
                {
                    _currentPlan.LinkedShoppingListPlanId = manualShoppingList.Id;
                    await _planService.UpdatePlanAsync(_currentPlan);
                    System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Linked manual shopping list {manualShoppingList.Id} to planner {_currentPlan.Id}");
                    await MergeWithShoppingListAsync(manualShoppingList);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Error handling shopping list: {ex.Message}");
            // Don't throw - shopping list is optional
        }
    }

    /// <summary>
    /// Create a new shopping list plan for the same date range and link it
    /// </summary>
    private async Task CreateShoppingListPlanAsync()
    {
        try
        {
            var newShoppingPlan = new Plan
            {
                StartDate = StartDate,
                EndDate = EndDate,
                Type = PlanType.ShoppingList,
                IsArchived = false
            };
            await _planService.AddPlanAsync(newShoppingPlan);
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Created new shopping list plan with ID: {newShoppingPlan.Id}");
            
            // Link the shopping list to this planner
            if (_currentPlan != null)
            {
                _currentPlan.LinkedShoppingListPlanId = newShoppingPlan.Id;
                await _planService.UpdatePlanAsync(_currentPlan);
                System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Linked shopping list {newShoppingPlan.Id} to planner {_currentPlan.Id}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Error creating shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("B³¹d", "Nie uda³o siê stworzyæ listy zakupów", "OK");
        }
    }

    /// <summary>
    /// Merge with existing shopping list (keep existing items, update dates)
    /// </summary>
    private async Task MergeWithShoppingListAsync(Plan shoppingPlan)
    {
        try
        {
            // Update dates to match planner
            bool needsUpdate = false;
            
            if (shoppingPlan.StartDate != StartDate || shoppingPlan.EndDate != EndDate)
            {
                shoppingPlan.StartDate = StartDate;
                shoppingPlan.EndDate = EndDate;
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
                System.Diagnostics.Debug.WriteLine("[PlannerEditVM] Merged with existing shopping list (updated dates)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PlannerEditVM] Shopping list already up to date");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Error merging shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("B³¹d", "Nie uda³o siê scaliæ listy zakupów", "OK");
        }
    }

    /// <summary>
    /// Overwrite existing shopping list (recreate from scratch)
    /// </summary>
    private async Task OverwriteShoppingListAsync(Plan shoppingPlan)
    {
        try
        {
            // Remove the old shopping list
            await _planService.RemovePlanAsync(shoppingPlan.Id);
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Removed old shopping list: {shoppingPlan.Id}");
            
            // Create a new one (which will also update the link)
            await CreateShoppingListPlanAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerEditVM] Error overwriting shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("B³¹d", "Nie uda³o siê nadpisaæ listy zakupów", "OK");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
