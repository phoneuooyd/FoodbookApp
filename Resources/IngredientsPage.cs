using System.Globalization;
using System.Resources;

namespace FoodbookApp.Resources
{
    public static class IngredientsPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.IngredientsPage", typeof(IngredientsPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string Title => ResourceManager.GetString("Title", CultureInfo.CurrentUICulture) ?? "Ingredients";
        public static string VerifyAll => ResourceManager.GetString("VerifyAll", CultureInfo.CurrentUICulture) ?? "?? Verify all";
        public static string SearchPlaceholder => ResourceManager.GetString("SearchPlaceholder", CultureInfo.CurrentUICulture) ?? "Search ingredients...";
        public static string AddIngredient => ResourceManager.GetString("AddIngredient", CultureInfo.CurrentUICulture) ?? "? Add ingredient";
        public static string Verify => ResourceManager.GetString("Verify", CultureInfo.CurrentUICulture) ?? "?? Verify";
        public static string BulkHint => ResourceManager.GetString("BulkHint", CultureInfo.CurrentUICulture) ?? "Use 'Verify' to update nutrition from OpenFoodFacts";
        public static string NoIngredients => ResourceManager.GetString("NoIngredients", CultureInfo.CurrentUICulture) ?? "No ingredients";
        public static string TapToAdd => ResourceManager.GetString("TapToAdd", CultureInfo.CurrentUICulture) ?? "Tap 'Add ingredient' to add your first ingredient";
    }
}