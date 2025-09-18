using System.Globalization;
using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace Foodbook.Converters
{
    public class AppColorThemeToLocalizedStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not AppColorTheme colorTheme)
                return string.Empty;

            try
            {
                var localizationService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
                if (localizationService == null)
                    return colorTheme.ToString();

                var key = $"ColorTheme{colorTheme}";
                var localizedValue = localizationService.GetString("SettingsPageResources", key);
                
                return !string.IsNullOrEmpty(localizedValue) ? localizedValue : colorTheme.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppColorThemeToLocalizedStringConverter] Error: {ex.Message}");
                return colorTheme.ToString();
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}