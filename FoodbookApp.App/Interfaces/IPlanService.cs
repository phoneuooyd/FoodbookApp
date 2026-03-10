using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IPlanService
{
    Task<List<Plan>> GetPlansAsync();
    Task<List<Plan>> GetFoodbooksAsync();
    Task<List<Plan>> GetArchivedPlansAsync();
    Task<Plan?> GetPlanAsync(Guid id);
    Task AddPlanAsync(Plan plan);
    Task UpdatePlanAsync(Plan plan);
    Task RemovePlanAsync(Guid id);
    Task<Plan> ApplyFoodbookAsync(Guid foodbookId, DateTime startDate);
    Task<bool> HasOverlapAsync(DateTime from, DateTime to, Guid? ignoreId = null);
}
