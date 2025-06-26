using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ShoppingListViewModel
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    public ObservableCollection<Plan> Plans { get; } = new();

    public ICommand OpenPlanCommand { get; }
    public ICommand DeletePlanCommand { get; }

    public ShoppingListViewModel(IPlanService planService, IPlannerService plannerService)
    {
        _planService = planService;
        _plannerService = plannerService;
        OpenPlanCommand = new Command<Plan>(async p => await OpenPlanAsync(p));
        DeletePlanCommand = new Command<Plan>(async p => await DeletePlanAsync(p));
    }

    public async Task LoadPlansAsync()
    {
        Plans.Clear();
        var plans = await _planService.GetPlansAsync();
        foreach (var p in plans.OrderByDescending(pl => pl.StartDate))
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
        var meals = await _plannerService.GetPlannedMealsAsync(plan.StartDate, plan.EndDate);
        foreach (var m in meals)
            await _plannerService.RemovePlannedMealAsync(m.Id);
        await _planService.RemovePlanAsync(plan.Id);
        await LoadPlansAsync();
    }
}

