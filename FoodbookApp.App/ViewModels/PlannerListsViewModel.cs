using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

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
        // Do not create a Plan immediately on FAB click. Navigate to PlannerPage so user can configure and Save there.
        CreatePlanCommand = new Command(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync($"{nameof(PlannerPage)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to PlannerPage failed: {ex.Message}");
            }
        });
        EditPlanCommand = new Command<Plan>(async (p) =>
        {
            if (p == null) return;
            await Shell.Current.GoToAsync($"{nameof(PlannerPage)}?planId={p.Id}");
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
