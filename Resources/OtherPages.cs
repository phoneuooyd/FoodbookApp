using System.Globalization;
using System.Resources;

namespace FoodbookApp.Resources
{
    public static class IngredientFormPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.IngredientFormPage", typeof(IngredientFormPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Add Ingredient";
        public static string EditTitle => ResourceManager.GetString("EditTitle", CultureInfo.CurrentUICulture) ?? "Edit Ingredient";
        public static string BasicInfo => ResourceManager.GetString("BasicInfo", CultureInfo.CurrentUICulture) ?? "Basic Information";
        public static string NameLabel => ResourceManager.GetString("NameLabel", CultureInfo.CurrentUICulture) ?? "Ingredient name";
        public static string NamePlaceholder => ResourceManager.GetString("NamePlaceholder", CultureInfo.CurrentUICulture) ?? "Enter ingredient name";
        public static string QuantityLabel => ResourceManager.GetString("QuantityLabel", CultureInfo.CurrentUICulture) ?? "Quantity";
        public static string QuantityPlaceholder => ResourceManager.GetString("QuantityPlaceholder", CultureInfo.CurrentUICulture) ?? "100";
        public static string UnitLabel => ResourceManager.GetString("UnitLabel", CultureInfo.CurrentUICulture) ?? "Unit";
        public static string NutritionHeader => ResourceManager.GetString("NutritionHeader", CultureInfo.CurrentUICulture) ?? "Nutrition values";
        public static string VerifyLabel => ResourceManager.GetString("VerifyLabel", CultureInfo.CurrentUICulture) ?? "Verify data from OpenFoodFacts:";
        public static string VerifyButton => ResourceManager.GetString("VerifyButton", CultureInfo.CurrentUICulture) ?? "?? Verify";
        public static string Calories => ResourceManager.GetString("Calories", CultureInfo.CurrentUICulture) ?? "Calories";
        public static string Protein => ResourceManager.GetString("Protein", CultureInfo.CurrentUICulture) ?? "Protein (g)";
        public static string Fat => ResourceManager.GetString("Fat", CultureInfo.CurrentUICulture) ?? "Fat (g)";
        public static string Carbs => ResourceManager.GetString("Carbs", CultureInfo.CurrentUICulture) ?? "Carbs (g)";
        public static string InfoHeader => ResourceManager.GetString("InfoHeader", CultureInfo.CurrentUICulture) ?? "Info";
    }

    public static class PlannerPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.PlannerPage", typeof(PlannerPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Meal Planner";
        public static string StartDate => ResourceManager.GetString("StartDate", CultureInfo.CurrentUICulture) ?? "Start Date";
        public static string EndDate => ResourceManager.GetString("EndDate", CultureInfo.CurrentUICulture) ?? "End Date";
        public static string MealsPerDay => ResourceManager.GetString("MealsPerDay", CultureInfo.CurrentUICulture) ?? "Meals per day";
        public static string AddMeal => ResourceManager.GetString("AddMeal", CultureInfo.CurrentUICulture) ?? "? Meal";
        public static string Save => ResourceManager.GetString("Save", CultureInfo.CurrentUICulture) ?? "Save";
    }

    public static class ShoppingListPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.ShoppingListPage", typeof(ShoppingListPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Shopping Lists";
        public static string EmptyTitle => ResourceManager.GetString("EmptyTitle", CultureInfo.CurrentUICulture) ?? "No Shopping Lists";
        public static string EmptyHint => ResourceManager.GetString("EmptyHint", CultureInfo.CurrentUICulture) ?? "Create a meal plan first to generate shopping lists automatically.";
        public static string GoToPlanner => ResourceManager.GetString("GoToPlanner", CultureInfo.CurrentUICulture) ?? "Go to Planner";
    }

    public static class ShoppingListDetailPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.ShoppingListDetailPage", typeof(ShoppingListDetailPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Shopping List";
        public static string IngredientName => ResourceManager.GetString("IngredientName", CultureInfo.CurrentUICulture) ?? "Ingredient";
        public static string Quantity => ResourceManager.GetString("Quantity", CultureInfo.CurrentUICulture) ?? "Quantity";
        public static string Unit => ResourceManager.GetString("Unit", CultureInfo.CurrentUICulture) ?? "Unit";
        public static string Actions => ResourceManager.GetString("Actions", CultureInfo.CurrentUICulture) ?? "Actions";
        public static string NamePlaceholder => ResourceManager.GetString("NamePlaceholder", CultureInfo.CurrentUICulture) ?? "Name";
        public static string AddItem => ResourceManager.GetString("AddItem", CultureInfo.CurrentUICulture) ?? "Add ingredient";
    }

    public static class ArchivePage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.ArchivePage", typeof(ArchivePage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Archive";
        public static string NoArchived => ResourceManager.GetString("NoArchived", CultureInfo.CurrentUICulture) ?? "No archived items";
    }

    public static class SettingsPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.SettingsPage", typeof(SettingsPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Settings";
        public static string Subtitle => ResourceManager.GetString("Subtitle", CultureInfo.CurrentUICulture) ?? "Configure your app preferences";
        public static string LanguageSection => ResourceManager.GetString("LanguageSection", CultureInfo.CurrentUICulture) ?? "Language & Region";
        public static string LanguageLabel => ResourceManager.GetString("LanguageLabel", CultureInfo.CurrentUICulture) ?? "Language";
        public static string LanguageDescription => ResourceManager.GetString("LanguageDescription", CultureInfo.CurrentUICulture) ?? "Choose your preferred language";
        public static string AppSection => ResourceManager.GetString("AppSection", CultureInfo.CurrentUICulture) ?? "Application";
        public static string ThemeLabel => ResourceManager.GetString("ThemeLabel", CultureInfo.CurrentUICulture) ?? "Theme";
        public static string ThemeDescription => ResourceManager.GetString("ThemeDescription", CultureInfo.CurrentUICulture) ?? "Light or dark mode";
        public static string NotificationsLabel => ResourceManager.GetString("NotificationsLabel", CultureInfo.CurrentUICulture) ?? "Notifications";
        public static string NotificationsDescription => ResourceManager.GetString("NotificationsDescription", CultureInfo.CurrentUICulture) ?? "Meal and shopping reminders";
        public static string AboutSection => ResourceManager.GetString("AboutSection", CultureInfo.CurrentUICulture) ?? "About";
        public static string AppName => ResourceManager.GetString("AppName", CultureInfo.CurrentUICulture) ?? "Foodbook App";
        public static string Version => ResourceManager.GetString("Version", CultureInfo.CurrentUICulture) ?? "Version 1.0.0";
        public static string Description => ResourceManager.GetString("Description", CultureInfo.CurrentUICulture) ?? "Your personal culinary assistant for managing recipes, meal planning, and shopping lists.";
        public static string ComingSoon => ResourceManager.GetString("ComingSoon", CultureInfo.CurrentUICulture) ?? "Coming soon";
    }
}