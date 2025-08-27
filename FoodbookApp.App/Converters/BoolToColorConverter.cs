using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace Foodbook.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            var param = parameter?.ToString();
            
            return param switch
            {
                "Text" => boolValue ? Colors.White : Colors.Black,
                "Bold" => boolValue ? FontAttributes.Bold : FontAttributes.None,
                "Drag" => boolValue ? Color.FromArgb("#bcacdc") : Colors.Transparent,
                _ => boolValue ? Colors.Green : Colors.Gray
            };
        }
        
        var paramDefault = parameter?.ToString();
        return paramDefault switch
        {
            "Text" => Colors.Black,
            "Bold" => FontAttributes.None,
            "Drag" => Colors.Transparent,
            _ => Colors.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DragToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isBeingDragged = (bool?)value;
        return (isBeingDragged ?? false) ? Color.FromArgb("#bcacdc") : Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value?.ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToTextConverter : IValueConverter
{
    public static readonly BoolToTextConverter Instance = new();
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isTrue && parameter is string param)
        {
            var texts = param.Split('|');
            if (texts.Length == 2)
            {
                return isTrue ? texts[0] : texts[1];
            }
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}