using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class PlanService : IPlanService
{
    private readonly AppDbContext _context;
    private readonly IPlannerService _plannerService;
    private readonly IFeatureAccessService _featureAccessService;
    private readonly ISupabaseSyncService? _syncService;

    public PlanService(AppDbContext context, IPlannerService plannerService, IFeatureAccessService featureAccessService, IServiceProvider serviceProvider)
    {
        _context = context;
        _plannerService = plannerService;
        _featureAccessService = featureAccessService;

        try
        {
            _syncService = serviceProvider.GetService(typeof(ISupabaseSyncService)) as ISupabaseSyncService;
        }
        catch
        {
            _syncService = null;
        }
    }

    public async Task<List<Plan>> GetPlansAsync()
    {
        return await _context.Plans.ToListAsync();
    }

    public async Task<List<Plan>> GetFoodbooksAsync()
    {
        return await _context.Plans
            .Where(p => p.Type == PlanType.Foodbook && !p.IsArchived)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Plan>> GetArchivedPlansAsync()
    {
        return await _context.Plans
            .Where(p => p.IsArchived)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<Plan?> GetPlanAsync(Guid id)
    {
        return await _context.Plans.FindAsync(id);
    }

    public async Task AddPlanAsync(Plan plan)
    {
        if (plan.Id == Guid.Empty)
            plan.Id = Guid.NewGuid();

        var nowUtc = DateTime.UtcNow;
        var decision = await _featureAccessService.CanCreatePlanAsync(plan.Type, nowUtc);
        if (!decision.IsAllowed)
        {
            throw new PlanLimitExceededException(decision.UserMessage);
        }

        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();
        await _featureAccessService.RegisterPlanCreationAsync(plan.Type, nowUtc);

        if (_syncService != null)
        {
            try
            {
                await _syncService.QueueForSyncAsync(plan, SyncOperationType.Insert);
                System.Diagnostics.Debug.WriteLine($"[PlanService] Queued plan {plan.Id} for Insert sync");
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[PlanService] Failed to queue insert sync: {syncEx.Message}");
            }
        }
    }

    public async Task UpdatePlanAsync(Plan plan)
    {
        _context.Plans.Update(plan);
        await _context.SaveChangesAsync();

        // Queue for sync (Update) - only if metadata changed (dates, name, etc)
        // This is rare; most changes are via PlannedMeal sync
        if (_syncService != null)
        {
            try
            {
                await _syncService.QueueForSyncAsync(plan, SyncOperationType.Update);
                System.Diagnostics.Debug.WriteLine($"[PlanService] Queued plan {plan.Id} for Update sync (metadata change)");
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[PlanService] Failed to queue sync: {syncEx.Message}");
            }
        }
    }

    public async Task RemovePlanAsync(Guid id)
    {
        var plan = await _context.Plans.FindAsync(id);
        if (plan == null) return;

        var meals = await _context.PlannedMeals.Where(m => m.PlanId == id).ToListAsync();
        var shoppingItems = await _context.ShoppingListItems.Where(i => i.PlanId == id).ToListAsync();

        _context.PlannedMeals.RemoveRange(meals);
        _context.ShoppingListItems.RemoveRange(shoppingItems);
        _context.Plans.Remove(plan);
        await _context.SaveChangesAsync();

        if (_syncService != null)
        {
            try
            {
                await _syncService.QueueForSyncAsync(new Plan { Id = id, Title = plan.Title }, SyncOperationType.Delete);
                foreach (var meal in meals)
                    await _syncService.QueueForSyncAsync(new PlannedMeal { Id = meal.Id }, SyncOperationType.Delete);
                foreach (var item in shoppingItems)
                    await _syncService.QueueForSyncAsync(new ShoppingListItem { Id = item.Id }, SyncOperationType.Delete);
                System.Diagnostics.Debug.WriteLine($"[PlanService] Queued plan {id}, {meals.Count} meals and {shoppingItems.Count} shopping items for Delete sync");
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[PlanService] Failed to queue sync: {syncEx.Message}");
            }
        }
    }

    public async Task<Plan> ApplyFoodbookAsync(Guid foodbookId, DateTime startDate)
    {
        var foodbook = await _context.Plans.FindAsync(foodbookId)
            ?? throw new InvalidOperationException("Foodbook not found");

        if (foodbook.Type != PlanType.Foodbook)
            throw new InvalidOperationException("Selected plan is not a Foodbook");

        var meals = await _context.PlannedMeals
            .Include(m => m.Recipe)
            .Where(m => m.PlanId == foodbookId)
            .ToListAsync();

        var minDate = meals.Count > 0 ? meals.Min(m => m.Date).Date : DateTime.Today;
        var offset = startDate.Date - minDate;

        var newPlan = new Plan
        {
            Type = PlanType.Planner,
            Title = foodbook.Title,
            StartDate = startDate.Date,
            EndDate = startDate.Date.AddDays(Math.Max(1, foodbook.DurationDays) - 1),
        };

        await AddPlanAsync(newPlan);

        foreach (var meal in meals)
        {
            await _plannerService.AddPlannedMealAsync(new PlannedMeal
            {
                PlanId = newPlan.Id,
                RecipeId = meal.RecipeId,
                Recipe = meal.Recipe,
                Date = meal.Date.Date + offset,
                Portions = meal.Portions,
            });
        }

        return newPlan;
    }

    public async Task<bool> HasOverlapAsync(DateTime from, DateTime to, Guid? ignoreId = null)
    {
        return await _context.Plans.AnyAsync(p =>
            (ignoreId == null || p.Id != ignoreId) &&
            p.Type == PlanType.Planner &&
            !p.IsArchived &&
            p.StartDate <= to &&
            p.EndDate >= from);
    }
}
