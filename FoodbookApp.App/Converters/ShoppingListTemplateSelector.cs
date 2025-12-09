using Foodbook.Models;
using Foodbook.ViewModels;
using Sharpnado.CollectionView;

namespace Foodbook.Converters
{
    /// <summary>
    /// DataTemplateSelector for shopping list items with Sharpnado CollectionView.
    /// Uses SizedDataTemplate for headers to enable mixed-height items.
    /// </summary>
    public class ShoppingListTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Template for group headers (To Buy / Collected) - uses SizedDataTemplate
        /// </summary>
        public SizedDataTemplate? HeaderTemplate { get; set; }

        /// <summary>
        /// Template for regular shopping list items (Ingredient)
        /// </summary>
        public DataTemplate? ItemTemplate { get; set; }

        protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
        {
            // Use pattern matching to determine which template to use
            if (item is ShoppingListHeader)
            {
                return HeaderTemplate;
            }
            
            // Default to item template for Ingredient and any other types
            return ItemTemplate;
        }
    }
}
