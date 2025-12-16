using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface IPlanService
{
    Task<List<Plan>> GetPlansAsync();
    Task<List<Plan>> GetArchivedPlansAsync();
    Task<Plan?> GetPlanAsync(int id);
    Task AddPlanAsync(Plan plan);
    Task UpdatePlanAsync(Plan plan); // Dodana brakuj¹ca metoda
    Task RemovePlanAsync(int id);
    Task<bool> HasOverlapAsync(DateTime from, DateTime to, int? ignoreId = null);
}
