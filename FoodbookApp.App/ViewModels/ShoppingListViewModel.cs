using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class ShoppingListViewModel
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    public ObservableCollection<Plan> Plans { get; } = new();

    public ICommand OpenPlanCommand { get; }
    public ICommand DeletePlanCommand { get; }
    public ICommand CreateShoppingListCommand { get; }

    // Handler stored so we can unsubscribe later
    private readonly Func<Task> _planChangedHandler;
    private bool _isListening = false;

    public ShoppingListViewModel(IPlanService planService, IPlannerService plannerService)
    {
        _planService = planService;
        _plannerService = plannerService;
        OpenPlanCommand = new Command<Plan>(async p => await OpenPlanAsync(p));
        DeletePlanCommand = new Command<Plan>(async p => await DeletePlanAsync(p));
        CreateShoppingListCommand = new Command(async () => await CreateShoppingListAsync());

        _planChangedHandler = async () => await LoadPlansAsync();
    }

    // Called by the page when it appears to start listening for global changes
    public void StartListening()
    {
        if (_isListening) return;
        AppEvents.PlanChangedAsync += _planChangedHandler;
        _isListening = true;
    }

    // Called by the page when it disappears to avoid leaking handlers
    public void StopListening()
    {
        if (!_isListening) return;
        AppEvents.PlanChangedAsync -= _planChangedHandler;
        _isListening = false;
    }

    public async Task LoadPlansAsync()
    {
        Plans.Clear();
        var plans = await _planService.GetPlansAsync();
        // Show only non-archived shopping list plans
        foreach (var p in plans.Where(pl => !pl.IsArchived && pl.Type == PlanType.ShoppingList).OrderByDescending(pl => pl.StartDate))
            Plans.Add(p);
    }

    private async Task OpenPlanAsync(Plan? plan)
    {
        if (plan == null) return;
        await Shell.Current.GoToAsync($"{nameof(ShoppingListDetailPage)}?id={plan.Id}");
    }

    private async Task DeletePlanAsync(Plan? plan)
    {
        if (plan == null) return;
        
        bool confirm = await Shell.Current.DisplayAlert(
            "Usuwanie", 
            "Czy na pewno chcesz trwale usun¹æ tê listê zakupów?", 
            "Tak", 
            "Nie");
            
        if (confirm)
        {
            // Remove the plan entity; related items will be handled by DB constraints if configured
            await _planService.RemovePlanAsync(plan.Id);
            await LoadPlansAsync();
            
            // Notify archive and other pages
            AppEvents.RaisePlanChanged();
        }
    }

    private async Task CreateShoppingListAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListVM] Creating new empty shopping list");
            
            // Create a new empty shopping list (no dates, not tied to any planner)
            var newPlan = new Plan
            {
                Type = PlanType.ShoppingList,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today,
                IsArchived = false,
                LinkedShoppingListPlanId = null // Ensure not linked to any planner
            };
            
            // Add to database
            await _planService.AddPlanAsync(newPlan);
            System.Diagnostics.Debug.WriteLine($"[ShoppingListVM] Created shopping list with ID: {newPlan.Id}");
            
            // Navigate to detail page to add items
            await Shell.Current.GoToAsync($"{nameof(ShoppingListDetailPage)}?id={newPlan.Id}");
            
            // Reload the list
            await LoadPlansAsync();
            
            // Notify other views
            AppEvents.RaisePlanChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListVM] Error creating shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert("B³¹d", "Nie uda³o siê utworzyæ listy zakupów.", "OK");
        }
    }
}

