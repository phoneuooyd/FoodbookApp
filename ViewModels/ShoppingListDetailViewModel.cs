using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel : INotifyPropertyChanged
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;

    public ObservableCollection<ShoppingListItem> Items { get; } = new();
    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    private Plan? _currentPlan;
    public Plan? CurrentPlan
    {
        get => _currentPlan;
        set
        {
            _currentPlan = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageTitle));
        }
    }

    public string PageTitle => CurrentPlan != null 
        ? $"Lista zakupów ({CurrentPlan.StartDate:dd.MM.yyyy} - {CurrentPlan.EndDate:dd.MM.yyyy})" 
        : "Lista zakupów";

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand ToggleItemCommand { get; }
    public ICommand GenerateListCommand { get; }

    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<ShoppingListItem>(RemoveItem);
        ToggleItemCommand = new Command<ShoppingListItem>(ToggleItem);
        GenerateListCommand = new Command(async () => await GenerateListAsync());
    }

    public async Task LoadAsync(int planId)
    {
        IsLoading = true;
        try
        {
            // Pobierz plan
            CurrentPlan = await _planService.GetPlanAsync(planId);
            if (CurrentPlan == null) return;

            // SprawdŸ czy lista ju¿ istnieje
            var existingItems = await _shoppingListService.GetShoppingListItemsAsync(planId);
            
            if (existingItems.Count == 0)
            {
                // Generuj now¹ listê
                existingItems = await _shoppingListService.GenerateShoppingListAsync(planId);
            }

            // Za³aduj pozycje
            Items.Clear();
            foreach (var item in existingItems.OrderBy(i => i.Name))
                Items.Add(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert(
                "B³¹d",
                "Nie uda³o siê za³adowaæ listy zakupów.",
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task GenerateListAsync()
    {
        if (CurrentPlan == null) return;

        try
        {
            IsLoading = true;
            
            // Regeneruj listê
            var newItems = await _shoppingListService.GenerateShoppingListAsync(CurrentPlan.Id);
            
            Items.Clear();
            foreach (var item in newItems.OrderBy(i => i.Name))
                Items.Add(item);

            await Shell.Current.DisplayAlert(
                "Sukces",
                "Lista zakupów zosta³a wygenerowana ponownie.",
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error generating shopping list: {ex.Message}");
            await Shell.Current.DisplayAlert(
                "B³¹d",
                "Nie uda³o siê wygenerowaæ listy zakupów.",
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddItem()
    {
        if (CurrentPlan == null) return;
        
        Items.Add(new ShoppingListItem 
        { 
            Name = "Nowy sk³adnik", 
            Quantity = 1, 
            Unit = Unit.Piece,
            PlanId = CurrentPlan.Id
        });
    }

    private async void RemoveItem(ShoppingListItem? item)
    {
        if (item == null) return;

        try
        {
            if (item.Id > 0)
            {
                await _shoppingListService.DeleteShoppingListItemAsync(item.Id);
            }
            Items.Remove(item);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing item: {ex.Message}");
            await Shell.Current.DisplayAlert(
                "B³¹d",
                "Nie uda³o siê usun¹æ pozycji.",
                "OK");
        }
    }

    private async void ToggleItem(ShoppingListItem? item)
    {
        if (item == null) return;

        try
        {
            item.IsChecked = !item.IsChecked;
            
            if (item.Id > 0)
            {
                await _shoppingListService.UpdateShoppingListItemAsync(item);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating item: {ex.Message}");
            // Przywróæ poprzedni stan
            item.IsChecked = !item.IsChecked;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
