using System.Globalization;
using Microsoft.Maui.Controls;
using System.Linq;

namespace Foodbook.Converters
{
    public class CountToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
