using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Foodbook.Converters;

public sealed class CultureCodeToNativeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;

        try
        {
            // Normalize codes like "en" vs "en-US"
            var ci = code.Length == 2 ? new CultureInfo(code) : new CultureInfo(code);
            var native = ci.NativeName;
            // Capitalize first letter for consistency
            return string.IsNullOrWhiteSpace(native) ? code : char.ToUpper(native[0]) + native.Substring(1);
        }
        catch
        {
            // Fallback to a friendly map
            return code switch
            {
                "en" => "English",
                "pl-PL" => "Polski",
                "de-DE" => "Deutsch",
                "es-ES" => "Espanol",
                "fr-FR" => "Francais",
                _ => code
            };
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
