using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Foodbook.Models;

namespace Foodbook.ViewModels
{
    /// <summary>
    /// Group wrapper for shopping list items (unchecked/checked sections)
    /// </summary>
    public class ShoppingItemGroup : ObservableCollection<Ingredient>
    {
        public string GroupName { get; }
        public bool IsCheckedGroup { get; }
        public Color GroupHeaderColor { get; }
        
        // ? NEW: Property to hide group header when empty
        public bool IsVisible => Count > 0;

        public ShoppingItemGroup(string groupName, bool isCheckedGroup, ObservableCollection<Ingredient> source)
        {
            GroupName = groupName;
            IsCheckedGroup = isCheckedGroup;
            
            // Use Primary color for unchecked section, gray for checked section
            GroupHeaderColor = isCheckedGroup 
                ? (Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark 
                    ? Color.FromArgb("#9E9E9E") // Gray400 dark
                    : Color.FromArgb("#757575")) // Gray500 light
                : (Color)(Application.Current?.Resources["Primary"] ?? Colors.Blue);

            // Mirror source collection
            foreach (var item in source)
                Add(item);

            source.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                        if (e.NewItems != null)
                        {
                            int idx = e.NewStartingIndex >= 0 ? e.NewStartingIndex : Count;
                            foreach (Ingredient item in e.NewItems)
                            {
                                if (idx >= 0 && idx <= Count)
                                    Insert(idx++, item);
                                else
                                    Add(item);
                            }
                        }
                        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsVisible)));
                        break;

                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        if (e.OldItems != null)
                        {
                            foreach (Ingredient item in e.OldItems)
                                Remove(item);
                        }
                        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsVisible)));
                        break;

                    case System.Collections.Specialized.NotifyCollectionChangedAction.Replace:
                        if (e.OldStartingIndex >= 0 && e.OldStartingIndex < Count)
                            RemoveAt(e.OldStartingIndex);
                        if (e.NewItems != null && e.NewStartingIndex >= 0)
                        {
                            foreach (Ingredient item in e.NewItems)
                                Insert(e.NewStartingIndex, item);
                        }
                        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsVisible)));
                        break;

                    case System.Collections.Specialized.NotifyCollectionChangedAction.Move:
                        if (e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0 && e.OldStartingIndex < Count)
                        {
                            var item = this[e.OldStartingIndex];
                            RemoveAt(e.OldStartingIndex);
                            Insert(e.NewStartingIndex, item);
                        }
                        break;

                    case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                        Clear();
                        foreach (var item in source)
                            Add(item);
                        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(IsVisible)));
                        break;
                }
            };
        }
    }
}
