using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly AppDbContext _context;

    public ShoppingListService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Ingredient>> GetShoppingListAsync(DateTime from, DateTime to)
    {
        var meals = await _context.PlannedMeals
            .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
            .Where(pm => pm.Date >= from && pm.Date <= to)
            .ToListAsync();

        return AggregateIngredientsFromMeals(meals);
    }

    private static List<Ingredient> AggregateIngredientsFromMeals(List<PlannedMeal> meals)
    {
        var ingredients = meals
            .SelectMany(pm =>
                (pm.Recipe?.Ingredients ?? Enumerable.Empty<Ingredient>())
                .Select(i => new Ingredient
                {
                    Name = i.Name,
                    Unit = i.Unit,
                    Quantity = i.Quantity * pm.Portions
                }));

        return ingredients
            .GroupBy(i => new { i.Name, i.Unit })
            .Select(g => new Ingredient
            {
                Name = g.Key.Name,
                Unit = g.Key.Unit,
                Quantity = g.Sum(i => i.Quantity)
            })
            .ToList();
    }

    private async Task<List<Ingredient>> GetShoppingListForPlanAsync(int mealPlanId)
    {
        var meals = await _context.PlannedMeals
            .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
            .Where(pm => pm.PlanId == mealPlanId)
            .ToListAsync();

        return AggregateIngredientsFromMeals(meals);
    }

    public async Task<List<Ingredient>> GetShoppingListWithCheckedStateAsync(int planId)
    {
        var plan = await _context.Plans.FindAsync(planId);
        if (plan == null) return new List<Ingredient>();

        List<Ingredient> recipeIngredients = new();

        // Build base items strictly by linkage, not by date range
        if (plan.Type == PlanType.ShoppingList)
        {
            // Find the planner linked to this shopping list (one-to-one expected)
            var linkedMealPlanId = await _context.Plans
                .Where(p => p.Type == PlanType.Planner && p.LinkedShoppingListPlanId == planId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            if (linkedMealPlanId.HasValue)
            {
                // Shopping list is linked: base on meals of that specific planner
                recipeIngredients = await GetShoppingListForPlanAsync(linkedMealPlanId.Value);
            }
            else
            {
                // Manual list (unlinked): do NOT prefill from recipes
                recipeIngredients = new List<Ingredient>();
            }
        }
        else if (plan.Type == PlanType.Planner)
        {
            // Called for a planner directly: base on its meals
            recipeIngredients = await GetShoppingListForPlanAsync(plan.Id);
        }

        // Saved item states for this shopping list plan
        var savedStates = await _context.ShoppingListItems
            .Where(sli => sli.PlanId == planId)
            .OrderBy(sli => sli.Order)
            .ToListAsync();

        var resultIngredients = new List<Ingredient>();

        // Seed from recipe-derived ingredients (if any), overlay saved states
        foreach (var ingredient in recipeIngredients)
        {
            var savedState = savedStates.FirstOrDefault(s =>
                s.IngredientName == ingredient.Name && s.Unit == ingredient.Unit);

            if (savedState != null)
            {
                ingredient.IsChecked = savedState.IsChecked;
                ingredient.Order = savedState.Order;
                ingredient.Quantity = savedState.Quantity;
            }

            resultIngredients.Add(ingredient);
        }

        // Add manual-only items saved for this list (not present in recipeIngredients)
        var recipeKeys = recipeIngredients.Select(i => new { i.Name, i.Unit }).ToHashSet();
        var manualStates = savedStates.Where(s =>
            !recipeKeys.Contains(new { Name = s.IngredientName, Unit = s.Unit }) &&
            !string.IsNullOrWhiteSpace(s.IngredientName))
            .ToList();

        foreach (var manual in manualStates)
        {
            resultIngredients.Add(new Ingredient
            {
                Name = manual.IngredientName,
                Unit = manual.Unit,
                Quantity = manual.Quantity,
                IsChecked = manual.IsChecked,
                Order = manual.Order
            });
        }

        return resultIngredients.OrderBy(i => i.Order).ToList();
    }

    // Matches new interface: update or insert by Id -> (PlanId+Order) -> (PlanId+Name+Unit)
    public async Task SaveShoppingListItemStateAsync(int planId, int id, int order, string ingredientName, Unit unit, bool isChecked, double quantity)
    {
        if (ingredientName == null) throw new ArgumentNullException(nameof(ingredientName));

        ShoppingListItem? existingItem = null;

        if (id > 0)
        {
            existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.Id == id);
        }

        if (existingItem == null)
        {
            existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.PlanId == planId && sli.Order == order);
        }

        if (existingItem == null)
        {
            existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.PlanId == planId && sli.IngredientName == ingredientName && sli.Unit == unit);
        }

        if (existingItem != null)
        {
            existingItem.IngredientName = ingredientName;
            existingItem.Unit = unit;
            existingItem.IsChecked = isChecked;
            existingItem.Quantity = quantity;
            existingItem.Order = order;
        }
        else
        {
            _context.ShoppingListItems.Add(new ShoppingListItem
            {
                PlanId = planId,
                IngredientName = ingredientName,
                Unit = unit,
                IsChecked = isChecked,
                Quantity = quantity,
                Order = order
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task RemoveShoppingListItemAsync(int planId, string ingredientName, Unit unit)
    {
        if (ingredientName == null) return;

        var existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.PlanId == planId &&
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
        var safeIngredients = ingredients ?? new List<Ingredient>();

        var existingItems = await _context.ShoppingListItems
            .Where(sli => sli.PlanId == planId)
            .ToListAsync();

        foreach (var ingredient in safeIngredients)
        {
            ShoppingListItem? existing = null;

            if (ingredient.Id > 0)
            {
                existing = existingItems.FirstOrDefault(sli => sli.Id == ingredient.Id);
            }
            if (existing == null)
            {
                existing = existingItems.FirstOrDefault(sli => sli.Order == ingredient.Order);
            }
            if (existing == null)
            {
                existing = existingItems.FirstOrDefault(sli => sli.IngredientName == ingredient.Name && sli.Unit == ingredient.Unit);
            }

            if (existing != null)
            {
                existing.IngredientName = ingredient.Name;
                existing.Unit = ingredient.Unit;
                existing.IsChecked = ingredient.IsChecked;
                existing.Quantity = ingredient.Quantity;
                existing.Order = ingredient.Order;
            }
            else
            {
                _context.ShoppingListItems.Add(new ShoppingListItem
                {
                    PlanId = planId,
                    IngredientName = ingredient.Name,
                    Unit = ingredient.Unit,
                    IsChecked = ingredient.IsChecked,
                    Quantity = ingredient.Quantity,
                    Order = ingredient.Order
                });
            }
        }

        // Remove only invalid placeholders
        var toRemove = existingItems.Where(e => string.IsNullOrWhiteSpace(e.IngredientName)).ToList();
        _context.ShoppingListItems.RemoveRange(toRemove);

        await _context.SaveChangesAsync();
    }
}
