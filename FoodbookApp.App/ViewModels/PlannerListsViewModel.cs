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
    private readonly IFoodbookTemplateService _templateService;

    public ObservableCollection<Plan> Plans { get; } = new();
    public ObservableCollection<FoodbookTemplate> Templates { get; } = new();

    public int SelectedTabIndex { get; set; }

    public bool IsLoading { get; set; }

    public ICommand SelectTabCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand CreatePlanCommand { get; }
    public ICommand EditPlanCommand { get; }
    public ICommand OpenShoppingListCommand { get; }
    public ICommand ArchivePlanCommand { get; }
    public ICommand OpenTemplatesCommand { get; }

    public PlannerListsViewModel(IPlanService planService, IFoodbookTemplateService templateService)
    {
        _planService = planService;
        _templateService = templateService;

        LoadCommand = new Command(async () => await LoadPlansAsync());
        SelectTabCommand = new Command<int>(i => SelectedTabIndex = i);

        CreatePlanCommand = new Command(async () =>
        {
            try
            {
                var newVM = FoodbookApp.MauiProgram.ServiceProvider?.GetService<PlannerViewModel>();
                if (newVM != null)
                {
                    var page = new PlannerPage(newVM);
                    await Shell.Current.Navigation.PushAsync(page);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to NEW PlannerPage failed: {ex.Message}");
            }
        });

        EditPlanCommand = new Command<Plan>(async (p) =>
        {
            if (p == null) return;

            try
            {
                var editVM = FoodbookApp.MauiProgram.ServiceProvider?.GetService<PlannerEditViewModel>();
                if (editVM != null)
                {
                    var page = new PlannerPage(editVM);
                    page.PlanId = p.Id.ToString();
                    await Shell.Current.Navigation.PushAsync(page);
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
        OpenTemplatesCommand = new Command(async () => await Shell.Current.Navigation.PushAsync(new FoodbookTemplatesPage()));

        AppEvents.PlanChangedAsync += async () => await LoadPlansAsync();
    }

    public async Task LoadPlansAsync()
    {
        IsLoading = true;
        Plans.Clear();
        var plans = await _planService.GetPlansAsync();
        foreach (var p in plans.Where(x => !x.IsArchived && x.Type == PlanType.Planner).OrderByDescending(x => x.StartDate))
        {
            Plans.Add(p);
        }

        Templates.Clear();
        var templates = await _templateService.GetTemplatesAsync();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }

        IsLoading = false;
    }

    private async Task ArchivePlanAsync(Plan? plan)
    {
        if (plan == null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Archiwizacja",
            "Czy na pewno chcesz zarchiwizować ten planner?",
            "Tak",
            "Nie");

        if (confirm)
        {
            plan.IsArchived = true;
            await _planService.UpdatePlanAsync(plan);
            await LoadPlansAsync();
            AppEvents.RaisePlanChanged();
        }
    }
}
