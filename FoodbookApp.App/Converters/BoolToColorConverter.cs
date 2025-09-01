using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace Foodbook.Converters;

public class BoolToColorConverter : IValueConverter
{
    private static Color GetAppColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is Color c)
            return c;
        return fallback;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        var param = parameter?.ToString();

        // Resolve dynamic theme colors
        var primary = GetAppColor("Primary", Color.FromArgb("#512BD4"));
        var primaryDark = GetAppColor("PrimaryDark", Color.FromArgb("#ac99ea"));
        var secondary = GetAppColor("Secondary", Color.FromArgb("#DFD8F7"));
        var gray200 = GetAppColor("Gray200", Color.FromArgb("#C8C8C8"));
        var gray300 = GetAppColor("Gray300", Color.FromArgb("#ACACAC"));
        var gray400 = GetAppColor("Gray400", Color.FromArgb("#919191"));
        var gray500 = GetAppColor("Gray500", Color.FromArgb("#6E6E6E"));
        var gray600 = GetAppColor("Gray600", Color.FromArgb("#404040"));

        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        Color primaryActive = dark ? primaryDark : primary;

        return param switch
        {
            // Text color for selected / unselected
            "Text" => isTrue ? primaryActive : (dark ? gray200 : gray500),
            // Font weight
            "Bold" => isTrue ? FontAttributes.Bold : FontAttributes.None,
            // Underline (tab border) color
            "TabBorder" => isTrue ? primaryActive : Colors.Transparent,
            // Tab background (keep transparent)
            "TabBackground" => Colors.Transparent,
            // Segmented button selected background
            "SegmentSelectedBg" => isTrue ? (dark ? primaryDark.WithAlpha(0.25f) : secondary) : Colors.Transparent,
            // Segmented button unselected background
            "SegmentUnselectedBg" => !isTrue ? (dark ? gray600 : gray300) : (dark ? primaryDark.WithAlpha(0.25f) : secondary),
            // Segmented text color
            "SegmentText" => isTrue ? (dark ? primaryDark : primary) : (dark ? gray200 : gray500),
            _ => isTrue ? primaryActive : (dark ? gray600 : gray300)
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DragStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool { } isDragged && isDragged)
        {
            // Light highlight using Secondary color if available
            var secondary = BoolToColorConverter_Get("Secondary", Color.FromArgb("#DFD8F7"));
            return secondary.WithAlpha(0.35f);
        }
        return Colors.Transparent;
    }

    private static Color BoolToColorConverter_Get(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is Color c)
            return c;
        return fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
}

public class DropZoneToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool { } over && over)
        {
            var primary = BoolToColorConverter_Get("Primary", Color.FromArgb("#512BD4"));
            return primary.WithAlpha(0.15f);
        }
        return Colors.Transparent;
    }

    private static Color BoolToColorConverter_Get(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is Color c)
            return c;
        return fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
}

public class DropZoneBorderColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool { } over && over)
        {
            var primary = BoolToColorConverter_Get("Primary", Color.FromArgb("#512BD4"));
            return primary;
        }
        var gray = BoolToColorConverter_Get("Gray200", Color.FromArgb("#C8C8C8"));
        return gray;
    }

    private static Color BoolToColorConverter_Get(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var val) == true && val is Color c)
            return c;
        return fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
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
                return isTrue ? texts[0] : texts[1];
        }
        return value?.ToString() ?? string.Empty;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class IsNotNullOrEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !string.IsNullOrEmpty(value?.ToString());
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}