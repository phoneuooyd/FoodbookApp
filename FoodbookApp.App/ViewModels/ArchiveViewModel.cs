using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ArchiveViewModel
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    public ObservableCollection<Plan> ArchivedPlanners { get; } = new();
    public ObservableCollection<Plan> ArchivedShoppingLists { get; } = new();

    public ICommand RestorePlanCommand { get; }
    public ICommand DeletePlanCommand { get; }

    public ArchiveViewModel(IPlanService planService, IPlannerService plannerService)
    {
        _planService = planService;
        _plannerService = plannerService;
        RestorePlanCommand = new Command<Plan>(async p => await RestorePlanAsync(p));
        DeletePlanCommand = new Command<Plan>(async p => await DeletePlanAsync(p));
        
        // Listener na zmiany planu
        AppEvents.PlanChangedAsync += async () => await LoadArchivedPlansAsync();
    }

    public async Task LoadArchivedPlansAsync()
    {
        ArchivedPlanners.Clear();
        ArchivedShoppingLists.Clear();
        
        var plans = await _planService.GetPlansAsync();
        
        // Rozdziel zarchiwizowane elementy wed³ug typu planu
        foreach (var p in plans.Where(pl => pl.IsArchived).OrderByDescending(pl => pl.StartDate))
        {
            if (p.Type == PlanType.Planner)
                ArchivedPlanners.Add(p);
            else if (p.Type == PlanType.ShoppingList)
                ArchivedShoppingLists.Add(p);
        }
    }

    private async Task RestorePlanAsync(Plan? plan)
    {
        if (plan == null) return;
        
        // SprawdŸ konflikty tylko z aktywnymi planami tego samego typu
        bool hasConflict = await _planService.HasOverlapAsync(plan.StartDate, plan.EndDate, plan.Id);
        
        if (hasConflict)
        {
            string itemType = plan.Type == PlanType.Planner ? "plannera" : "listy zakupów";
            await Shell.Current.DisplayAlert(
                "Konflikt dat", 
                $"Nie mo¿na przywróciæ {itemType} - ju¿ istnieje aktywny plan na ten okres dat.", 
                "OK");
            return;
        }
        
        string itemName = plan.Type == PlanType.Planner ? "planner" : "listê zakupów";
        bool confirm = await Shell.Current.DisplayAlert(
            "Przywracanie", 
            $"Czy na pewno chcesz przywróciæ ten {itemName}?", 
            "Tak", 
            "Nie");
            
        if (confirm)
        {
            plan.IsArchived = false;
            await _planService.UpdatePlanAsync(plan);
            await LoadArchivedPlansAsync();
            
            // Powiadom inne widoki
            AppEvents.RaisePlanChanged();
        }
    }

    private async Task DeletePlanAsync(Plan? plan)
    {
        if (plan == null) return;
        
        string itemName = plan.Type == PlanType.Planner ? "planner" : "listê zakupów";
        bool confirm = await Shell.Current.DisplayAlert(
            "Usuwanie", 
            $"Czy na pewno chcesz trwale usun¹æ ten {itemName}? Ta operacja jest nieodwracalna.", 
            "Tak", 
            "Nie");
            
        if (confirm)
        {
            var meals = await _plannerService.GetPlannedMealsAsync(plan.StartDate, plan.EndDate);
            foreach (var m in meals)
                await _plannerService.RemovePlannedMealAsync(m.Id);
            await _planService.RemovePlanAsync(plan.Id);
            await LoadArchivedPlansAsync();
            
            // Powiadom inne widoki
            AppEvents.RaisePlanChanged();
        }
    }
}