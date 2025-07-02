using System.Globalization;
using System.Resources;

namespace FoodbookApp.Resources
{
    public static class AddRecipePage
    {
        private static ResourceManager? _resourceManager;
        private static ResourceManager ResourceManager
        {
            get
            {
                if (_resourceManager == null)
                {
                    _resourceManager = new ResourceManager("FoodbookApp.Resources.AddRecipePage", typeof(AddRecipePage).Assembly);
                }
                return _resourceManager;
            }
        }

        public static string AddModeLabel => ResourceManager.GetString("AddModeLabel", CultureInfo.CurrentUICulture) ?? "Recipe entry mode";
        public static string ManualButton => ResourceManager.GetString("ManualButton", CultureInfo.CurrentUICulture) ?? "?? Manual";
        public static string ImportButton => ResourceManager.GetString("ImportButton", CultureInfo.CurrentUICulture) ?? "?? From link";
        public static string ImportHeader => ResourceManager.GetString("ImportHeader", CultureInfo.CurrentUICulture) ?? "Import from the web";
        public static string UrlLabel => ResourceManager.GetString("UrlLabel", CultureInfo.CurrentUICulture) ?? "Recipe URL *";
        public static string UrlPlaceholder => ResourceManager.GetString("UrlPlaceholder", CultureInfo.CurrentUICulture) ?? "Paste recipe link";
        public static string ImportAction => ResourceManager.GetString("ImportAction", CultureInfo.CurrentUICulture) ?? "?? Import recipe";
        public static string BasicInfo => ResourceManager.GetString("BasicInfo", CultureInfo.CurrentUICulture) ?? "Basic information";
        public static string NameLabel => ResourceManager.GetString("NameLabel", CultureInfo.CurrentUICulture) ?? "Recipe name *";
        public static string NamePlaceholder => ResourceManager.GetString("NamePlaceholder", CultureInfo.CurrentUICulture) ?? "Enter recipe name";
        public static string DescriptionLabel => ResourceManager.GetString("DescriptionLabel", CultureInfo.CurrentUICulture) ?? "Description";
        public static string DescriptionPlaceholder => ResourceManager.GetString("DescriptionPlaceholder", CultureInfo.CurrentUICulture) ?? "Enter recipe description";
        public static string DefaultPortionsLabel => ResourceManager.GetString("DefaultPortionsLabel", CultureInfo.CurrentUICulture) ?? "Default servings (shopping list)*";
        public static string Ingredients => ResourceManager.GetString("Ingredients", CultureInfo.CurrentUICulture) ?? "Ingredients";
        public static string IngredientLabel => ResourceManager.GetString("IngredientLabel", CultureInfo.CurrentUICulture) ?? "Ingredient";
        public static string PickIngredient => ResourceManager.GetString("PickIngredient", CultureInfo.CurrentUICulture) ?? "Select ingredient";
        public static string QuantityLabel => ResourceManager.GetString("QuantityLabel", CultureInfo.CurrentUICulture) ?? "Quantity";
        public static string UnitLabel => ResourceManager.GetString("UnitLabel", CultureInfo.CurrentUICulture) ?? "Unit";
        public static string AddIngredientButton => ResourceManager.GetString("AddIngredientButton", CultureInfo.CurrentUICulture) ?? "? Add ingredient";
        public static string NutritionHeader => ResourceManager.GetString("NutritionHeader", CultureInfo.CurrentUICulture) ?? "Nutrition (per recipe)";
        public static string CalcMethodLabel => ResourceManager.GetString("CalcMethodLabel", CultureInfo.CurrentUICulture) ?? "Nutrition calculation method";
        public static string CalcAuto => ResourceManager.GetString("CalcAuto", CultureInfo.CurrentUICulture) ?? "?? Automatic";
        public static string CalcManual => ResourceManager.GetString("CalcManual", CultureInfo.CurrentUICulture) ?? "?? Manual";
        public static string AutoInfo => ResourceManager.GetString("AutoInfo", CultureInfo.CurrentUICulture) ?? "Calculated automatically from ingredients:";
        public static string ManualInfo => ResourceManager.GetString("ManualInfo", CultureInfo.CurrentUICulture) ?? "Enter values manually:";
        public static string CopyButton => ResourceManager.GetString("CopyButton", CultureInfo.CurrentUICulture) ?? "?? Copy calculated";
        public static string Calories => ResourceManager.GetString("Calories", CultureInfo.CurrentUICulture) ?? "Calories";
        public static string Protein => ResourceManager.GetString("Protein", CultureInfo.CurrentUICulture) ?? "Protein (g)";
        public static string Fat => ResourceManager.GetString("Fat", CultureInfo.CurrentUICulture) ?? "Fat (g)";
        public static string Carbs => ResourceManager.GetString("Carbs", CultureInfo.CurrentUICulture) ?? "Carbs (g)";
    }
}