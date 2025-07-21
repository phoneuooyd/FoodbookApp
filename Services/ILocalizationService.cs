using System.Globalization;

namespace Foodbook.Services;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    event EventHandler? CultureChanged;
    void SetCulture(string cultureName);
    string GetString(string baseName, string key);
}
