using System.Globalization;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.Converters
{
    public class AppThemeToLocalizedStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Foodbook.Models.AppTheme theme)
            {
                try
                {
                    var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
                    string L(string key) => loc?.GetString("SettingsPageResources", key) ?? key;

                    return theme switch
                    {
                        Foodbook.Models.AppTheme.Light => L("ThemeLight"),
                        Foodbook.Models.AppTheme.Dark => L("ThemeDark"),
                        Foodbook.Models.AppTheme.System => L("ThemeSystem"),
                        _ => theme.ToString()
                    };
                }
                catch
                {
                    return theme.ToString();
                }
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
                string L(string key) => loc?.GetString("SettingsPageResources", key) ?? key;

                if (value is string localizedName)
                {
                    if (localizedName == L("ThemeLight")) return Foodbook.Models.AppTheme.Light;
                    if (localizedName == L("ThemeDark")) return Foodbook.Models.AppTheme.Dark;
                    if (localizedName == L("ThemeSystem")) return Foodbook.Models.AppTheme.System;
                }
            }
            catch { }
            
            return Foodbook.Models.AppTheme.System; // Default fallback
        }
    }
}