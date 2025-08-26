using Foodbook.Data;
using Foodbook.Models;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly AppDbContext _context;

    public ShoppingListService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to)
    {
        var meals = await _context.PlannedMeals
            .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
            .Where(pm => pm.Date >= from && pm.Date <= to)
            .ToListAsync();

        var ingredients = meals
            .SelectMany(pm =>
                (pm.Recipe?.Ingredients ?? Enumerable.Empty<Ingredient>())
                .Select(i => new Ingredient
                {
                    Name = i.Name,
                    Unit = i.Unit,
                    Quantity = i.Quantity * pm.Portions
                }));

        var grouped = ingredients
            .GroupBy(i => new { i.Name, i.Unit })
            .Select(g => new Ingredient
            {
                Name = g.Key.Name,
                Unit = g.Key.Unit,
                Quantity = g.Sum(i => i.Quantity)
            })
            .ToList();

        return grouped;
    }

    public async Task<List<Ingredient>> GetShoppingListWithCheckedStateAsync(int planId)
    {
        var plan = await _context.Plans.FindAsync(planId);
        if (plan == null) return new List<Ingredient>();

        // Get the base shopping list
        var ingredients = await GetShoppingListAsync(plan.StartDate, plan.EndDate);

        // Get saved checked states
        var savedStates = await _context.ShoppingListItems
            .Where(sli => sli.PlanId == planId)
            .OrderBy(sli => sli.Order)
            .ToListAsync();

        // Apply saved states to ingredients
        foreach (var ingredient in ingredients)
        {
            var savedState = savedStates.FirstOrDefault(s => 
                s.IngredientName == ingredient.Name && s.Unit == ingredient.Unit);
            
            if (savedState != null)
            {
                ingredient.IsChecked = savedState.IsChecked;
                ingredient.Order = savedState.Order;
            }
        }

        // Sort ingredients by order
        return ingredients.OrderBy(i => i.Order).ToList();
    }

    public async Task SaveShoppingListItemStateAsync(int planId, string ingredientName, Unit unit, bool isChecked, double quantity)
    {
        var existingItem = await _context.ShoppingListItems
            .FirstOrDefaultAsync(sli => sli.PlanId == planId && 
                                      sli.IngredientName == ingredientName && 
                                      sli.Unit == unit);

        if (existingItem != null)
        {
            existingItem.IsChecked = isChecked;
            existingItem.Quantity = quantity;
        }
        else
        {
            // Get the next order value for this plan
            var maxOrder = await _context.ShoppingListItems
                .Where(sli => sli.PlanId == planId)
                .MaxAsync(sli => (int?)sli.Order) ?? -1;

            var newItem = new ShoppingListItem
            {
                PlanId = planId,
                IngredientName = ingredientName,
                Unit = unit,
                IsChecked = isChecked,
                Quantity = quantity,
                Order = maxOrder + 1
            };
            _context.ShoppingListItems.Add(newItem);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SaveAllShoppingListStatesAsync(int planId, List<Ingredient> ingredients)
    {
        // Get existing saved states for this plan
        var existingItems = await _context.ShoppingListItems
            .Where(sli => sli.PlanId == planId)
            .ToListAsync();

        // Process each ingredient
        foreach (var ingredient in ingredients)
        {
            var existingItem = existingItems.FirstOrDefault(sli => 
                sli.IngredientName == ingredient.Name && sli.Unit == ingredient.Unit);

            if (existingItem != null)
            {
                existingItem.IsChecked = ingredient.IsChecked;
                existingItem.Quantity = ingredient.Quantity;
                existingItem.Order = ingredient.Order;
            }
            else
            {
                var newItem = new ShoppingListItem
                {
                    PlanId = planId,
                    IngredientName = ingredient.Name,
                    Unit = ingredient.Unit,
                    IsChecked = ingredient.IsChecked,
                    Quantity = ingredient.Quantity,
                    Order = ingredient.Order
                };
                _context.ShoppingListItems.Add(newItem);
            }
        }

        // Remove items that are no longer in the shopping list
        var currentIngredientKeys = ingredients.Select(i => new { i.Name, i.Unit }).ToHashSet();
        var itemsToRemove = existingItems.Where(existing => 
            !currentIngredientKeys.Contains(new { Name = existing.IngredientName, Unit = existing.Unit }))
            .ToList();

        _context.ShoppingListItems.RemoveRange(itemsToRemove);

        await _context.SaveChangesAsync();
    }
}
