using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Foodbook.Converters;

public sealed class CultureCodeToDisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var code = value as string;
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;

        try
        {
            var ci = code.Length == 2 ? new CultureInfo(code) : new CultureInfo(code);
            return ci.EnglishName; // "German (Germany)"
        }
        catch
        {
            return code switch
            {
                "en" => "English",
                "pl-PL" => "Polish",
                "de-DE" => "German",
                "es-ES" => "Spanish",
                "fr-FR" => "French",
                _ => code
            };
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
