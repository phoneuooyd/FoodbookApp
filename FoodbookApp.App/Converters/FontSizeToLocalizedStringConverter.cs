using System.Globalization;
using Foodbook.Models;

namespace Foodbook.Converters
{
    /// <summary>
    /// Converter for AppFontSize enum to localized display name
    /// </summary>
    public class FontSizeToLocalizedStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AppFontSize fontSize)
            {
                return fontSize switch
                {
                    AppFontSize.Small => "Small",
                    AppFontSize.Default => "Default",
                    AppFontSize.Large => "Large", 
                    AppFontSize.ExtraLarge => "Extra Large",
                    _ => fontSize.ToString()
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string localizedName)
            {
                if (localizedName == "Small")
                    return AppFontSize.Small;
                if (localizedName == "Default")
                    return AppFontSize.Default;
                if (localizedName == "Large")
                    return AppFontSize.Large;
                if (localizedName == "Extra Large")
                    return AppFontSize.ExtraLarge;
            }
            
            return AppFontSize.Default; // Default fallback
        }
    }
}