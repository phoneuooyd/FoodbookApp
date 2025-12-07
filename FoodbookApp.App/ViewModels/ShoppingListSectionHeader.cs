using System.ComponentModel;
using Microsoft.Maui.Graphics;

namespace Foodbook.ViewModels;

/// <summary>
/// Represents a section header in the shopping list (e.g., "To Buy" or "Collected")
/// Used with Sharpnado CollectionView headers via DataTemplateSelector
/// </summary>
public class ShoppingListSectionHeader : IShoppingListItem, INotifyPropertyChanged
{
    public string Title { get; }
    public bool IsCollectedSection { get; }
    
    private int _itemCount;
    public int ItemCount
    {
        get => _itemCount;
        set
        {
            if (_itemCount != value)
            {
                _itemCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemCount)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }
    }
    
    /// <summary>
    /// Hide header when there are no items in the section
    /// </summary>
    public bool IsVisible => ItemCount > 0;
    
    /// <summary>
    /// Header color - Primary for "To Buy", Gray for "Collected"
    /// </summary>
    public Color HeaderColor { get; }

    public ShoppingListSectionHeader(string title, bool isCollectedSection)
    {
        Title = title;
        IsCollectedSection = isCollectedSection;
        
        HeaderColor = isCollectedSection 
            ? (Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark 
                ? Color.FromArgb("#9E9E9E")
                : Color.FromArgb("#757575"))
            : (Color)(Application.Current?.Resources["Primary"] ?? Colors.Blue);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
