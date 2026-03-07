using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services;

public class FoodbookTemplateService : IFoodbookTemplateService
{
    private readonly AppDbContext _context;
    private readonly IPlannerService _plannerService;
    private readonly IPlanService _planService;

    public FoodbookTemplateService(AppDbContext context, IPlannerService plannerService, IPlanService planService)
    {
        _context = context;
        _plannerService = plannerService;
        _planService = planService;
    }

    public async Task<List<FoodbookTemplate>> GetTemplatesAsync(string userId, bool includePublic = true)
    {
        var query = _context.FoodbookTemplates
            .Include(t => t.Meals)
            .AsQueryable();

        if (includePublic)
        {
            query = query.Where(t => t.UserId == userId || t.IsPublic);
        }
        else
        {
            query = query.Where(t => t.UserId == userId);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<FoodbookTemplate?> GetTemplateWithMealsAsync(Guid templateId)
    {
        return await _context.FoodbookTemplates
            .Include(t => t.Meals)
            .ThenInclude(m => m.Recipe)
            .FirstOrDefaultAsync(t => t.Id == templateId);
    }

    public async Task<FoodbookTemplate> CreateTemplateFromPlanAsync(
        string userId,
        string name,
        string? description,
        DateTime startDate,
        int mealsPerDay,
        IReadOnlyCollection<TemplateMeal> meals,
        bool isPublic)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required", nameof(name));

        if (meals.Count == 0)
            throw new ArgumentException("Template must contain at least one meal", nameof(meals));

        var maxDayOffset = meals.Max(m => m.DayOffset);
        var template = new FoodbookTemplate
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DurationDays = Math.Max(1, maxDayOffset + 1),
            MealsPerDay = Math.Max(1, mealsPerDay),
            IsPublic = isPublic,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var meal in meals)
        {
            template.Meals.Add(new TemplateMeal
            {
                Id = Guid.NewGuid(),
                FoodbookTemplateId = template.Id,
                DayOffset = meal.DayOffset,
                SlotIndex = meal.SlotIndex,
                RecipeId = meal.RecipeId,
                Portions = Math.Max(1, meal.Portions)
            });
        }

        _context.FoodbookTemplates.Add(template);
        await _context.SaveChangesAsync();

        return template;
    }

    public async Task<Plan?> ApplyTemplateAsync(Guid templateId, DateTime startDate, string? planTitle = null)
    {
        var template = await GetTemplateWithMealsAsync(templateId);
        if (template == null)
            return null;

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            StartDate = startDate.Date,
            EndDate = startDate.Date.AddDays(template.DurationDays - 1),
            Type = PlanType.Planner,
            PlannerName = string.IsNullOrWhiteSpace(planTitle) ? template.Name : planTitle.Trim()
        };

        await _planService.AddPlanAsync(plan);

        foreach (var meal in template.Meals)
        {
            var plannedDate = startDate.Date.AddDays(meal.DayOffset);
            await _plannerService.AddPlannedMealAsync(new PlannedMeal
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                Date = plannedDate,
                RecipeId = meal.RecipeId,
                Portions = Math.Max(1, meal.Portions)
            });
        }

        return plan;
    }

    public async Task UpdateTemplateMetadataAsync(Guid templateId, string name, string? description, bool isPublic)
    {
        var template = await _context.FoodbookTemplates.FindAsync(templateId);
        if (template == null)
            return;

        template.Name = name.Trim();
        template.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        template.IsPublic = isPublic;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteTemplateAsync(Guid templateId)
    {
        var entity = await _context.FoodbookTemplates.FindAsync(templateId);
        if (entity == null)
            return;

        _context.FoodbookTemplates.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
