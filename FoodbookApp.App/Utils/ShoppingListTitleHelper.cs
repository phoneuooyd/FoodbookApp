using System.Globalization;
using FoodbookApp.Localization;

namespace FoodbookApp.Utils;

public static class ShoppingListTitleHelper
{
    public static string BuildDefaultTitle(DateTime startDate, DateTime endDate)
    {
        var culture = ShoppingListPageResources.Culture ?? CultureInfo.CurrentUICulture;
        var dateRange = startDate.Date == endDate.Date
            ? startDate.ToString("d", culture)
            : string.Format(culture, "{0:d} - {1:d}", startDate, endDate);

        var format = ShoppingListPageResources.ResourceManager.GetString(
            "CreateListTitleFormat",
            ShoppingListPageResources.Culture) ?? "Shopping List ({0})";

        return string.Format(culture, format, dateRange);
    }
}
