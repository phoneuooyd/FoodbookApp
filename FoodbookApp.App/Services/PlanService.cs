using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class PlanService : IPlanService
{
    private readonly AppDbContext _context;

    public PlanService(AppDbContext context)
    {
        _context = context;
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
    }

    public async Task RemovePlanAsync(Guid id)
    {
        var plan = await _context.Plans.FindAsync(id);
        if (plan != null)
        {
            _context.Plans.Remove(plan);
            await _context.SaveChangesAsync();
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
