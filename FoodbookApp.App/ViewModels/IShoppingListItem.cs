namespace Foodbook.ViewModels
{
    /// <summary>
    /// Marker interface for items in the shopping list flat collection.
    /// Implemented by both Ingredient (actual items) and ShoppingGroupHeader (group headers).
    /// </summary>
    public interface IShoppingListItem
    {
        /// <summary>
        /// Indicates whether this item is a group header (true) or an actual ingredient (false).
        /// </summary>
        bool IsHeader { get; }
    }
}
