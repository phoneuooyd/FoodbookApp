using System.Globalization;
using Microsoft.Maui.Controls;
using System.Linq;
using Foodbook.Models;

namespace Foodbook.Converters
{
    public class CountToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int i) return i > 0;
            if (value is long l) return l > 0;
            if (value is IEnumerable<object> e) return e.Any();
            if (value is System.Collections.IEnumerable en)
            {
                foreach (var _ in en) return true;
                return false;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IsNotNullConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value != null;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IsNotNullToColorConverter : IValueConverter
    {
        private static Color GetAppColor(string key, Color fallback)
        {
            if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is Color c)
                return c;
            return fallback;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var hasValue = value != null;
            
            // Get adaptive frame text color for better contrast
            var frameTextColor = GetAppColor("FrameTextColor", Color.FromArgb("#000000"));
            var grayColor = GetAppColor("Gray500", Color.FromArgb("#6E6E6E"));
            
            // Return frame text color if has value, otherwise gray
            return hasValue ? frameTextColor : grayColor;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IsRecipeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Recipe;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class IsFolderConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Folder;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class IsPlanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is Plan;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class FolderRecipeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? FolderTemplate { get; set; }
        public DataTemplate? RecipeTemplate { get; set; }

        protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
        {
            return item switch
            {
                Folder => FolderTemplate,
                Recipe => RecipeTemplate,
                _ => null
            };
        }
    }

    public class UnitToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Unit unit)
            {
                return unit switch
                {
                    Unit.Gram => "g",
                    Unit.Milliliter => "ml", 
                    Unit.Piece => "szt.",
                    _ => value.ToString()
                };
            }
            return value?.ToString() ?? "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "g" => Unit.Gram,
                    "ml" => Unit.Milliliter,
                    "szt." => Unit.Piece,
                    _ => Unit.Gram
                };
            }
            return Unit.Gram;
        }
    }
}
