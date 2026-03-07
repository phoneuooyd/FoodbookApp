namespace Foodbook.Models;

public class TemplateMeal
{
    public Guid Id { get; set; }

    public Guid FoodbookTemplateId { get; set; }

    public int DayOffset { get; set; }

    public int SlotIndex { get; set; }

    public Guid RecipeId { get; set; }

    public int Portions { get; set; }

    public FoodbookTemplate? FoodbookTemplate { get; set; }
    public Recipe? Recipe { get; set; }
}
