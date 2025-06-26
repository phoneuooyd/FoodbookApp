using System.Collections.ObjectModel;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class ShoppingListDetailViewModel
{
    private readonly IShoppingListService _shoppingListService;
    private readonly IPlanService _planService;

    public ObservableCollection<Ingredient> Items { get; } = new();

    public ShoppingListDetailViewModel(IShoppingListService shoppingListService, IPlanService planService)
    {
        _shoppingListService = shoppingListService;
        _planService = planService;
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
}
