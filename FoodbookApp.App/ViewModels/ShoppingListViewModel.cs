using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Views;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ShoppingListViewModel
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    public ObservableCollection<Plan> Plans { get; } = new();

    public ICommand OpenPlanCommand { get; }
    public ICommand ArchivePlanCommand { get; }

    public ShoppingListViewModel(IPlanService planService, IPlannerService plannerService)
    {
        _planService = planService;
        _plannerService = plannerService;
        OpenPlanCommand = new Command<Plan>(async p => await OpenPlanAsync(p));
        ArchivePlanCommand = new Command<Plan>(async p => await ArchivePlanAsync(p));
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

    private async Task ArchivePlanAsync(Plan? plan)
    {
        if (plan == null) return;
        
        bool confirm = await Shell.Current.DisplayAlert(
            "Archiwizacja", 
            "Czy na pewno chcesz zarchiwizowaæ tê listê zakupów?", 
            "Tak", 
            "Nie");
            
        if (confirm)
        {
            plan.IsArchived = true;
            await _planService.UpdatePlanAsync(plan);
            await LoadPlansAsync();
            
            // Notify archive page
            AppEvents.RaisePlanChanged();
        }
    }
}

