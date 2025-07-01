using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ShoppingListViewModel : INotifyPropertyChanged
{
    private readonly IPlanService _planService;
    private readonly IPlannerService _plannerService;

    public ObservableCollection<Plan> Plans { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPlans));
        }
    }

    /// <summary>
    /// Czy istniej¹ jakiekolwiek plany do wyœwietlenia
    /// </summary>
    public bool HasPlans => Plans.Count > 0;

    /// <summary>
    /// Czy nie ma ¿adnych planów (dla wyœwietlenia pustego stanu)
    /// </summary>
    public bool HasNoPlans => !HasPlans && !IsLoading;

    public ICommand OpenPlanCommand { get; }
    public ICommand ArchivePlanCommand { get; }

    public ShoppingListViewModel(IPlanService planService, IPlannerService plannerService)
    {
        _planService = planService;
        _plannerService = plannerService;
        OpenPlanCommand = new Command<Plan>(async p => await OpenPlanAsync(p));
        ArchivePlanCommand = new Command<Plan>(async p => await ArchivePlanAsync(p));
        
        // Subskrybuj zmiany w kolekcji Plans
        Plans.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasPlans));
            OnPropertyChanged(nameof(HasNoPlans));
        };
    }

    public async Task LoadPlansAsync()
    {
        IsLoading = true;
        try
        {
            Plans.Clear();
            var plans = await _planService.GetPlansAsync();
            
            // Pokazuj tylko niearchiwizowane plany
            foreach (var p in plans.Where(pl => !pl.IsArchived).OrderByDescending(pl => pl.StartDate))
                Plans.Add(p);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading plans: {ex.Message}");
            // Mo¿esz dodaæ wyœwietlanie b³êdu u¿ytkownikowi
        }
        finally
        {
            IsLoading = false;
        }
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
            try
            {
                plan.IsArchived = true;
                await _planService.UpdatePlanAsync(plan);
                await LoadPlansAsync();
                
                await Shell.Current.DisplayAlert(
                    "Sukces",
                    "Lista zakupów zosta³a zarchiwizowana.",
                    "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error archiving plan: {ex.Message}");
                await Shell.Current.DisplayAlert(
                    "B³¹d",
                    "Nie uda³o siê zarchiwizowaæ listy zakupów.",
                    "OK");
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

