using System.Globalization;
using System.Resources;

namespace FoodbookApp.Resources
{
    public static class RecipesPage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.RecipesPage", typeof(RecipesPage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string AddRecipe => ResourceManager.GetString("AddRecipe", CultureInfo.CurrentUICulture) ?? "Add recipe";
    }
}