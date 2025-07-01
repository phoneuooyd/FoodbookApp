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

    /// <summary>
    /// Stara metoda - zachowana dla kompatybilnoœci
    /// </summary>
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

    /// <summary>
    /// Pobiera pozycje listy zakupów dla okreœlonego planu
    /// </summary>
    public async Task<List<ShoppingListItem>> GetShoppingListItemsAsync(int planId)
    {
        return await _context.ShoppingListItems
            .Where(item => item.PlanId == planId)
            .OrderBy(item => item.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Generuje pozycje listy zakupów dla planu z powi¹zaniami do przepisów
    /// </summary>
    public async Task<List<ShoppingListItem>> GenerateShoppingListAsync(int planId)
    {
        // Pobierz plan z datami
        var plan = await _context.Plans.FindAsync(planId);
        if (plan == null)
            return new List<ShoppingListItem>();

        // Usuñ istniej¹ce pozycje dla tego planu
        var existingItems = await _context.ShoppingListItems
            .Where(item => item.PlanId == planId)
            .ToListAsync();
        
        _context.ShoppingListItems.RemoveRange(existingItems);

        // Pobierz wszystkie zaplanowane posi³ki dla tego planu
        var meals = await _context.PlannedMeals
            .Include(pm => pm.Recipe)
                .ThenInclude(r => r.Ingredients)
            .Where(pm => pm.Date >= plan.StartDate && pm.Date <= plan.EndDate)
            .ToListAsync();

        // Grupuj sk³adniki z informacj¹ o przepisach
        var ingredientGroups = new Dictionary<(string Name, Unit Unit), ShoppingListItem>();

        foreach (var meal in meals)
        {
            if (meal.Recipe?.Ingredients == null)
                continue;

            foreach (var ingredient in meal.Recipe.Ingredients)
            {
                var key = (ingredient.Name, ingredient.Unit);
                var quantity = ingredient.Quantity * meal.Portions;

                if (ingredientGroups.TryGetValue(key, out var existingItem))
                {
                    // Aktualizuj istniej¹c¹ pozycjê
                    existingItem.Quantity += quantity;
                    
                    var recipeIds = existingItem.RecipeIdsList;
                    var recipeNames = existingItem.RecipeNamesList;
                    
                    if (!recipeIds.Contains(meal.Recipe.Id))
                    {
                        recipeIds.Add(meal.Recipe.Id);
                        recipeNames.Add(meal.Recipe.Name);
                        
                        existingItem.RecipeIdsList = recipeIds;
                        existingItem.RecipeNamesList = recipeNames;
                    }
                }
                else
                {
                    // Utwórz now¹ pozycjê
                    var newItem = new ShoppingListItem
                    {
                        Name = ingredient.Name,
                        Unit = ingredient.Unit,
                        Quantity = quantity,
                        PlanId = planId,
                        RecipeIdsList = new List<int> { meal.Recipe.Id },
                        RecipeNamesList = new List<string> { meal.Recipe.Name }
                    };
                    
                    ingredientGroups[key] = newItem;
                }
            }
        }

        // Dodaj do kontekstu
        var shoppingListItems = ingredientGroups.Values.ToList();
        await _context.ShoppingListItems.AddRangeAsync(shoppingListItems);
        await _context.SaveChangesAsync();

        return shoppingListItems;
    }

    /// <summary>
    /// Aktualizuje pozycjê listy zakupów
    /// </summary>
    public async Task UpdateShoppingListItemAsync(ShoppingListItem item)
    {
        _context.ShoppingListItems.Update(item);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Usuwa pozycjê z listy zakupów
    /// </summary>
    public async Task DeleteShoppingListItemAsync(int itemId)
    {
        var item = await _context.ShoppingListItems.FindAsync(itemId);
        if (item != null)
        {
            _context.ShoppingListItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
