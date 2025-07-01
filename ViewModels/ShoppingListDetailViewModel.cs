
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;
    public ObservableCollection<Ingredient> Items { get; } = new();
    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }

    public IEnumerable<Unit> Units => Enum.GetValues(typeof(Unit)).Cast<Unit>();

    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }


    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;

        AddItemCommand = new Command(AddItem);
        RemoveItemCommand = new Command<Ingredient>(RemoveItem);

    }

    public async Task LoadAsync(int planId)
    {
        var plan = await _planService.GetPlanAsync(planId);
        if (plan == null) return;

        var items = await _shoppingListService.GetShoppingListAsync(plan.StartDate, plan.EndDate);
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
    }


    private void AddItem()
    {
        Items.Add(new Ingredient { Name = string.Empty, Quantity = 0, Unit = Unit.Gram });
    }

    private void RemoveItem(Ingredient? item)
    {
        if (item == null) return;
        Items.Remove(item);
    }
}
