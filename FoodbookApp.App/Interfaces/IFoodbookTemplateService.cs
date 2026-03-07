using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IFoodbookTemplateService
{
    Task<List<FoodbookTemplate>> GetTemplatesAsync(bool includePublic = true);
    Task<FoodbookTemplate?> GetTemplateAsync(Guid templateId);
    Task<FoodbookTemplate> SaveTemplateFromPlanAsync(Guid planId, string name, string? description, bool isPublic);
    Task<FoodbookTemplate> UpdateTemplateAsync(FoodbookTemplate template);
    Task RemoveTemplateAsync(Guid templateId);
    Task<Plan> ApplyTemplateAsync(Guid templateId, DateTime startDate, string? planTitle = null);
}
