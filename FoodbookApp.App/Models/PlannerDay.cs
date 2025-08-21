using System;
using System.Collections.ObjectModel;

namespace Foodbook.Models
{
    public class PlannerDay
    {
        public DateTime Date { get; }
        public ObservableCollection<PlannedMeal> Meals { get; } = new();

        public PlannerDay(DateTime date)
        {
            Date = date;
        }
    }
}
