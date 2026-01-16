using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class PlanService : IPlanService
{
    private readonly AppDbContext _context;
    private readonly ISupabaseSyncService? _syncService;

    public PlanService(AppDbContext context, IServiceProvider serviceProvider)
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

    public async Task<List<Plan>> GetPlansAsync()
    {
        return await _context.Plans.ToListAsync();
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

        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePlanAsync(Plan plan)
    {
        _context.Plans.Update(plan);
        await _context.SaveChangesAsync();
        
        // Queue for sync (Update)
        if (_syncService != null)
        {
            try
            {
                await _syncService.QueueForSyncAsync(plan, SyncOperationType.Update);
                System.Diagnostics.Debug.WriteLine($"[PlanService] Queued plan {plan.Id} for Update sync");
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
        if (plan != null)
        {
            _context.Plans.Remove(plan);
            await _context.SaveChangesAsync();
            
            // Queue for sync (Delete)
            if (_syncService != null)
            {
                try
                {
                    var deleteEntity = new Plan { Id = id, Title = plan.Title };
                    await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete);
                    System.Diagnostics.Debug.WriteLine($"[PlanService] Queued plan {id} for Delete sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlanService] Failed to queue sync: {syncEx.Message}");
                }
            }
        }
    }

    public async Task<bool> HasOverlapAsync(DateTime from, DateTime to, Guid? ignoreId = null)
    {
        return await _context.Plans.AnyAsync(p =>
            (ignoreId == null || p.Id != ignoreId) &&
            !p.IsArchived &&
            p.StartDate <= to &&
            p.EndDate >= from);
    }
}
