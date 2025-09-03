using System.Globalization;
using FoodbookApp.Localization;

namespace Foodbook.Converters
{
    public class CultureCodeToLocalizedNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string cultureCode)
            {
                return cultureCode switch
                {
                    "en" => SettingsPageResources.LanguageEnglish,
                    "pl-PL" => SettingsPageResources.LanguagePolish,
                    _ => cultureCode
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string localizedName)
            {
                if (localizedName == SettingsPageResources.LanguageEnglish)
                    return "en";
                if (localizedName == SettingsPageResources.LanguagePolish)
                    return "pl-PL";
            }
            
            return "en"; // Default fallback
        }
    }
}