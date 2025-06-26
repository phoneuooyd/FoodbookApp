using Foodbook.Models;

namespace Foodbook.Services;

public interface IPlanService
{
    Task<List<Plan>> GetPlansAsync();
    Task<Plan?> GetPlanAsync(int id);
    Task AddPlanAsync(Plan plan);
    Task RemovePlanAsync(int id);
    Task<bool> HasOverlapAsync(DateTime from, DateTime to, int? ignoreId = null);
}
