using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.ViewModels;

public class PlannerListsViewModel
{
    private readonly IPlanService _planService;

    public ObservableCollection<Plan> Plans { get; } = new();

    public ICommand LoadCommand { get; }
    public ICommand CreatePlanCommand { get; }
    public ICommand EditPlanCommand { get; }
    public ICommand OpenShoppingListCommand { get; }
    public ICommand ArchivePlanCommand { get; }

    public PlannerListsViewModel(IPlanService planService)
    {
        _planService = planService;

        LoadCommand = new Command(async () => await LoadPlansAsync());
        
        // Navigate to PlannerPage with NEW planner ViewModel
        CreatePlanCommand = new Command(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[PlannerListsVM] Creating NEW planner");
                
                // Resolve PlannerViewModel and PlannerPage from DI
                var newVM = FoodbookApp.MauiProgram.ServiceProvider?.GetService<PlannerViewModel>();
                if (newVM != null)
                {
                    var page = new PlannerPage(newVM);
                    await Shell.Current.Navigation.PushAsync(page);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[PlannerListsVM] Failed to resolve PlannerViewModel");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to NEW PlannerPage failed: {ex.Message}");
            }
        });
        
        // Navigate to PlannerPage with EDIT ViewModel
        EditPlanCommand = new Command<Plan>(async (p) =>
        {
            if (p == null) return;
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] EDITING existing plan {p.Id}");
                
                // Resolve PlannerEditViewModel from DI
                var editVM = FoodbookApp.MauiProgram.ServiceProvider?.GetService<PlannerEditViewModel>();
                if (editVM != null)
                {
                    var page = new PlannerPage(editVM);
                    
                    // Set the PlanId property for QueryProperty
                    page.PlanId = p.Id;
                    
                    await Shell.Current.Navigation.PushAsync(page);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[PlannerListsVM] Failed to resolve PlannerEditViewModel");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to EDIT PlannerPage failed: {ex.Message}");
            }
        });
        
        OpenShoppingListCommand = new Command<Plan>(async (p) =>
        {
            if (p == null) return;
            await Shell.Current.GoToAsync($"{nameof(ShoppingListDetailPage)}?id={p.Id}");
        });
        
        ArchivePlanCommand = new Command<Plan>(async (p) => await ArchivePlanAsync(p));

        // Refresh when plans change elsewhere
        AppEvents.PlanChangedAsync += async () => await LoadPlansAsync();
    }

    public async Task LoadPlansAsync()
    {
        Plans.Clear();
        var plans = await _planService.GetPlansAsync();
        // Filter only non-archived Planner type plans
        foreach (var p in plans.Where(x => !x.IsArchived && x.Type == PlanType.Planner).OrderByDescending(x => x.StartDate))
        {
            Plans.Add(p);
        }
    }

    private async Task ArchivePlanAsync(Plan? plan)
    {
        if (plan == null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Archiwizacja",
            "Czy na pewno chcesz zarchiwizowaæ ten planner?",
            "Tak",
            "Nie");

        if (confirm)
        {
            plan.IsArchived = true;
            await _planService.UpdatePlanAsync(plan);
            await LoadPlansAsync();

            // Notify other views including archive page
            AppEvents.RaisePlanChanged();
        }
    }
}
