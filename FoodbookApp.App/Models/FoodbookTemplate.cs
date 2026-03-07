using System.ComponentModel.DataAnnotations;

namespace Foodbook.Models;

public class FoodbookTemplate
{
    public Guid Id { get; set; }

    [MaxLength(128)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DurationDays { get; set; }

    public int MealsPerDay { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsPublic { get; set; }

    public ICollection<TemplateMeal> Meals { get; set; } = new List<TemplateMeal>();
}
