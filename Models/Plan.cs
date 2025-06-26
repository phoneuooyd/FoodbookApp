using System;

namespace Foodbook.Models
{
    public class Plan
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Label => $"Foodbook {StartDate:yyyy-MM-dd} do {EndDate:yyyy-MM-dd}";
    }
}
