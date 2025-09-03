using System.Globalization;
using Microsoft.Maui.Controls;
using Foodbook.Models;
using FoodbookApp.Localization;

namespace Foodbook.Converters
{
    /// <summary>
    /// Converter that translates AppFontFamily enum values to localized display strings
    /// </summary>
    public class FontFamilyToLocalizedStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is AppFontFamily fontFamily)
            {
                return fontFamily switch
                {
                    // Note: Using hardcoded strings instead of resource keys since they don't have localized versions
                    // The resource keys are for basic display only, custom fonts use their names
                    AppFontFamily.Default => "System Default",
                    AppFontFamily.SansSerif => "Sans-serif",
                    AppFontFamily.Serif => "Serif",
                    AppFontFamily.Monospace => "Monospace",
                    
                    // Custom fonts - use their display names
                    AppFontFamily.OpenSansRegular => "OpenSans Regular",
                    AppFontFamily.OpenSansSemibold => "OpenSans Semibold",
                    AppFontFamily.BarlowCondensed => "Barlow Condensed",
                    AppFontFamily.BarlowCondensedLight => "Barlow Condensed Light",
                    AppFontFamily.BarlowCondensedMedium => "Barlow Condensed Medium",
                    AppFontFamily.BarlowCondensedSemibold => "Barlow Condensed Semibold",
                    AppFontFamily.CherryBombOne => "Cherry Bomb One",
                    AppFontFamily.DynaPuff => "DynaPuff",
                    AppFontFamily.DynaPuffMedium => "DynaPuff Medium",
                    AppFontFamily.DynaPuffSemibold => "DynaPuff Semibold",
                    AppFontFamily.Gruppo => "Gruppo",
                    AppFontFamily.JustMeAgainDownHere => "Just Me Again Down Here",
                    AppFontFamily.Kalam => "Kalam",
                    AppFontFamily.PoiretOne => "Poiret One",
                    AppFontFamily.SendFlowers => "Send Flowers",
                    AppFontFamily.Slabo27px => "Slabo 27px",
                    AppFontFamily.Yellowtail => "Yellowtail",
                    _ => "Unknown"
                };
            }
            
            return "Unknown";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for FontFamilyToLocalizedStringConverter");
        }
    }
}