using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services
{
    public class PlannerService : IPlannerService
    {
        private readonly AppDbContext _context;

        public PlannerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PlannedMeal>> GetPlannedMealsAsync(DateTime from, DateTime to)
        {
            return await _context.PlannedMeals
                .Include(pm => pm.Recipe)
                .Where(pm => pm.Date >= from && pm.Date <= to)
                .ToListAsync();
        }

        public async Task AddPlannedMealAsync(PlannedMeal meal)
        {
            _context.PlannedMeals.Add(meal);
            await _context.SaveChangesAsync();
        }

        public async Task RemovePlannedMealAsync(int id)
        {
            var meal = await _context.PlannedMeals.FindAsync(id);
            if (meal != null)
            {
                _context.PlannedMeals.Remove(meal);
                await _context.SaveChangesAsync();
            }
        }
    }
}