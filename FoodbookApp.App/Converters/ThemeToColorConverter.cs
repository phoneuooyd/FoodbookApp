using System.Globalization;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.Converters
{
    public class ThemeToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not AppColorTheme colorTheme || parameter is not string colorType)
                return Colors.Transparent;

            try
            {
                var themeService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IThemeService>();
                if (themeService == null)
                    return Colors.Transparent;

                var themeColors = themeService.GetThemeColors(colorTheme);
                
                // Check if current app theme is dark
                var isDarkMode = Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark ||
                               (Application.Current?.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified &&
                                Application.Current?.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark);

                return colorType.ToLower() switch
                {
                    "primary" => isDarkMode ? themeColors.PrimaryDark : themeColors.PrimaryLight,
                    "secondary" => isDarkMode ? themeColors.SecondaryDark : themeColors.SecondaryLight,
                    "tertiary" => isDarkMode ? themeColors.TertiaryDark : themeColors.TertiaryLight,
                    "accent" => isDarkMode ? themeColors.AccentDark : themeColors.AccentLight,
                    _ => Colors.Transparent
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeToColorConverter] Error: {ex.Message}");
                return Colors.Transparent;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}