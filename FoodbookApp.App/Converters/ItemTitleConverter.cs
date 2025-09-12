using System.Globalization;
using Microsoft.Maui.Controls;
using Foodbook.Models;

namespace Foodbook.Converters
{
    /// <summary>
    /// Returns a display title for different list item types.
    /// - Recipe/Ingredient: Name
    /// - Plan: formatted date range (StartDate – EndDate)
    /// </summary>
    public sealed class ItemTitleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            switch (value)
            {
                case Recipe r:
                    return r.Name;
                case Ingredient i:
                    return i.Name;
                case Plan p:
                    // Localized short date format
                    var start = p.StartDate.ToString("d", culture);
                    var end = p.EndDate.ToString("d", culture);
                    return $"{start} – {end}";
                default:
                    return value?.ToString() ?? string.Empty;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
