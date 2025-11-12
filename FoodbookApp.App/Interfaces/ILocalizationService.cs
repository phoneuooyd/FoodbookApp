using System.Globalization;

namespace FoodbookApp.Interfaces;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    void SetCulture(string cultureName);
    string GetString(string baseName, string key);

    event EventHandler? CultureChanged;
    event EventHandler? PickerRefreshRequested;

    void RequestPickerRefresh();
}
