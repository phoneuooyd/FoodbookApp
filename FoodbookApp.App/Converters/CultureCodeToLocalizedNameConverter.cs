using System.Globalization;
using FoodbookApp.Interfaces;

namespace Foodbook.Converters
{
    public class CultureCodeToLocalizedNameConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string cultureCode)
            {
                try
                {
                    var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
                    string Res(string key) => loc?.GetString("SettingsPageResources", key) ?? key;

                    return cultureCode switch
                    {
                        "en" => Res("LanguageEnglish"),
                        "pl-PL" => Res("LanguagePolish"),
                        "de-DE" => Res("LanguageGerman"),
                        "es-ES" => Res("LanguageSpanish"),
                        "fr-FR" => Res("LanguageFrench"),
                        "ko-KR" => Res("LanguageKorean"),
                        _ => cultureCode
                    };
                }
                catch
                {
                    return cultureCode;
                }
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            try
            {
                var loc = FoodbookApp.MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
                string Res(string key) => loc?.GetString("SettingsPageResources", key) ?? key;

                if (value is string localizedName)
                {
                    if (localizedName == Res("LanguageEnglish")) return "en";
                    if (localizedName == Res("LanguagePolish")) return "pl-PL";
                    if (localizedName == Res("LanguageGerman")) return "de-DE";
                    if (localizedName == Res("LanguageSpanish")) return "es-ES";
                    if (localizedName == Res("LanguageFrench")) return "fr-FR";
                    if (localizedName == Res("LanguageKorean")) return "ko-KR";
                }
            }
            catch { }
            
            return "en"; // Default fallback
        }
    }
}