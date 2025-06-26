using Foodbook.Data;
using Foodbook.Models;
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

    public async Task<Plan?> GetPlanAsync(int id)
    {
        return await _context.Plans.FindAsync(id);
    }

    public async Task AddPlanAsync(Plan plan)
    {
        _context.Plans.Add(plan);
        await _context.SaveChangesAsync();
    }

    public async Task RemovePlanAsync(int id)
    {
        var plan = await _context.Plans.FindAsync(id);
        if (plan != null)
        {
            _context.Plans.Remove(plan);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasOverlapAsync(DateTime from, DateTime to, int? ignoreId = null)
    {
        return await _context.Plans.AnyAsync(p => (ignoreId == null || p.Id != ignoreId) && p.StartDate <= to && p.EndDate >= from);
    }
}
