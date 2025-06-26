using Foodbook.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Foodbook.Services
{
    public interface IPlannerService
    {
        Task<List<PlannedMeal>> GetPlannedMealsAsync(DateTime from, DateTime to);
        Task<PlannedMeal?> GetPlannedMealAsync(int id);
        Task AddPlannedMealAsync(PlannedMeal meal);
        Task UpdatePlannedMealAsync(PlannedMeal meal);
        Task RemovePlannedMealAsync(int id);
    }
}