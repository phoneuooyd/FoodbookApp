using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Globalization;

namespace Foodbook.Converters
{
    public class BoolToGrayConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isChecked)
            {
                if (isChecked)
                {
                    // Checked items: lighter gray
                    return Application.Current?.RequestedTheme == AppTheme.Dark 
                        ? Color.FromArgb("#9E9E9E") // Gray400 dark
                        : Color.FromArgb("#757575"); // Gray500 light
                }
                else
                {
                    // Unchecked items: darker gray
                    return Application.Current?.RequestedTheme == AppTheme.Dark 
                        ? Color.FromArgb("#BDBDBD") // Gray400 dark
                        : Color.FromArgb("#757575"); // Gray600 light
                }
            }
            return Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
