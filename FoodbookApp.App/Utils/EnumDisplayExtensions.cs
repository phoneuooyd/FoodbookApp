using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Resources;
using System.Globalization;

namespace Foodbook.Utils
{
    public static class EnumDisplayExtensions
    {
        public static string GetDisplayName(this Enum value)
        {
            return GetDisplayString(value, preferShort: false);
        }

        public static string GetDisplayShortName(this Enum value)
        {
            return GetDisplayString(value, preferShort: true);
        }

        private static string GetDisplayString(Enum value, bool preferShort)
        {
            try
            {
                var member = value.GetType().GetField(value.ToString());
                if (member == null) return value.ToString();
                var attr = member.GetCustomAttribute<DisplayAttribute>();
                if (attr == null) return value.ToString();

                // Prefer ShortName when requested; fall back to Name
                var key = preferShort ? (attr.ShortName ?? attr.Name) : (attr.Name ?? attr.ShortName);
                if (string.IsNullOrWhiteSpace(key))
                    return value.ToString();

                // If ResourceType defined, resolve through ResourceManager directly so we don't rely on public properties
                if (attr.ResourceType != null)
                {
                    try
                    {
                        var rm = new ResourceManager(attr.ResourceType.FullName!, attr.ResourceType.Assembly);
                        var localized = rm.GetString(key, CultureInfo.CurrentUICulture);
                        if (!string.IsNullOrWhiteSpace(localized))
                            return localized!;
                    }
                    catch
                    {
                        // ignore and fall back below
                    }
                }

                // Fallback: return the key itself or enum name
                return key ?? value.ToString();
            }
            catch
            {
                return value.ToString();
            }
        }
    }
}
