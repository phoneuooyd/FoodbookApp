using System.Globalization;
using Foodbook.Models;

namespace Foodbook.Converters
{
    /// <summary>
    /// Converter for AppFontFamily enum to localized display name
    /// </summary>
    public class FontFamilyToLocalizedStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AppFontFamily fontFamily)
            {
                return fontFamily switch
                {
                    AppFontFamily.Default => "System Default",
                    AppFontFamily.SansSerif => "Sans-serif", 
                    AppFontFamily.Serif => "Serif",
                    AppFontFamily.Monospace => "Monospace",
                    _ => fontFamily.ToString()
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string localizedName)
            {
                if (localizedName == "System Default")
                    return AppFontFamily.Default;
                if (localizedName == "Sans-serif")
                    return AppFontFamily.SansSerif;
                if (localizedName == "Serif")
                    return AppFontFamily.Serif;
                if (localizedName == "Monospace")
                    return AppFontFamily.Monospace;
            }
            
            return AppFontFamily.Default; // Default fallback
        }
    }
}