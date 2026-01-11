using Foodbook.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FoodbookApp.Interfaces
{
    public interface IPlannerService
    {
        // Date range-based access (legacy)
        Task<List<PlannedMeal>> GetPlannedMealsAsync(DateTime from, DateTime to);
        // New: plan-based access to support multiple planners simultaneously
        Task<List<PlannedMeal>> GetPlannedMealsAsync(Guid planId);
        Task<PlannedMeal?> GetPlannedMealAsync(Guid id);
        Task AddPlannedMealAsync(PlannedMeal meal);
        Task UpdatePlannedMealAsync(PlannedMeal meal);
        Task RemovePlannedMealAsync(Guid id);
    }
}