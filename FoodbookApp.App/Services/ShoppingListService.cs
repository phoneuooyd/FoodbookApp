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

        // Get the base shopping list from recipes
        var recipeIngredients = await GetShoppingListAsync(plan.StartDate, plan.EndDate);

        // Get all saved states for this plan (including manually added items)
        var savedStates = await _context.ShoppingListItems
            .Where(sli => sli.PlanId == planId)
            .OrderBy(sli => sli.Order)
            .ToListAsync();

        var resultIngredients = new List<Ingredient>();

        // Process recipe ingredients first
        foreach (var ingredient in recipeIngredients)
        {
            var savedState = savedStates.FirstOrDefault(s => 
                s.IngredientName == ingredient.Name && s.Unit == ingredient.Unit);
            
            if (savedState != null)
            {
                ingredient.IsChecked = savedState.IsChecked;
                ingredient.Order = savedState.Order;
                ingredient.Quantity = savedState.Quantity; // Use saved quantity (might be user-modified)
            }

            resultIngredients.Add(ingredient);
        }

        // Add manually added items that don't correspond to recipe ingredients
        var recipeIngredientKeys = recipeIngredients.Select(i => new { i.Name, i.Unit }).ToHashSet();
        var manuallyAddedStates = savedStates.Where(s => 
            !recipeIngredientKeys.Contains(new { Name = s.IngredientName, Unit = s.Unit }) &&
            !string.IsNullOrWhiteSpace(s.IngredientName)).ToList();

        foreach (var manualState in manuallyAddedStates)
        {
            var manualIngredient = new Ingredient
            {
                Name = manualState.IngredientName,
                Unit = manualState.Unit,
                Quantity = manualState.Quantity,
                IsChecked = manualState.IsChecked,
                Order = manualState.Order
            };
            resultIngredients.Add(manualIngredient);
        }

        // Sort all ingredients by order
        return resultIngredients.OrderBy(i => i.Order).ToList();
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

    public async Task RemoveShoppingListItemAsync(int planId, string ingredientName, Unit unit)
    {
        var existingItem = await _context.ShoppingListItems
            .FirstOrDefaultAsync(sli => sli.PlanId == planId && 
                                      sli.IngredientName == ingredientName && 
                                      sli.Unit == unit);

        if (existingItem != null)
        {
            _context.ShoppingListItems.Remove(existingItem);
            await _context.SaveChangesAsync();
        }
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

        // Only remove items that have empty/invalid names (these are temporary items that shouldn't be saved)
        // Don't remove manually added items that user created
        var itemsToRemove = existingItems.Where(existing => 
            string.IsNullOrWhiteSpace(existing.IngredientName) ||
            (!ingredients.Any(i => i.Name == existing.IngredientName && i.Unit == existing.Unit) &&
             !IsManuallyAddedItem(existing.IngredientName)))
            .ToList();

        _context.ShoppingListItems.RemoveRange(itemsToRemove);

        await _context.SaveChangesAsync();
    }

    private bool IsManuallyAddedItem(string ingredientName)
    {
        // Check if this is a manually added item (not from recipe ingredients)
        // We consider items with default names or custom names as manually added
        return ingredientName.StartsWith("Nowy sk³adnik") || 
               !string.IsNullOrWhiteSpace(ingredientName);
    }
}
