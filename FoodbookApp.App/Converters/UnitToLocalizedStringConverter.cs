using System.Globalization;
using Foodbook.Models;
using FoodbookApp.Localization;

namespace Foodbook.Converters
{
    public class UnitToLocalizedStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Unit unit)
            {
                return unit switch
                {
                    Unit.Gram => UnitResources.Gram,
                    Unit.Milliliter => UnitResources.Milliliter,
                    Unit.Piece => UnitResources.Piece,
                    _ => value.ToString()
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // Convert localized string back to Unit enum
                if (stringValue == UnitResources.Gram)
                    return Unit.Gram;
                if (stringValue == UnitResources.Milliliter)
                    return Unit.Milliliter;
                if (stringValue == UnitResources.Piece)
                    return Unit.Piece;
                
                // Fallback: try to parse as enum
                if (Enum.TryParse<Unit>(stringValue, out Unit result))
                    return result;
            }
            
            return Unit.Piece; // Default fallback
        }
    }
}