using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using FoodbookApp.Interfaces;

namespace Foodbook.Converters
{
    // Converter that formats the incoming value using a format string stored in resource files
    public class FormatWithResourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                var key = parameter as string;
                if (string.IsNullOrWhiteSpace(key))
                    return value?.ToString() ?? string.Empty;

                var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService(typeof(ILocalizationService)) as ILocalizationService;
                var format = loc?.GetString("HomePageResources", key) ?? string.Empty;

                if (string.IsNullOrEmpty(format))
                    return value?.ToString() ?? string.Empty;

                // Use invariant culture for numeric formatting unless app culture provided
                return string.Format(culture ?? CultureInfo.CurrentCulture, format, value);
            }
            catch
            {
                return value?.ToString() ?? string.Empty;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
