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
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ========== SaveAllShoppingListStatesAsync STARTED ==========");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] PlanId: {planId}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Total ingredients to save: {ingredients?.Count ?? 0}");

            // VALIDATION: Check plan ID
            if (planId == Guid.Empty)
            {
                var errorMsg = "Cannot save shopping list: planId is empty";
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? VALIDATION ERROR: {errorMsg}");
                throw new ArgumentException(errorMsg, nameof(planId));
            }

            // VALIDATION: Check ingredients list
            if (ingredients == null)
            {
                var errorMsg = "Cannot save shopping list: ingredients list is null";
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? VALIDATION ERROR: {errorMsg}");
                throw new ArgumentNullException(nameof(ingredients), errorMsg);
            }

            // STEP 1: Remove existing items
            System.Diagnostics.Debug.WriteLine("[ShoppingListService] Step 1: Removing existing items...");
            var existing = await _context.ShoppingListItems.Where(sli => sli.PlanId == planId).ToListAsync();
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Found {existing.Count} existing items to remove");
            
            if (existing.Count > 0)
            {
                try
                {
                    _context.ShoppingListItems.RemoveRange(existing);
                    await _context.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? Successfully removed {existing.Count} existing items");
                }
                catch (Exception removeEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? ERROR removing existing items: {removeEx.Message}");
                    throw new InvalidOperationException("Failed to remove existing shopping list items", removeEx);
                }
            }

            // STEP 2: Normalize and validate ingredients
            System.Diagnostics.Debug.WriteLine("[ShoppingListService] Step 2: Normalizing and validating ingredients...");
            var normalized = ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Normalized {normalized.Count} valid ingredients (filtered out {ingredients.Count - normalized.Count} invalid)");
            
            if (normalized.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[ShoppingListService] ?? WARNING: No valid ingredients to save after normalization");
                System.Diagnostics.Debug.WriteLine("[ShoppingListService] ========== SaveAllShoppingListStatesAsync COMPLETED (empty list) ==========");
                return;
            }

            // Assign order based on position in list
            for (int i = 0; i < normalized.Count; i++) 
            {
                normalized[i].Order = i;
            }
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? Assigned order to {normalized.Count} ingredients");

            // STEP 3: Save each ingredient
            System.Diagnostics.Debug.WriteLine("[ShoppingListService] Step 3: Saving ingredients...");
            int savedCount = 0;
            int failedCount = 0;
            
            foreach (var src in normalized)
            {
                try
                {
                    // Additional validation per item
                    if (string.IsNullOrWhiteSpace(src.Name))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ?? Skipping ingredient with empty name at order {src.Order}");
                        failedCount++;
                        continue;
                    }

                    if (src.Quantity <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ?? WARNING: Ingredient '{src.Name}' has invalid quantity {src.Quantity}, using 1 instead");
                        src.Quantity = 1;
                    }

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
                    
                    try
                    {
                        await _context.SaveChangesAsync();
                        src.Id = entity.Id;
                        savedCount++;
                        
                        if (savedCount % 10 == 0 || savedCount == normalized.Count)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Progress: {savedCount}/{normalized.Count} items saved");
                        }
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? DATABASE ERROR saving ingredient '{src.Name}': {dbEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Inner exception: {dbEx.InnerException?.Message}");
                        
                        // Remove the failed entity from context to prevent further issues
                        _context.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        
                        failedCount++;
                        
                        // If this is a critical error (e.g., constraint violation), throw to abort
                        if (dbEx.InnerException?.Message?.Contains("constraint", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            throw new InvalidOperationException($"Database constraint violation when saving '{src.Name}'", dbEx);
                        }
                    }
                    catch (Exception itemEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? ERROR saving ingredient '{src.Name}': {itemEx.Message}");
                        
                        // Remove the failed entity from context
                        try
                        {
                            _context.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        }
                        catch { }
                        
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? ERROR processing ingredient at order {src.Order}: {ex.Message}");
                    failedCount++;
                }
            }

            // SUMMARY
            System.Diagnostics.Debug.WriteLine("[ShoppingListService] ========== SAVE SUMMARY ==========");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Total ingredients: {normalized.Count}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Successfully saved: {savedCount}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to save: {failedCount}");
            
            if (failedCount > 0 && savedCount == 0)
            {
                throw new InvalidOperationException($"Failed to save all {failedCount} shopping list items. No items were saved.");
            }
            else if (failedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ?? WARNING: Partial save - {failedCount} items failed");
            }
            
            System.Diagnostics.Debug.WriteLine("[ShoppingListService] ========== SaveAllShoppingListStatesAsync COMPLETED ==========");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? CRITICAL ERROR in SaveAllShoppingListStatesAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Exception type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Stack trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Inner exception: {ex.InnerException.Message}");
            }
            
            throw; // Re-throw to let caller handle the error
        }
    }
}
