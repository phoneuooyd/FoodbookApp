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
        Task<List<PlannedMeal>> GetPlannedMealsAsync(int planId);
        Task<PlannedMeal?> GetPlannedMealAsync(int id);
        Task AddPlannedMealAsync(PlannedMeal meal);
        Task UpdatePlannedMealAsync(PlannedMeal meal);
        Task RemovePlannedMealAsync(int id);
    }
}