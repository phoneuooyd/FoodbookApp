using System.Globalization;
using Foodbook.Models;

namespace Foodbook.Converters;

/// <summary>
/// Converter that returns 3px when recipe is selected (not null), otherwise returns 1px
/// </summary>
public class RecipeSelectedToStrokeThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // If Recipe is not null, use 3px thickness, otherwise use 1px
        if (value is Recipe recipe && recipe != null)
        {
            return 3.0;
        }
        
        return 1.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
