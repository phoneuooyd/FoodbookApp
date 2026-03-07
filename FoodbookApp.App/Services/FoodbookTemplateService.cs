using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class FoodbookTemplateService : IFoodbookTemplateService
{
    private readonly AppDbContext _context;
    private readonly IAccountService _accountService;

    public FoodbookTemplateService(AppDbContext context, IAccountService accountService)
    {
        _context = context;
        _accountService = accountService;
    }

    public async Task<List<FoodbookTemplate>> GetTemplatesAsync(bool includePublic = true)
    {
        var userId = await GetCurrentUserIdAsync();

        var query = _context.FoodbookTemplates
            .Include(t => t.Meals)
            .ThenInclude(m => m.Recipe)
            .AsQueryable();

        query = includePublic
            ? query.Where(t => t.UserId == userId || t.IsPublic)
            : query.Where(t => t.UserId == userId);

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<FoodbookTemplate?> GetTemplateAsync(Guid templateId)
    {
        var userId = await GetCurrentUserIdAsync();
        return await _context.FoodbookTemplates
            .Include(t => t.Meals)
            .ThenInclude(m => m.Recipe)
            .FirstOrDefaultAsync(t => t.Id == templateId && (t.UserId == userId || t.IsPublic));
    }

    public async Task<FoodbookTemplate> SaveTemplateFromPlanAsync(Guid planId, string name, string? description, bool isPublic)
    {
        var userId = await GetCurrentUserIdAsync();

        var plan = await _context.Plans.FirstOrDefaultAsync(p => p.Id == planId);
        if (plan is null)
            throw new InvalidOperationException("Plan not found.");

        var plannedMeals = await _context.PlannedMeals
            .Where(pm => pm.PlanId == planId && pm.RecipeId != Guid.Empty)
            .OrderBy(pm => pm.Date)
            .ToListAsync();

        var durationDays = Math.Max((plan.EndDate.Date - plan.StartDate.Date).Days + 1, 1);
        var mealsPerDay = plannedMeals
            .GroupBy(pm => pm.Date.Date)
            .Select(g => g.Count())
            .DefaultIfEmpty(1)
            .Max();

        var template = new FoodbookTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DurationDays = durationDays,
            MealsPerDay = Math.Max(mealsPerDay, 1),
            IsPublic = isPublic,
            CreatedAt = DateTime.UtcNow,
            Meals = plannedMeals
                .GroupBy(pm => pm.Date.Date)
                .SelectMany(dayGroup => dayGroup.Select((meal, index) => new TemplateMeal
                {
                    Id = Guid.NewGuid(),
                    DayOffset = (dayGroup.Key - plan.StartDate.Date).Days,
                    SlotIndex = index,
                    RecipeId = meal.RecipeId,
                    Portions = Math.Max(meal.Portions, 1)
                }))
                .ToList()
        };

        _context.FoodbookTemplates.Add(template);
        await _context.SaveChangesAsync();
        return template;
    }

    public async Task<FoodbookTemplate> UpdateTemplateAsync(FoodbookTemplate template)
    {
        var existing = await _context.FoodbookTemplates
            .Include(t => t.Meals)
            .FirstOrDefaultAsync(t => t.Id == template.Id);

        if (existing is null)
            throw new InvalidOperationException("Template not found.");

        existing.Name = template.Name.Trim();
        existing.Description = string.IsNullOrWhiteSpace(template.Description) ? null : template.Description.Trim();
        existing.IsPublic = template.IsPublic;
        existing.DurationDays = Math.Max(template.DurationDays, 1);
        existing.MealsPerDay = Math.Max(template.MealsPerDay, 1);

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task RemoveTemplateAsync(Guid templateId)
    {
        var template = await _context.FoodbookTemplates.FirstOrDefaultAsync(t => t.Id == templateId);
        if (template is null)
            return;

        _context.FoodbookTemplates.Remove(template);
        await _context.SaveChangesAsync();
    }

    public async Task<Plan> ApplyTemplateAsync(Guid templateId, DateTime startDate, string? planTitle = null)
    {
        var template = await _context.FoodbookTemplates
            .Include(t => t.Meals)
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template is null)
            throw new InvalidOperationException("Template not found.");

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Type = PlanType.Planner,
            StartDate = startDate.Date,
            EndDate = startDate.Date.AddDays(Math.Max(template.DurationDays, 1) - 1),
            Title = string.IsNullOrWhiteSpace(planTitle) ? template.Name : planTitle,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        _context.Plans.Add(plan);

        var meals = template.Meals
            .OrderBy(m => m.DayOffset)
            .ThenBy(m => m.SlotIndex)
            .Select(tm => new PlannedMeal
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                RecipeId = tm.RecipeId,
                Portions = Math.Max(tm.Portions, 1),
                Date = startDate.Date.AddDays(tm.DayOffset),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            })
            .ToList();

        _context.PlannedMeals.AddRange(meals);
        await _context.SaveChangesAsync();

        return plan;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var account = await _accountService.GetActiveAccountAsync();
        return account?.SupabaseUserId ?? "local-user";
    }
}
