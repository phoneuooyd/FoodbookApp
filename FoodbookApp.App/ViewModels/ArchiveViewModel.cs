using System.Collections.ObjectModel;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ArchiveViewModel
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;
    private readonly IFeatureAccessService _featureAccessService;

    public ObservableCollection<Plan> ArchivedPlanners { get; } = new();
    public ObservableCollection<Plan> ArchivedShoppingLists { get; } = new();

    public ICommand RestorePlanCommand { get; }
    public ICommand DeletePlanCommand { get; }
    public ICommand UseArchivedPlannerCommand { get; }

    public ArchiveViewModel(
        IPlanService planService,
        IPlannerService plannerService,
        IFeatureAccessService featureAccessService)
    {
        _planService = planService;
        _plannerService = plannerService;
        _featureAccessService = featureAccessService;

        RestorePlanCommand = new Command<Plan>(async p => await RestorePlanAsync(p));
        DeletePlanCommand = new Command<Plan>(async p => await DeletePlanAsync(p));
        UseArchivedPlannerCommand = new Command<Plan>(async p => await UseArchivedPlannerAsync(p));

        AppEvents.PlanChangedAsync += async () => await LoadArchivedPlansAsync();
    }

    public async Task LoadArchivedPlansAsync()
    {
        ArchivedPlanners.Clear();
        ArchivedShoppingLists.Clear();

        var plans = await _planService.GetArchivedPlansAsync();
        foreach (var plan in plans)
        {
            if (plan.Type == PlanType.Planner)
            {
                ArchivedPlanners.Add(plan);
            }
            else if (plan.Type == PlanType.ShoppingList)
            {
                ArchivedShoppingLists.Add(plan);
            }
        }
    }

    private async Task UseArchivedPlannerAsync(Plan? plan)
    {
        if (plan == null || plan.Type != PlanType.Planner)
        {
            return;
        }

        var hasAccess = await _featureAccessService.CanUsePremiumFeatureAsync(PremiumFeature.PlanRecycling);
        if (!hasAccess)
        {
            var choice = await Shell.Current.DisplayActionSheet(
                "Funkcja premium",
                "Anuluj",
                null,
                "Odblokuj reklamą",
                "Przejdź do Premium");

            if (choice == "Odblokuj reklamą")
            {
                var unlockResult = await _featureAccessService.RequestAdUnlockAsync(PremiumFeature.PlanRecycling);
                if (!unlockResult.Success)
                {
                    await Shell.Current.DisplayAlert("Brak dostępu", "Nie udało się odblokować funkcji recyklingu planu.", "OK");
                    return;
                }

                hasAccess = await _featureAccessService.CanUsePremiumFeatureAsync(PremiumFeature.PlanRecycling);
            }
            else if (choice == "Przejdź do Premium")
            {
                await Shell.Current.DisplayAlert("Premium", "Włącz Premium, aby używać recyklingu plannerów bez ograniczeń.", "OK");
                return;
            }
            else
            {
                return;
            }
        }

        if (!hasAccess)
        {
            await Shell.Current.DisplayAlert("Brak dostępu", "Recykling plannerów jest dostępny w Premium lub po odblokowaniu reklamą.", "OK");
            return;
        }

        var plannerViewModel = FoodbookApp.MauiProgram.ServiceProvider?.GetService<PlannerViewModel>();
        if (plannerViewModel == null)
        {
            await Shell.Current.DisplayAlert("Błąd", "Nie udało się otworzyć planera.", "OK");
            return;
        }

        var page = new PlannerPage(plannerViewModel)
        {
            RecycleMode = "true",
            SourceArchivePlanId = plan.Id.ToString()
        };

        await Shell.Current.Navigation.PushAsync(page);
    }

    private async Task RestorePlanAsync(Plan? plan)
    {
        if (plan == null)
        {
            return;
        }

        var hasConflict = await _planService.HasOverlapAsync(plan.StartDate, plan.EndDate, plan.Id);
        if (hasConflict)
        {
            var itemType = plan.Type == PlanType.Planner ? "plannera" : "listy zakupów";
            await Shell.Current.DisplayAlert(
                "Konflikt dat",
                $"Nie można przywrócić {itemType} - już istnieje aktywny plan na ten okres dat.",
                "OK");
            return;
        }

        var itemName = plan.Type == PlanType.Planner ? "planner" : "listę zakupów";
        var confirm = await Shell.Current.DisplayAlert(
            "Przywracanie",
            $"Czy na pewno chcesz przywrócić ten {itemName}?",
            "Tak",
            "Nie");

        if (!confirm)
        {
            return;
        }

        plan.IsArchived = false;
        await _planService.UpdatePlanAsync(plan);
        await LoadArchivedPlansAsync();
        AppEvents.RaisePlanChanged();
    }

    private async Task DeletePlanAsync(Plan? plan)
    {
        if (plan == null)
        {
            return;
        }

        var itemName = plan.Type == PlanType.Planner ? "planner" : "listę zakupów";
        var confirm = await Shell.Current.DisplayAlert(
            "Usuwanie",
            $"Czy na pewno chcesz trwale usunąć ten {itemName}? Ta operacja jest nieodwracalna.",
            "Tak",
            "Nie");

        if (!confirm)
        {
            return;
        }

        var meals = await _plannerService.GetPlannedMealsAsync(plan.StartDate, plan.EndDate);
        foreach (var meal in meals)
        {
            await _plannerService.RemovePlannedMealAsync(meal.Id);
        }

        await _planService.RemovePlanAsync(plan.Id);
        await LoadArchivedPlansAsync();
        AppEvents.RaisePlanChanged();
    }
}
