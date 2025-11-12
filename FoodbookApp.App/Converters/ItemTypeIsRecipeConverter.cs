using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Foodbook.Views.Components;

namespace Foodbook.Converters
{
    public class ItemTypeIsRecipeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is FolderPickerItemType type)
                {
                    return type == FolderPickerItemType.Recipe;
                }
                // Fallback for string values
                if (value is string s)
                {
                    return string.Equals(s, nameof(FolderPickerItemType.Recipe), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
