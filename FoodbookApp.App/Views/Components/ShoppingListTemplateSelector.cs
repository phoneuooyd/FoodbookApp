using Microsoft.Maui.Controls;
using Foodbook.Models;
using Foodbook.ViewModels;

namespace Foodbook.Views.Components;

/// <summary>
/// DataTemplateSelector for shopping list items - distinguishes between section headers and ingredient items
/// </summary>
public class ShoppingListTemplateSelector : DataTemplateSelector
{
    public DataTemplate? HeaderTemplate { get; set; }
    public DataTemplate? ItemTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            ShoppingListSectionHeader => HeaderTemplate,
            Ingredient => ItemTemplate,
            _ => ItemTemplate
        };
    }
}
