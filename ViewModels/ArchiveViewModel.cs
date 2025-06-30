using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ArchiveViewModel
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    public ObservableCollection<Plan> ArchivedPlans { get; } = new();

    public ICommand RestorePlanCommand { get; }
    public ICommand DeletePlanCommand { get; }

    public ArchiveViewModel(IPlanService planService, IPlannerService plannerService)
    {
        _planService = planService;
        _plannerService = plannerService;
        RestorePlanCommand = new Command<Plan>(async p => await RestorePlanAsync(p));
        DeletePlanCommand = new Command<Plan>(async p => await DeletePlanAsync(p));
    }

    public async Task LoadArchivedPlansAsync()
    {
        ArchivedPlans.Clear();
        var plans = await _planService.GetPlansAsync();
        // Pokazuj tylko zarchiwizowane plany
        foreach (var p in plans.Where(pl => pl.IsArchived).OrderByDescending(pl => pl.StartDate))
            ArchivedPlans.Add(p);
    }

    private async Task RestorePlanAsync(Plan? plan)
    {
        if (plan == null) return;
        
        bool confirm = await Shell.Current.DisplayAlert(
            "Przywracanie", 
            "Czy na pewno chcesz przywróciæ tê listê zakupów?", 
            "Tak", 
            "Nie");
            
        if (confirm)
        {
            plan.IsArchived = false;
            await _planService.UpdatePlanAsync(plan);
            await LoadArchivedPlansAsync();
        }
    }

    private async Task DeletePlanAsync(Plan? plan)
    {
        if (plan == null) return;
        
        bool confirm = await Shell.Current.DisplayAlert(
            "Usuwanie", 
            "Czy na pewno chcesz trwale usun¹æ tê listê zakupów? Ta operacja jest nieodwracalna.", 
            "Tak", 
            "Nie");
            
        if (confirm)
        {
            var meals = await _plannerService.GetPlannedMealsAsync(plan.StartDate, plan.EndDate);
            foreach (var m in meals)
                await _plannerService.RemovePlannedMealAsync(m.Id);
            await _planService.RemovePlanAsync(plan.Id);
            await LoadArchivedPlansAsync();
        }
    }
}