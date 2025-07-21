using System.ComponentModel;
using System.Globalization;
using Foodbook.Services;

namespace Foodbook.Services.Localization;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    private readonly ILocalizationService _localizationService;

    public LocalizationResourceManager(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        if (_localizationService is LocalizationService concrete)
        {
            concrete.CultureChanged += (_, _) => OnPropertyChanged(null);
        }
    }

    public CultureInfo CurrentCulture => _localizationService.CurrentCulture;

    public string GetValue(string baseName, string key)
    {
        return _localizationService.GetString(baseName, key);
    }

    public void SetCulture(string cultureName)
    {
        _localizationService.SetCulture(cultureName);
        OnPropertyChanged(null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string? name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
