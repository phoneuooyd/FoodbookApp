using System;

namespace Foodbook.Models
{
    public class Plan
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsArchived { get; set; } = false; 
        public string Label => "Lista zakupów";

    }
}
