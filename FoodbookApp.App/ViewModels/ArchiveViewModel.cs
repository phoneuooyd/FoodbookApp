using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;
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
        
        // Pobierz tylko zarchiwizowane plany bezpo�rednio z us�ugi
        var plans = await _planService.GetArchivedPlansAsync();
        
        // Rozdziel zarchiwizowane elementy wed�ug typu planu
        foreach (var p in plans)
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
        
        // Sprawd� konflikty tylko z aktywnymi planami tego samego typu
        bool hasConflict = await _planService.HasOverlapAsync(plan.StartDate, plan.EndDate, plan.Id);
        
        if (hasConflict)
        {
            string itemType = plan.Type == PlanType.Planner
                ? A("PlannerItemLabel", "planner")
                : A("ShoppingListItemLabel", "shopping list");
            await Shell.Current.DisplayAlert(
                A("ConflictDateTitle", "Date conflict"),
                string.Format(
                    CultureInfo.CurrentUICulture,
                    A("RestoreConflictMessageFormat", "Cannot restore {0}. An active plan already exists in this date range."),
                    itemType),
                ButtonResources.OK);
            return;
        }
        
        string itemName = plan.Type == PlanType.Planner
            ? A("PlannerItemLabel", "planner")
            : A("ShoppingListItemLabel", "shopping list");
        bool confirm = await Shell.Current.DisplayAlert(
            A("RestoreConfirmTitle", "Restore"),
            string.Format(
                CultureInfo.CurrentUICulture,
                A("RestoreConfirmMessageFormat", "Are you sure you want to restore this {0}?"),
                itemName),
            ButtonResources.Yes,
            ButtonResources.No);
            
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
        
        string itemName = plan.Type == PlanType.Planner
            ? A("PlannerItemLabel", "planner")
            : A("ShoppingListItemLabel", "shopping list");
        bool confirm = await Shell.Current.DisplayAlert(
            A("DeleteConfirmTitle", "Delete"),
            string.Format(
                CultureInfo.CurrentUICulture,
                A("DeleteConfirmMessageFormat", "Are you sure you want to permanently delete this {0}? This operation cannot be undone."),
                itemName),
            ButtonResources.Yes,
            ButtonResources.No);
            
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

    private static string A(string key, string fallback)
    {
        var value = ArchivePageResources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}