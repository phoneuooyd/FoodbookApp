using System.Globalization;
using Microsoft.Maui.Controls;
using Foodbook.Views.Components;

namespace Foodbook.Converters;

public class ItemTypeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FolderPickerItemType itemType)
        {
            return itemType switch
            {
                FolderPickerItemType.Navigation => Application.Current?.RequestedTheme == AppTheme.Dark 
                    ? Color.FromArgb("#3B82F6") : Color.FromArgb("#1D4ED8"),
                FolderPickerItemType.Folder => Application.Current?.RequestedTheme == AppTheme.Dark 
                    ? Color.FromArgb("#F59E0B") : Color.FromArgb("#D97706"),
                FolderPickerItemType.Recipe => Application.Current?.RequestedTheme == AppTheme.Dark 
                    ? Color.FromArgb("#10B981") : Color.FromArgb("#059669"),
                _ => Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray
            };
        }
        
        return Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.LightGray : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}