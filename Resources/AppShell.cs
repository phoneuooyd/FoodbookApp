using System.Globalization;
using System.Resources;

namespace FoodbookApp.Resources
{
    public static class AppShell
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.AppShell", typeof(AppShell).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string IngredientsTab => ResourceManager.GetString("IngredientsTab", CultureInfo.CurrentUICulture) ?? "Ingredients";
        public static string RecipesTab => ResourceManager.GetString("RecipesTab", CultureInfo.CurrentUICulture) ?? "Recipes";
        public static string HomeTab => ResourceManager.GetString("HomeTab", CultureInfo.CurrentUICulture) ?? "Home";
        public static string PlannerTab => ResourceManager.GetString("PlannerTab", CultureInfo.CurrentUICulture) ?? "Planner";
        public static string ShoppingTab => ResourceManager.GetString("ShoppingTab", CultureInfo.CurrentUICulture) ?? "Shopping lists";
    }
}