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
        var ingredients = meals.SelectMany(pm => (pm.Recipe?.Ingredients ?? Enumerable.Empty<Ingredient>())
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

    private async Task<List<Ingredient>> GetShoppingListForPlanAsync(Guid mealPlanId)
    {
        var meals = await _context.PlannedMeals
            .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
            .Where(pm => pm.PlanId == mealPlanId)
            .ToListAsync();
        return AggregateIngredientsFromMeals(meals);
    }

    public async Task<List<Ingredient>> GetShoppingListWithCheckedStateAsync(Guid planId)
    {
        var plan = await _context.Plans.FindAsync(planId);
        if (plan == null) return new List<Ingredient>();

        var savedStates = await _context.ShoppingListItems
            .Where(sli => sli.PlanId == planId)
            .OrderBy(sli => sli.Order)
            .ToListAsync();

        if (savedStates.Any())
        {
            return savedStates.Select(s => new Ingredient
            {
                Id = s.Id,
                Name = s.IngredientName,
                Unit = s.Unit,
                Quantity = s.Quantity,
                IsChecked = s.IsChecked,
                Order = s.Order
            }).OrderBy(i => i.Order).ToList();
        }

        if (plan.Type == PlanType.ShoppingList)
        {
            var linkedPlanner = await _context.Plans
                .FirstOrDefaultAsync(p => p.Type == PlanType.Planner && p.LinkedShoppingListPlanId == planId);

            if (linkedPlanner != null)
            {
                var baseIngredients = await GetShoppingListForPlanAsync(linkedPlanner.Id);
                for (int i = 0; i < baseIngredients.Count; i++)
                {
                    baseIngredients[i].Order = i;
                    baseIngredients[i].Id = Guid.Empty;
                }
                return baseIngredients;
            }

            return new List<Ingredient>();
        }

        if (plan.Type == PlanType.Planner)
        {
            var recipeIngredients = await GetShoppingListForPlanAsync(plan.Id);
            for (int i = 0; i < recipeIngredients.Count; i++)
            {
                recipeIngredients[i].Order = i;
                recipeIngredients[i].Id = Guid.Empty;
            }
            return recipeIngredients;
        }

        return new List<Ingredient>();
    }

    public async Task<Guid> SaveShoppingListItemStateAsync(Guid planId, Guid id, int order, string ingredientName, Unit unit, bool isChecked, double quantity)
    {
        if (ingredientName == null) throw new ArgumentNullException(nameof(ingredientName));

        ShoppingListItem? existingItem = null;
        if (id != Guid.Empty)
            existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.Id == id);
        if (existingItem == null && order >= 0)
            existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.PlanId == planId && sli.Order == order);
        if (existingItem == null)
            existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.PlanId == planId && sli.IngredientName == ingredientName && sli.Unit == unit);

        if (existingItem != null)
        {
            existingItem.IngredientName = ingredientName;
            existingItem.Unit = unit;
            existingItem.IsChecked = isChecked;
            existingItem.Quantity = quantity;
            existingItem.Order = order;
            await _context.SaveChangesAsync();
            return existingItem.Id;
        }

        var newItem = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            IngredientName = ingredientName,
            Unit = unit,
            IsChecked = isChecked,
            Quantity = quantity,
            Order = order
        };
        _context.ShoppingListItems.Add(newItem);
        await _context.SaveChangesAsync();
        return newItem.Id;
    }

    public async Task RemoveShoppingListItemAsync(Guid planId, string ingredientName, Unit unit)
    {
        if (ingredientName == null) return;
        var existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.PlanId == planId && sli.IngredientName == ingredientName && sli.Unit == unit);
        if (existingItem != null)
        {
            _context.ShoppingListItems.Remove(existingItem);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveShoppingListItemByIdAsync(Guid id)
    {
        var existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.Id == id);
        if (existingItem != null)
        {
            _context.ShoppingListItems.Remove(existingItem);
            await _context.SaveChangesAsync();
        }
    }

    public async Task SaveAllShoppingListStatesAsync(Guid planId, List<Ingredient> ingredients)
    {
        var existing = await _context.ShoppingListItems.Where(sli => sli.PlanId == planId).ToListAsync();
        if (existing.Count > 0)
        {
            _context.ShoppingListItems.RemoveRange(existing);
            await _context.SaveChangesAsync();
        }

        var normalized = (ingredients ?? new List<Ingredient>()).Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
        for (int i = 0; i < normalized.Count; i++) normalized[i].Order = i;

        foreach (var src in normalized)
        {
            var entity = new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                PlanId = planId,
                IngredientName = src.Name!,
                Unit = src.Unit,
                IsChecked = src.IsChecked,
                Quantity = src.Quantity,
                Order = src.Order
            };
            _context.ShoppingListItems.Add(entity);
            await _context.SaveChangesAsync();
            src.Id = entity.Id;
        }
    }
}
