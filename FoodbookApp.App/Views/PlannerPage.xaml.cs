using System.ComponentModel;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Models;
using Foodbook.Views.Base;

namespace Foodbook.Views
{
    [QueryProperty(nameof(PlanId), "planId")]
    public partial class PlannerPage : ContentPage, ITabLoadable
    {
        private readonly object _viewModel; // Can be PlannerViewModel or PlannerEditViewModel
        private readonly PageThemeHelper _themeHelper;
        private bool _isInitialized;
        private bool _hasEverLoaded;

        // Accept plan id for editing existing plan
        public string? PlanId
        {
            get => _planId == Guid.Empty ? null : _planId.ToString();
            set
            {
                if (Guid.TryParse(value, out var parsed))
                {
                    _planId = parsed;
                    _pendingPlanId = parsed != Guid.Empty ? parsed : null;
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] PlanId query parameter parsed: {parsed}");
                }
                else
                {
                    _planId = Guid.Empty;
                    _pendingPlanId = null;
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] Invalid planId query parameter: '{value}'");
                }
            }
        }
        private Guid _planId;
        private Guid? _pendingPlanId;

        // Constructor for NEW planner (PlannerViewModel)
        public PlannerPage(PlannerViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("[PlannerPage] Constructor called with PlannerViewModel (NEW planner mode)");
            
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            _themeHelper = new PageThemeHelper();
        }

        // Constructor for EDIT mode (PlannerEditViewModel)
        public PlannerPage(PlannerEditViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("[PlannerPage] Constructor called with PlannerEditViewModel (EDIT mode)");
            
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            _themeHelper = new PageThemeHelper();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnAppearing - ViewModel type: {_viewModel.GetType().Name}, PlanId={_planId}, _pendingPlanId={_pendingPlanId}");
            
            // Initialize theme and font handling
            _themeHelper.Initialize();
            
            // Hook date validation once
            EnsureDatePickersHandlers();

            // EDIT MODE: PlannerEditViewModel
            if (_viewModel is PlannerEditViewModel editVM)
            {
                System.Diagnostics.Debug.WriteLine("[PlannerPage] ?? EDIT MODE detected");
                
                Guid planIdToLoad = Guid.Empty;
                
                // Try to get planId from QueryProperty first
                if (_pendingPlanId.HasValue && _pendingPlanId.Value != Guid.Empty)
                {
                    planIdToLoad = _pendingPlanId.Value;
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] Using _pendingPlanId: {planIdToLoad}");
                    _pendingPlanId = null;
                }
                else if (_planId != Guid.Empty)
                {
                    planIdToLoad = _planId;
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] Using _planId: {planIdToLoad}");
                }
                
                // Load the plan data
                if (planIdToLoad != Guid.Empty && !_hasEverLoaded)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] Loading plan {planIdToLoad} for editing...");
                    try
                    {
                        await editVM.LoadPlanForEditAsync(planIdToLoad);
                        System.Diagnostics.Debug.WriteLine($"[PlannerPage] ? Plan {planIdToLoad} loaded successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlannerPage] ? Error loading plan: {ex.Message}");
                    }
                }
                else if (_hasEverLoaded)
                {
                    System.Diagnostics.Debug.WriteLine("[PlannerPage] Plan already loaded, skipping reload");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[PlannerPage] ?? WARNING: No valid planId provided for edit mode");
                }
                
                _hasEverLoaded = true;
                _isInitialized = true;
                return;
            }

            // NEW PLANNER MODE: PlannerViewModel
            if (_viewModel is PlannerViewModel newVM)
            {
                System.Diagnostics.Debug.WriteLine("[PlannerPage] ?? NEW PLANNER MODE detected");
                
                // If navigation provided a plan id in NEW mode, this shouldn't happen
                // but handle it gracefully by switching to edit
                if (_pendingPlanId.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] ?? WARNING: PlanId provided but using PlannerViewModel - this is unexpected");
                    _pendingPlanId = null;
                }
                
                // Already in edit mode (returning from popup), don't reload
                if (newVM.IsEditing)
                {
                    System.Diagnostics.Debug.WriteLine("[PlannerPage] Already in edit mode - skipping reload (returning from popup)");
                    return;
                }
                
                if (!_hasEverLoaded)
                {
                    // First time loading - load fresh data
                    System.Diagnostics.Debug.WriteLine("[PlannerPage] First load - loading fresh data (new planner)");
                    await newVM.LoadAsync(forceReload: false);
                    _hasEverLoaded = true;
                    _isInitialized = true;
                }
                else
                {
                    // Do not auto-refresh on subsequent appearances
                    System.Diagnostics.Debug.WriteLine("[PlannerPage] Skipping auto refresh on re-appear");
                }
            }

            // After loading, ensure any PlannedMeal objects have fresh Recipe instances from DB
            try
            {
                var recipeService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IRecipeService>();
                if (recipeService != null)
                {
                    await RefreshMealsRecipesAsync(recipeService);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] RefreshMealsRecipesAsync failed: {ex.Message}");
            }
        }

        public async Task OnTabActivatedAsync()
        {
            try
            {
                if (_viewModel is PlannerViewModel newVM)
                {
                    await newVM.LoadAsync(forceReload: false);
                }
                // PlannerEditViewModel is loaded via OnAppearing + LoadPlanForEditAsync(planId);
                // no parameterless reload method exists, so skip.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnTabActivatedAsync error: {ex.Message}");
            }
        }

        private async Task RefreshMealsRecipesAsync(IRecipeService recipeService)
        {
            try
            {
                // Walk through days and meals and fetch fresh Recipe for each assigned RecipeId
                foreach (var dayProp in ((PlannerViewModel)BindingContext).Days)
                {
                    foreach (var meal in dayProp.Meals)
                    {
                        if (meal.RecipeId != Guid.Empty)
                        {
                            try
                            {
                                var fresh = await recipeService.GetRecipeAsync(meal.RecipeId);
                                if (fresh != null)
                                {
                                    meal.Recipe = fresh;
                                    // Ensure Portions reflect recipe's stored portion count
                                    meal.Portions = Math.Max(fresh.IloscPorcji, 1);
                                }
                            }
                            catch (Exception mex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[PlannerPage] Could not refresh recipe {meal.RecipeId}: {mex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] RefreshMealsRecipesAsync error: {ex.Message}");
            }
        }

        private bool _dateHandlersAttached;
        private void EnsureDatePickersHandlers()
        {
            if (_dateHandlersAttached) return;
            try
            {
                var startPicker = this.FindByName<DatePicker>("StartDatePicker");
                var endPicker = this.FindByName<DatePicker>("EndDatePicker");
                if (startPicker != null)
                {
                    startPicker.DateSelected += OnStartDateSelected;
                }
                if (endPicker != null)
                {
                    endPicker.DateSelected += OnEndDateSelected;
                }
                _dateHandlersAttached = true;
                System.Diagnostics.Debug.WriteLine("[PlannerPage] Date picker handlers attached");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] Failed to attach date handlers: {ex.Message}");
            }
        }

        private void OnStartDateSelected(object? sender, DateChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] StartDate changed: {e.NewDate:yyyy-MM-dd}");
                
                // Handle both ViewModel types
                if (_viewModel is PlannerEditViewModel editVM)
                {
                    if (editVM.StartDate > editVM.EndDate)
                    {
                        editVM.EndDate = editVM.StartDate;
                    }
                }
                else if (_viewModel is PlannerViewModel newVM)
                {
                    if (newVM.StartDate > newVM.EndDate)
                    {
                        newVM.EndDate = newVM.StartDate;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnStartDateSelected error: {ex.Message}");
            }
        }

        private void OnEndDateSelected(object? sender, DateChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] EndDate changed: {e.NewDate:yyyy-MM-dd}");
                
                // Handle both ViewModel types
                if (_viewModel is PlannerEditViewModel editVM)
                {
                    if (editVM.EndDate < editVM.StartDate)
                    {
                        editVM.StartDate = editVM.EndDate;
                    }
                }
                else if (_viewModel is PlannerViewModel newVM)
                {
                    if (newVM.EndDate < newVM.StartDate)
                    {
                        newVM.StartDate = newVM.EndDate;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnEndDateSelected error: {ex.Message}");
            }
        }

        private void OnMealRecipeChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlannedMeal.Recipe) && sender is PlannedMeal meal && meal.Recipe != null)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] Recipe changed: {meal.Recipe.Name} - setting portions to recipe.IloscPorcji and not modifying totals");
                // Always show portions per recipe default
                meal.Portions = Math.Max(meal.Recipe.IloscPorcji, 1);
            }
        }

        // Event handlers for meal portion buttons
        private void OnDecreasePortionsClicked(object? sender, EventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.BindingContext is PlannedMeal meal)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnDecreasePortionsClicked: Current portions = {meal.Portions}");
                    
                    if (meal.Portions > 1)
                    {
                        meal.Portions--;
                        System.Diagnostics.Debug.WriteLine($"[PlannerPage] Portions decreased to {meal.Portions}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnDecreasePortionsClicked error: {ex.Message}");
            }
        }

        private void OnIncreasePortionsClicked(object? sender, EventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.BindingContext is PlannedMeal meal)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnIncreasePortionsClicked: Current portions = {meal.Portions}");
                    
                    if (meal.Portions < 20)
                    {
                        meal.Portions++;
                        System.Diagnostics.Debug.WriteLine($"[PlannerPage] Portions increased to {meal.Portions}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnIncreasePortionsClicked error: {ex.Message}");
            }
        }

        private void OnRemoveMealClicked(object? sender, EventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.BindingContext is PlannedMeal meal)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnRemoveMealClicked: Removing meal from {meal.Date:yyyy-MM-dd}");
                    
                    // Get the ViewModel and call RemoveMealCommand
                    if (_viewModel is PlannerViewModel newVM)
                    {
                        newVM.RemoveMealCommand.Execute(meal);
                        System.Diagnostics.Debug.WriteLine($"[PlannerPage] Meal removed successfully");
                    }
                    else if (_viewModel is PlannerEditViewModel editVM)
                    {
                        editVM.RemoveMealCommand.Execute(meal);
                        System.Diagnostics.Debug.WriteLine($"[PlannerPage] Meal removed successfully (edit mode)");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnRemoveMealClicked error: {ex.Message}");
            }
        }

        private void OnDecreaseMealsPerDayClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_viewModel is PlannerViewModel newVm)
                {
                    if (newVm.MealsPerDay > 1)
                        newVm.MealsPerDay--;
                }
                else if (_viewModel is PlannerEditViewModel editVm)
                {
                    if (editVm.MealsPerDay > 1)
                        editVm.MealsPerDay--;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnDecreaseMealsPerDayClicked error: {ex.Message}");
            }
        }

        private void OnIncreaseMealsPerDayClicked(object? sender, EventArgs e)
        {
            try
            {
                if (_viewModel is PlannerViewModel newVm)
                {
                    if (newVm.MealsPerDay < 20)
                        newVm.MealsPerDay++;
                }
                else if (_viewModel is PlannerEditViewModel editVm)
                {
                    if (editVm.MealsPerDay < 20)
                        editVm.MealsPerDay++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] OnIncreaseMealsPerDayClicked error: {ex.Message}");
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
            
            System.Diagnostics.Debug.WriteLine($"[PlannerPage] Disappearing - ViewModel type: {_viewModel.GetType().Name}");
        }
    }
}