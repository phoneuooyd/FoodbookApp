using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IPlanService
{
    Task<List<Plan>> GetPlansAsync();
    Task<List<Plan>> GetArchivedPlansAsync();
    Task<Plan?> GetPlanAsync(Guid id);
    Task<Plan?> GetArchivedPlanWithMealsAsync(Guid planId);
    Task AddPlanAsync(Plan plan);
    Task UpdatePlanAsync(Plan plan);
    Task RemovePlanAsync(Guid id);
    Task<bool> HasOverlapAsync(DateTime from, DateTime to, Guid? ignoreId = null);
}
