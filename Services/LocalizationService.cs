using System.Globalization;

namespace Foodbook.Services
{
    public interface ILocalizationService
    {
        Task SetLanguageAsync(string languageCode);
        string GetCurrentLanguage();
        List<(string Code, string Name)> GetAvailableLanguages();
        void InitializeLanguage();
    }

    public class LocalizationService : ILocalizationService
    {
        private const string LANGUAGE_KEY = "app_language";

        public async Task SetLanguageAsync(string languageCode)
        {
            try
            {
                // Save preference
                await SecureStorage.SetAsync(LANGUAGE_KEY, languageCode);
                
                // Set application culture immediately
                ApplyCulture(languageCode);
                
                System.Diagnostics.Debug.WriteLine($"? Language successfully changed to: {languageCode}");
                System.Diagnostics.Debug.WriteLine($"? Current UI Culture: {CultureInfo.CurrentUICulture.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error setting language: {ex.Message}");
                throw;
            }
        }

        public string GetCurrentLanguage()
        {
            try
            {
                var savedLanguage = SecureStorage.GetAsync(LANGUAGE_KEY).Result;
                if (!string.IsNullOrEmpty(savedLanguage))
                {
                    System.Diagnostics.Debug.WriteLine($"?? Retrieved saved language: {savedLanguage}");
                    return savedLanguage;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"?? Error retrieving saved language: {ex.Message}");
            }

            // Fallback to system language
            var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var fallbackLanguage = systemLanguage switch
            {
                "pl" => "pl-PL",
                "en" => "en-US",
                _ => "en-US"
            };
            
            System.Diagnostics.Debug.WriteLine($"?? Using fallback language: {fallbackLanguage} (system: {systemLanguage})");
            return fallbackLanguage;
        }

        public List<(string Code, string Name)> GetAvailableLanguages()
        {
            var languages = new List<(string Code, string Name)>
            {
                ("pl-PL", "Polski"),
                ("en-US", "English")
            };
            
            System.Diagnostics.Debug.WriteLine($"?? Available languages: {string.Join(", ", languages.Select(l => $"{l.Name}({l.Code})"))}");
            return languages;
        }

        public void InitializeLanguage()
        {
            var currentLanguage = GetCurrentLanguage();
            ApplyCulture(currentLanguage);
            System.Diagnostics.Debug.WriteLine($"?? Language initialized: {currentLanguage}");
        }

        private void ApplyCulture(string languageCode)
        {
            try
            {
                var culture = new CultureInfo(languageCode);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                
                // Also set current thread culture for immediate effect
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                
                System.Diagnostics.Debug.WriteLine($"?? Culture applied: {culture.Name} (Display: {culture.DisplayName})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error applying culture {languageCode}: {ex.Message}");
            }
        }
    }
}