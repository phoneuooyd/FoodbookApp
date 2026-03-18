using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;
using FoodbookApp.Interfaces;

namespace Foodbook.Services
{
    public class PlannerService : IPlannerService
    {
        private readonly AppDbContext _context;
        private readonly ISupabaseSyncService? _syncService;

        public PlannerService(AppDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            
            try
            {
                _syncService = serviceProvider.GetService(typeof(ISupabaseSyncService)) as ISupabaseSyncService;
            }
            catch
            {
                _syncService = null;
            }
        }

        public async Task<List<PlannedMeal>> GetPlannedMealsAsync(DateTime from, DateTime to)
        {
            return await _context.PlannedMeals
                .Include(pm => pm.Recipe)
                .Include(pm => pm.Plan)
                .Where(pm => pm.Date >= from && pm.Date <= to)
                .Where(pm => pm.Plan != null && pm.Plan.Type == PlanType.Planner)
                .ToListAsync();
        }

        public async Task<List<PlannedMeal>> GetPlannedMealsAsync(Guid planId)
        {
            return await _context.PlannedMeals
                .Include(pm => pm.Recipe)
                .Where(pm => pm.PlanId == planId)
                .ToListAsync();
        }

        public async Task<PlannedMeal?> GetPlannedMealAsync(Guid id)
        {
            return await _context.PlannedMeals.Include(pm => pm.Recipe).FirstOrDefaultAsync(pm => pm.Id == id);
        }

        public async Task AddPlannedMealAsync(PlannedMeal meal)
        {
            if (meal.Id == Guid.Empty)
                meal.Id = Guid.NewGuid();

            _context.PlannedMeals.Add(meal);
            await _context.SaveChangesAsync();
            
            // Queue for sync (Insert)
            if (_syncService != null)
            {
                try
                {
                    await _syncService.QueueForSyncAsync(meal, SyncOperationType.Insert);
                    System.Diagnostics.Debug.WriteLine($"[PlannerService] Queued PlannedMeal {meal.Id} for Insert sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerService] Failed to queue sync: {syncEx.Message}");
                }
            }
        }

        public async Task UpdatePlannedMealAsync(PlannedMeal meal)
        {
            _context.PlannedMeals.Update(meal);
            await _context.SaveChangesAsync();
            
            // Queue for sync (Update)
            if (_syncService != null)
            {
                try
                {
                    await _syncService.QueueForSyncAsync(meal, SyncOperationType.Update);
                    System.Diagnostics.Debug.WriteLine($"[PlannerService] Queued PlannedMeal {meal.Id} for Update sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlannerService] Failed to queue sync: {syncEx.Message}");
                }
            }
        }

        public async Task RemovePlannedMealAsync(Guid id)
        {
            var meal = await _context.PlannedMeals.FindAsync(id);
            if (meal != null)
            {
                _context.PlannedMeals.Remove(meal);
                await _context.SaveChangesAsync();
                
                // Queue for sync (Delete)
                if (_syncService != null)
                {
                    try
                    {
                        var deleteEntity = new PlannedMeal { Id = id };
                        await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
                        System.Diagnostics.Debug.WriteLine($"[PlannerService] Queued PlannedMeal {id} for Delete sync");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlannerService] Failed to queue sync: {syncEx.Message}");
                    }
                }
            }
        }
    }
}