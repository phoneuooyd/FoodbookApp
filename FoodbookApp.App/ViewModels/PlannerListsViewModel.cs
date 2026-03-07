using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class PlannerListsViewModel : INotifyPropertyChanged
{
    private readonly IPlanService _planService;
    private readonly IFoodbookTemplateService _templateService;
    private readonly IAccountService _accountService;

    public ObservableCollection<Plan> Plans { get; } = new();
    public ObservableCollection<FoodbookTemplate> Templates { get; } = new();

    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (_selectedTabIndex == value) return;
            _selectedTabIndex = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadCommand { get; }
    public ICommand CreatePlanCommand { get; }
    public ICommand EditPlanCommand { get; }
    public ICommand OpenShoppingListCommand { get; }
    public ICommand ArchivePlanCommand { get; }

    public ICommand OpenTemplatesPageCommand { get; }
    public ICommand ApplyTemplateCommand { get; }
    public ICommand EditTemplateCommand { get; }
    public ICommand DeleteTemplateCommand { get; }

    public PlannerListsViewModel(IPlanService planService, IFoodbookTemplateService templateService, IAccountService accountService)
    {
        _planService = planService;
        _templateService = templateService;
        _accountService = accountService;

        LoadCommand = new Command(async () => await LoadAsync());

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

        EditPlanCommand = new Command<Plan>(async p =>
        {
            if (p == null) return;

            try
            {
                var editVM = FoodbookApp.MauiProgram.ServiceProvider?.GetService<PlannerEditViewModel>();
                if (editVM != null)
                {
                    var page = new PlannerPage(editVM)
                    {
                        PlanId = p.Id.ToString()
                    };

                    await Shell.Current.Navigation.PushAsync(page);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to EDIT PlannerPage failed: {ex.Message}");
            }
        });

        OpenShoppingListCommand = new Command<Plan>(async p =>
        {
            if (p == null) return;
            await Shell.Current.GoToAsync($"{nameof(ShoppingListDetailPage)}?id={p.Id}");
        });

        ArchivePlanCommand = new Command<Plan>(async p => await ArchivePlanAsync(p));

        OpenTemplatesPageCommand = new Command(async () => await Shell.Current.Navigation.PushAsync(new FoodbookTemplatesPage(FoodbookApp.MauiProgram.ServiceProvider!.GetRequiredService<FoodbookTemplatesViewModel>())));
        ApplyTemplateCommand = new Command<FoodbookTemplate>(async t => await ApplyTemplateAsync(t));
        EditTemplateCommand = new Command<FoodbookTemplate>(async t => await EditTemplateAsync(t));
        DeleteTemplateCommand = new Command<FoodbookTemplate>(async t => await DeleteTemplateAsync(t));

        AppEvents.PlanChangedAsync += async () => await LoadPlansAsync();
    }

    public async Task LoadAsync()
    {
        await LoadPlansAsync();
        await LoadTemplatesAsync();
    }

    public async Task LoadPlansAsync()
    {
        Plans.Clear();
        var plans = await _planService.GetPlansAsync();
        foreach (var p in plans.Where(x => !x.IsArchived && x.Type == PlanType.Planner).OrderByDescending(x => x.StartDate))
        {
            Plans.Add(p);
        }
    }

    public async Task LoadTemplatesAsync()
    {
        Templates.Clear();
        var account = await _accountService.GetActiveAccountAsync();
        var userId = account?.SupabaseUserId ?? "local-user";
        var templates = await _templateService.GetTemplatesAsync(userId);

        foreach (var template in templates)
        {
            Templates.Add(template);
        }
    }

    private async Task ApplyTemplateAsync(FoodbookTemplate? template)
    {
        if (template == null) return;

        var dateText = await Shell.Current.DisplayPromptAsync("Data startowa", "Podaj datę startową (rrrr-MM-dd)", "Zastosuj", "Anuluj", placeholder: DateTime.Today.ToString("yyyy-MM-dd"));
        if (string.IsNullOrWhiteSpace(dateText) || !DateTime.TryParse(dateText, out var startDate))
        {
            return;
        }

        var plan = await _templateService.ApplyTemplateAsync(template.Id, startDate.Date);
        if (plan != null)
        {
            AppEvents.RaisePlanChanged();
            await LoadPlansAsync();
        }
    }

    private async Task EditTemplateAsync(FoodbookTemplate? template)
    {
        if (template == null) return;
        await Shell.Current.Navigation.PushAsync(new FoodbookTemplateFormPage(template.Id));
    }

    private async Task DeleteTemplateAsync(FoodbookTemplate? template)
    {
        if (template == null) return;

        var confirm = await Shell.Current.DisplayAlert("Usuń szablon", $"Czy usunąć szablon '{template.Name}'?", "Usuń", "Anuluj");
        if (!confirm) return;

        await _templateService.DeleteTemplateAsync(template.Id);
        Templates.Remove(template);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
