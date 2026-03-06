using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class ShoppingListService : IShoppingListService
{
    private readonly AppDbContext _context;
    private readonly ISupabaseSyncService? _syncService;

    public ShoppingListService(AppDbContext context, IServiceProvider serviceProvider)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        try
        {
            _syncService = serviceProvider.GetService(typeof(ISupabaseSyncService)) as ISupabaseSyncService;
        }
        catch
        {
            _syncService = null;
        }
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
            existingItem.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            // Queue for sync (Update)
            if (_syncService != null)
            {
                try
                {
                    await _syncService.QueueForSyncAsync(existingItem, SyncOperationType.Update);
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Queued ShoppingListItem {existingItem.Id} for Update sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to queue sync: {syncEx.Message}");
                }
            }
            
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
            Order = order,
            CreatedAt = DateTime.UtcNow
        };
        _context.ShoppingListItems.Add(newItem);
        await _context.SaveChangesAsync();

        if (_syncService != null)
        {
            try
            {
                await _syncService.QueueForSyncAsync(newItem, SyncOperationType.Insert);
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Queued ShoppingListItem {newItem.Id} for Insert sync");
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to queue insert sync: {syncEx.Message}");
            }
        }
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
            
            // Queue for sync (Delete)
            if (_syncService != null)
            {
                try
                {
                    var deleteEntity = new ShoppingListItem { Id = existingItem.Id, IngredientName = existingItem.IngredientName };
                    await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Queued ShoppingListItem {existingItem.Id} for Delete sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to queue sync: {syncEx.Message}");
                }
            }
        }
    }

    public async Task RemoveShoppingListItemByIdAsync(Guid id)
    {
        var existingItem = await _context.ShoppingListItems.FirstOrDefaultAsync(sli => sli.Id == id);
        if (existingItem != null)
        {
            _context.ShoppingListItems.Remove(existingItem);
            await _context.SaveChangesAsync();
            
            // Queue for sync (Delete)
            if (_syncService != null)
            {
                try
                {
                    var deleteEntity = new ShoppingListItem { Id = id, IngredientName = existingItem.IngredientName };
                    await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Queued ShoppingListItem {id} for Delete sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to queue sync: {syncEx.Message}");
                }
            }
        }
    }

    public async Task SaveAllShoppingListStatesAsync(Guid planId, List<Ingredient> ingredients)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ========== SaveAllShoppingListStatesAsync STARTED ==========");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] PlanId: {planId}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Total ingredients to save: {ingredients?.Count ?? 0}");

            if (planId == Guid.Empty)
            {
                var errorMsg = "Cannot save shopping list: planId is empty";
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? VALIDATION ERROR: {errorMsg}");
                throw new ArgumentException(errorMsg, nameof(planId));
            }

            if (ingredients == null)
            {
                var errorMsg = "Cannot save shopping list: ingredients list is null";
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ? VALIDATION ERROR: {errorMsg}");
                throw new ArgumentNullException(nameof(ingredients), errorMsg);
            }

            var normalized = ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Normalized {normalized.Count} valid ingredients (filtered out {ingredients.Count - normalized.Count} invalid)");

            if (normalized.Count == 0)
            {
                var existingItems = await _context.ShoppingListItems.Where(sli => sli.PlanId == planId).ToListAsync();
                if (existingItems.Count > 0)
                {
                    _context.ShoppingListItems.RemoveRange(existingItems);
                    await _context.SaveChangesAsync();
                    await QueueShoppingListDeletesAsync(existingItems);
                }

                System.Diagnostics.Debug.WriteLine("[ShoppingListService] ========== SaveAllShoppingListStatesAsync COMPLETED (empty list) ==========");
                return;
            }

            for (int i = 0; i < normalized.Count; i++)
            {
                normalized[i].Order = i;
                if (normalized[i].Quantity <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShoppingListService] ?? WARNING: Ingredient '{normalized[i].Name}' has invalid quantity {normalized[i].Quantity}, using 1 instead");
                    normalized[i].Quantity = 1;
                }
            }

            var existing = await _context.ShoppingListItems
                .Where(sli => sli.PlanId == planId)
                .OrderBy(sli => sli.Order)
                .ToListAsync();

            var existingById = existing.ToDictionary(item => item.Id);
            var matchedExistingIds = new HashSet<Guid>();
            var insertedItems = new List<ShoppingListItem>();
            var updatedItems = new List<ShoppingListItem>();

            foreach (var src in normalized)
            {
                ShoppingListItem? entity = null;

                if (src.Id != Guid.Empty && existingById.TryGetValue(src.Id, out var existingByIdItem))
                {
                    entity = existingByIdItem;
                }
                else
                {
                    entity = existing.FirstOrDefault(item =>
                        !matchedExistingIds.Contains(item.Id) &&
                        item.IngredientName == src.Name &&
                        item.Unit == src.Unit);
                }

                if (entity == null)
                {
                    entity = new ShoppingListItem
                    {
                        Id = src.Id != Guid.Empty ? src.Id : Guid.NewGuid(),
                        PlanId = planId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ShoppingListItems.Add(entity);
                    insertedItems.Add(entity);
                }
                else
                {
                    updatedItems.Add(entity);
                }

                entity.IngredientName = src.Name!;
                entity.Unit = src.Unit;
                entity.IsChecked = src.IsChecked;
                entity.Quantity = src.Quantity;
                entity.Order = src.Order;
                entity.UpdatedAt = entity.CreatedAt == default ? null : DateTime.UtcNow;
                matchedExistingIds.Add(entity.Id);
                src.Id = entity.Id;
            }

            var itemsToDelete = existing
                .Where(item => !matchedExistingIds.Contains(item.Id))
                .ToList();

            if (itemsToDelete.Count > 0)
            {
                _context.ShoppingListItems.RemoveRange(itemsToDelete);
            }

            await _context.SaveChangesAsync();
            await QueueShoppingListDeletesAsync(itemsToDelete);
            await QueueShoppingListBatchAsync(insertedItems, SyncOperationType.Insert);
            await QueueShoppingListBatchAsync(updatedItems.Where(item => !insertedItems.Any(inserted => inserted.Id == item.Id)).ToList(), SyncOperationType.Update);

            System.Diagnostics.Debug.WriteLine("[ShoppingListService] ========== SAVE SUMMARY ==========");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Total ingredients: {normalized.Count}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Inserts: {insertedItems.Count}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Updates: {updatedItems.Count}");
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Deletes: {itemsToDelete.Count}");
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
            
            throw;
        }
    }

    private async Task QueueShoppingListBatchAsync(List<ShoppingListItem> items, SyncOperationType operation)
    {
        if (_syncService == null || items.Count == 0)
            return;

        try
        {
            await _syncService.QueueBatchForSyncAsync(items, operation);
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Queued {items.Count} ShoppingListItems for {operation} sync");
        }
        catch (Exception syncEx)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to queue {operation} batch sync: {syncEx.Message}");
        }
    }

    private async Task QueueShoppingListDeletesAsync(List<ShoppingListItem> items)
    {
        if (_syncService == null || items.Count == 0)
            return;

        foreach (var item in items)
        {
            try
            {
                var deleteEntity = new ShoppingListItem { Id = item.Id, IngredientName = item.IngredientName };
                await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListService] Failed to queue delete sync for item {item.Id}: {syncEx.Message}");
            }
        }
    }
}
