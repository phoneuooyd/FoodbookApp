using System.Globalization;
using Foodbook.Models;

namespace Foodbook.Converters;

/// <summary>
/// Converter that returns Primary color when recipe is selected (not null), otherwise returns LightGray
/// </summary>
public class RecipeSelectedToStrokeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If Recipe is not null, use Primary color from DynamicResource, otherwise use default border color
        if (value is Recipe recipe && recipe != null)
        {
            // Return the Primary color from app resources using dynamic resource
            if (Application.Current?.Resources?.TryGetValue("Primary", out var primary) ?? false)
            {
                return primary;
            }
            // Fallback to a standard accent-like color
            return Color.FromArgb("#3498db");
        }
        
        return Colors.LightGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
