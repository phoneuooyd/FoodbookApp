using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.ViewModels;

public class PlannerListsViewModel : INotifyPropertyChanged
{
    private readonly IPlanService _planService;
    private int _selectedTabIndex;
    private bool _isLoading;

    public ObservableCollection<Plan> Plans { get; } = new();
    public ObservableCollection<Plan> Foodbooks { get; } = new();

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

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand LoadCommand { get; }
    public ICommand CreatePlanCommand { get; }
    public ICommand EditPlanCommand { get; }
    public ICommand OpenShoppingListCommand { get; }
    public ICommand ArchivePlanCommand { get; }
    public ICommand CreateFoodbookCommand { get; }
    public ICommand EditFoodbookCommand { get; }
    public ICommand ArchiveFoodbookCommand { get; }
    public ICommand ApplyFoodbookCommand { get; }

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
                    var page = new PlannerPage(editVM)
                    {
                        // Set the PlanId property for QueryProperty
                        PlanId = p.Id.ToString()
                    };

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
        CreateFoodbookCommand = new Command(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(FoodbookPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to NEW FoodbookPage failed: {ex.Message}");
            }
        });
        EditFoodbookCommand = new Command<Plan>(async (p) =>
        {
            if (p == null) return;
            try
            {
                await Shell.Current.GoToAsync($"{nameof(FoodbookPage)}?PlanId={p.Id}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlannerListsVM] Navigation to EDIT FoodbookPage failed: {ex.Message}");
            }
        });
        ArchiveFoodbookCommand = new Command<Plan>(async (p) => await ArchiveFoodbookAsync(p));
        ApplyFoodbookCommand = new Command<Plan>(async (p) => await ApplyFoodbookAsync(p));

        // Refresh when plans change elsewhere
        AppEvents.PlanChangedAsync += async () => await LoadPlansAsync();
    }

    public async Task LoadPlansAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            Plans.Clear();
            Foodbooks.Clear();

            var plans = await _planService.GetPlansAsync();
            // Filter only non-archived Planner type plans
            foreach (var p in plans.Where(x => !x.IsArchived && x.Type == PlanType.Planner).OrderByDescending(x => x.StartDate))
            {
                Plans.Add(p);
            }

            var foodbooks = await _planService.GetFoodbooksAsync();
            foreach (var foodbook in foodbooks)
            {
                Foodbooks.Add(foodbook);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ArchiveFoodbookAsync(Plan? foodbook)
    {
        if (foodbook == null) return;

        var confirm = await Shell.Current.DisplayAlert(
            "Archiwizacja",
            "Czy na pewno chcesz zarchiwizowaæ ten Foodbook?",
            "Tak",
            "Nie");

        if (!confirm) return;

        foodbook.IsArchived = true;
        await _planService.UpdatePlanAsync(foodbook);
        await LoadPlansAsync();
        AppEvents.RaisePlanChanged();
    }

    private async Task ApplyFoodbookAsync(Plan? foodbook)
    {
        if (foodbook == null) return;

        var startDateInput = await Shell.Current.DisplayPromptAsync(
            "Zastosuj Foodbook",
            "Podaj datê startu (rrrr-MM-dd)",
            accept: "Zastosuj",
            cancel: "Anuluj",
            initialValue: DateTime.Today.ToString("yyyy-MM-dd"));

        if (startDateInput == null)
            return;

        if (!DateTime.TryParse(startDateInput, out var startDate))
        {
            await Shell.Current.DisplayAlert("B³¹d", "Nieprawid³owy format daty.", "OK");
            return;
        }

        var endDate = startDate.Date.AddDays(Math.Max(1, foodbook.DurationDays) - 1);
        var hasOverlap = await _planService.HasOverlapAsync(startDate.Date, endDate);
        if (hasOverlap)
        {
            var proceed = await Shell.Current.DisplayAlert(
                "Nak³adanie planów",
                "W wybranym terminie istnieje ju¿ planer. Kontynuowaæ mimo to?",
                "Tak",
                "Nie");

            if (!proceed) return;
        }

        await _planService.ApplyFoodbookAsync(foodbook.Id, startDate.Date);
        await LoadPlansAsync();
        AppEvents.RaisePlanChanged();

        await Shell.Current.DisplayAlert("Gotowe", "Foodbook zosta³ zastosowany jako nowy planer.", "OK");
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
            AppEvents.RaisePlanChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
