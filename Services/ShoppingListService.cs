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
                    Quantity = i.Quantity * pm.Portions,
                    IsChecked = false
                }));

        var grouped = ingredients
            .GroupBy(i => new { i.Name, i.Unit })
            .Select(g => new Ingredient
            {
                Name = g.Key.Name,
                Unit = g.Key.Unit,
                Quantity = g.Sum(i => i.Quantity),
                IsChecked = false
            })
            .ToList();

        return grouped;
    }
}
