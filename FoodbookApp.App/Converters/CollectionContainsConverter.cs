using Microsoft.Maui.Controls;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Foodbook.Models;

namespace Foodbook.Converters
{
    /// <summary>
    /// Converter that checks if an item is contained in a collection (used for selection state)
    /// </summary>
    public class CollectionContainsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // value should be the item to check
            // parameter should be the collection to check against (via binding)
            if (value == null || parameter == null) return false;
            
            if (parameter is IEnumerable collection)
            {
                if (value is RecipeLabel label)
                {
                    // Check if the label is in the selected collection by comparing IDs
                    return collection.Cast<RecipeLabel>().Any(l => l.Id == label.Id);
                }
                
                // Fallback: direct contains check
                return collection.Cast<object>().Contains(value);
            }
            
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Multi-value converter to determine if a label is selected and return appropriate border color
    /// </summary>
    public class LabelSelectionToBorderConverter : IMultiValueConverter
    {
        public object? Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return Colors.Transparent;

            var label = values[0] as RecipeLabel;
            var selectedLabels = values[1] as ObservableCollection<RecipeLabel>;

            if (label == null || selectedLabels == null) return Colors.Transparent;

            bool isSelected = selectedLabels.Any(l => l.Id == label.Id);

            if (isSelected)
            {
                // Return primary color based on theme
                bool dark = Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark;
                if (dark)
                {
                    return Application.Current?.Resources.TryGetValue("PrimaryDark", out var primaryDark) == true 
                        ? primaryDark : Color.FromArgb("#ac99ea");
                }
                else
                {
                    return Application.Current?.Resources.TryGetValue("Primary", out var primary) == true 
                        ? primary : Color.FromArgb("#512BD4");
                }
            }

            return Colors.Transparent;
        }

        public object?[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}