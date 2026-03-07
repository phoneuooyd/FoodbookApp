using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IFoodbookTemplateService
{
    Task<List<FoodbookTemplate>> GetTemplatesAsync(string userId, bool includePublic = true);
    Task<FoodbookTemplate?> GetTemplateWithMealsAsync(Guid templateId);
    Task<FoodbookTemplate> CreateTemplateFromPlanAsync(
        string userId,
        string name,
        string? description,
        DateTime startDate,
        int mealsPerDay,
        IReadOnlyCollection<TemplateMeal> meals,
        bool isPublic);
    Task<Plan?> ApplyTemplateAsync(Guid templateId, DateTime startDate, string? planTitle = null);
    Task UpdateTemplateMetadataAsync(Guid templateId, string name, string? description, bool isPublic);
    Task DeleteTemplateAsync(Guid templateId);
}
