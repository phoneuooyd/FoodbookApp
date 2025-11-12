using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using System;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "planId")]
public partial class PlannerPage : ContentPage
{
    private readonly object _viewModel; // Can be PlannerViewModel or PlannerEditViewModel
    private readonly PageThemeHelper _themeHelper;
    private bool _isInitialized;
    private bool _hasEverLoaded;

    // Accept plan id for editing existing plan
    public int PlanId
    {
        get => _planId;
        set
        {
            _planId = value;
            _pendingPlanId = value > 0 ? value : null;
            System.Diagnostics.Debug.WriteLine($"[PlannerPage] PlanId property set to: {value}");
        }
    }
    private int _planId;
    private int? _pendingPlanId;

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
            
            int planIdToLoad = 0;
            
            // Try to get planId from QueryProperty first
            if (_pendingPlanId.HasValue && _pendingPlanId.Value > 0)
            {
                planIdToLoad = _pendingPlanId.Value;
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] Using _pendingPlanId: {planIdToLoad}");
                _pendingPlanId = null;
            }
            else if (_planId > 0)
            {
                planIdToLoad = _planId;
                System.Diagnostics.Debug.WriteLine($"[PlannerPage] Using _planId: {planIdToLoad}");
            }
            
            // Load the plan data
            if (planIdToLoad > 0 && !_hasEverLoaded)
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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
        
        System.Diagnostics.Debug.WriteLine($"[PlannerPage] Disappearing - ViewModel type: {_viewModel.GetType().Name}");
    }
}