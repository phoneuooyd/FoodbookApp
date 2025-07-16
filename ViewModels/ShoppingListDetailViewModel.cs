using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel : INotifyPropertyChanged
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;

    // Dwie oddzielne kolekcje
    public ObservableCollection<Ingredient> UncheckedItems { get; } = new();
    public ObservableCollection<Ingredient> CheckedItems { get; } = new();
    
    // W³aœciwoœæ do sprawdzania czy s¹ zebrane produkty
    public bool HasCheckedItems => CheckedItems.Count > 0;
    
    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }

    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<Ingredient>(RemoveItem);
        
        // S³uchaj zmian w kolekcjach
        UncheckedItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCheckedItems));
        CheckedItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasCheckedItems));
    }

    public async Task LoadAsync(int planId)
    {
        var plan = await _planService.GetPlanAsync(planId);
        if (plan == null) return;

        var items = await _shoppingListService.GetShoppingListAsync(plan.StartDate, plan.EndDate);
        
        UncheckedItems.Clear();
        CheckedItems.Clear();
        
        foreach (var item in items)
        {
            // Dodaj obs³ugê zmiany stanu CheckBox
            item.PropertyChanged += OnItemPropertyChanged;
            
            if (item.IsChecked)
                CheckedItems.Add(item);
            else
                UncheckedItems.Add(item);
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Ingredient.IsChecked) && sender is Ingredient item)
        {
            if (item.IsChecked)
            {
                // Przenoszenie z UncheckedItems do CheckedItems
                if (UncheckedItems.Contains(item))
                {
                    UncheckedItems.Remove(item);
                    CheckedItems.Add(item);
                }
            }
            else
            {
                // Przenoszenie z CheckedItems do UncheckedItems
                if (CheckedItems.Contains(item))
                {
                    CheckedItems.Remove(item);
                    UncheckedItems.Add(item);
                }
            }
        }
    }

    private void AddItem()
    {
        var newItem = new Ingredient { Name = string.Empty, Quantity = 0, Unit = Unit.Gram };
        newItem.PropertyChanged += OnItemPropertyChanged;
        UncheckedItems.Add(newItem);
    }

    private void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        
        // Usuñ event handler
        item.PropertyChanged -= OnItemPropertyChanged;
        
        // Usuñ z odpowiedniej kolekcji
        UncheckedItems.Remove(item);
        CheckedItems.Remove(item);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
