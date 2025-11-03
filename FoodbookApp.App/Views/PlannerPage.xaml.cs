using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using System;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "planId")]
public partial class PlannerPage : ContentPage
{
    private readonly PlannerViewModel _viewModel;
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
        }
    }
    private int _planId;
    private int? _pendingPlanId;

    public PlannerPage(PlannerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        
        // Hook date validation once
        EnsureDatePickersHandlers();

        // If navigation provided a plan id, initialize edit mode before the first load
        if (_pendingPlanId.HasValue && !_hasEverLoaded)
        {
            try
            {
                await _viewModel.InitializeForEditAsync(_pendingPlanId.Value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlannerPage: InitializeForEditAsync failed: {ex.Message}");
            }
            finally
            {
                _pendingPlanId = null;
            }
        }
        
        if (!_hasEverLoaded)
        {
            // First time loading - always load fresh data
            System.Diagnostics.Debug.WriteLine("?? PlannerPage: First load - loading fresh data");
            await _viewModel.LoadAsync(forceReload: false);
            _hasEverLoaded = true;
            _isInitialized = true;
        }
        else
        {
            // Do not auto-refresh on subsequent appearances (e.g., after popup close)
            System.Diagnostics.Debug.WriteLine("?? PlannerPage: Skipping auto refresh on re-appear");
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
        }
        catch { }
    }

    private void OnStartDateSelected(object? sender, DateChangedEventArgs e)
    {
        try
        {
            // If new start is after end -> align end to start
            if (_viewModel.StartDate > _viewModel.EndDate)
            {
                _viewModel.EndDate = _viewModel.StartDate;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlannerPage.OnStartDateSelected error: {ex.Message}");
        }
    }

    private void OnEndDateSelected(object? sender, DateChangedEventArgs e)
    {
        try
        {
            // If new end is before start -> align start to end
            if (_viewModel.EndDate < _viewModel.StartDate)
            {
                _viewModel.StartDate = _viewModel.EndDate;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PlannerPage.OnEndDateSelected error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
        
        System.Diagnostics.Debug.WriteLine("?? PlannerPage: Disappearing - data remains cached");
    }
}