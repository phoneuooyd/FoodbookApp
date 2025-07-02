using System.Globalization;
using System.Resources;

namespace FoodbookApp.Resources
{
    public static class HomePage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.HomePage", typeof(HomePage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string WelcomeTitle => ResourceManager.GetString("WelcomeTitle", CultureInfo.CurrentUICulture) ?? "Welcome to Foodbook";
        public static string WelcomeSubtitle => ResourceManager.GetString("WelcomeSubtitle", CultureInfo.CurrentUICulture) ?? "Your personal culinary assistant";
        public static string Recipes => ResourceManager.GetString("Recipes", CultureInfo.CurrentUICulture) ?? "Recipes";
        public static string SavedRecipes => ResourceManager.GetString("SavedRecipes", CultureInfo.CurrentUICulture) ?? "Saved recipes";
        public static string Plans => ResourceManager.GetString("Plans", CultureInfo.CurrentUICulture) ?? "Plans";
        public static string ActiveLists => ResourceManager.GetString("ActiveLists", CultureInfo.CurrentUICulture) ?? "Active lists";
        public static string Archive => ResourceManager.GetString("Archive", CultureInfo.CurrentUICulture) ?? "Archive";
        public static string Archived => ResourceManager.GetString("Archived", CultureInfo.CurrentUICulture) ?? "Archived";
        public static string Open => ResourceManager.GetString("Open", CultureInfo.CurrentUICulture) ?? "Open";
        public static string Settings => ResourceManager.GetString("Settings", CultureInfo.CurrentUICulture) ?? "Settings";
        public static string ComingSoon => ResourceManager.GetString("ComingSoon", CultureInfo.CurrentUICulture) ?? "Coming soon";
        public static string NutritionStats => ResourceManager.GetString("NutritionStats", CultureInfo.CurrentUICulture) ?? "Diet statistics";
        public static string Calories => ResourceManager.GetString("Calories", CultureInfo.CurrentUICulture) ?? "Calories";
        public static string Protein => ResourceManager.GetString("Protein", CultureInfo.CurrentUICulture) ?? "Protein";
        public static string Carbs => ResourceManager.GetString("Carbs", CultureInfo.CurrentUICulture) ?? "Carbohydrates";
        public static string Info => ResourceManager.GetString("Info", CultureInfo.CurrentUICulture) ?? "Plan meals to see statistics";
        public static string Language => ResourceManager.GetString("Language", CultureInfo.CurrentUICulture) ?? "Language";
        public static string Polish => ResourceManager.GetString("Polish", CultureInfo.CurrentUICulture) ?? "Polish";
        public static string English => ResourceManager.GetString("English", CultureInfo.CurrentUICulture) ?? "English";
    }
}