using System.Globalization;
using Foodbook.Models;
using FoodbookApp.Localization;

namespace Foodbook.Converters
{
    public class AppThemeToLocalizedStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Foodbook.Models.AppTheme theme)
            {
                return theme switch
                {
                    Foodbook.Models.AppTheme.Light => SettingsPageResources.ThemeLight,
                    Foodbook.Models.AppTheme.Dark => SettingsPageResources.ThemeDark,
                    Foodbook.Models.AppTheme.System => SettingsPageResources.ThemeSystem,
                    _ => theme.ToString()
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string localizedName)
            {
                if (localizedName == SettingsPageResources.ThemeLight)
                    return Foodbook.Models.AppTheme.Light;
                if (localizedName == SettingsPageResources.ThemeDark)
                    return Foodbook.Models.AppTheme.Dark;
                if (localizedName == SettingsPageResources.ThemeSystem)
                    return Foodbook.Models.AppTheme.System;
            }
            
            return Foodbook.Models.AppTheme.System; // Default fallback
        }
    }
}