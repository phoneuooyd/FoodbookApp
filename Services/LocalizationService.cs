using System.Globalization;

namespace Foodbook.Services
{
    public interface ILocalizationService
    {
        Task SetLanguageAsync(string languageCode);
        string GetCurrentLanguage();
        List<(string Code, string Name)> GetAvailableLanguages();
    }

    public class LocalizationService : ILocalizationService
    {
        private const string LANGUAGE_KEY = "app_language";

        public async Task SetLanguageAsync(string languageCode)
        {
            try
            {
                // Zapisz wybór w preferencjach
                await SecureStorage.SetAsync(LANGUAGE_KEY, languageCode);
                
                // Ustaw kulturê aplikacji
                var culture = new CultureInfo(languageCode);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                
                System.Diagnostics.Debug.WriteLine($"Language changed to: {languageCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting language: {ex.Message}");
            }
        }

        public string GetCurrentLanguage()
        {
            try
            {
                var savedLanguage = SecureStorage.GetAsync(LANGUAGE_KEY).Result;
                if (!string.IsNullOrEmpty(savedLanguage))
                {
                    return savedLanguage;
                }
            }
            catch
            {
                // Ignore errors and fall back to system language
            }

            // Fallback to system language
            var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return systemLanguage switch
            {
                "pl" => "pl-PL",
                "en" => "en-US",
                _ => "en-US"
            };
        }

        public List<(string Code, string Name)> GetAvailableLanguages()
        {
            return new List<(string Code, string Name)>
            {
                ("pl-PL", "Polski"),
                ("en-US", "English")
            };
        }
    }
}