using System.Globalization;

namespace Foodbook.Services;

/// <summary>
/// Utility class for safe parsing and normalizing numeric values.
/// Handles both comma and dot as decimal separators, always uses invariant culture for storage.
/// </summary>
public static class NumberNormalizer
{
    /// <summary>
    /// Parses a double value from string, handling both comma and dot decimal separators.
    /// Always uses InvariantCulture for reliable parsing.
    /// </summary>
    public static double ParseDouble(string value, double defaultValue = 0.0)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            // Normalize: both comma and dot are treated as decimal separator
            string normalizedValue = value.Trim().Replace(',', '.');
            
            return double.TryParse(
                normalizedValue,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var result) ? result : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Formats a double value using InvariantCulture (always uses '.' as decimal separator).
    /// This ensures consistency across all locales.
    /// </summary>
    public static string FormatDouble(double value, string format = "F2")
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Displays a double value with user's locale (comma or dot depending on system settings).
    /// Use this for UI display only, NOT for storage.
    /// </summary>
    public static string DisplayDouble(double value, string format = "F2")
    {
        return value.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Validates if a string represents a valid double number (accepts both comma and dot).
    /// </summary>
    public static bool IsValidDouble(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string normalizedValue = value.Trim().Replace(',', '.');
            return double.TryParse(
                normalizedValue,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalizes a numeric string by parsing and reformatting to InvariantCulture.
    /// Converts "100,5" ? "100.50", "1.234,56" ? "1234.56", etc.
    /// </summary>
    public static string NormalizeNumericString(string value, string format = "F2")
    {
        double parsed = ParseDouble(value);
        return FormatDouble(parsed, format);
    }
}
