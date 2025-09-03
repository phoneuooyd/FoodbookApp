using System.Globalization;
using Microsoft.Maui.Controls;
using Foodbook.Models;

namespace Foodbook.Converters
{
    /// <summary>
    /// Converter that translates AppFontSize enum values to localized display strings
    /// </summary>
    public class FontSizeToLocalizedStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AppFontSize fontSize)
            {
                return fontSize switch
                {
                    AppFontSize.Small => "Small (13pt)",
                    AppFontSize.Default => "Default (16pt)",
                    AppFontSize.Large => "Large (20pt)",
                    AppFontSize.ExtraLarge => "Extra Large (24pt)",
                    _ => "Unknown"
                };
            }
            
            return "Unknown";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for FontSizeToLocalizedStringConverter");
        }
    }
}