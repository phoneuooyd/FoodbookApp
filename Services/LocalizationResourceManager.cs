using System.ComponentModel;
using Foodbook.Services;

namespace Foodbook.Services;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    private readonly ILocalizationService _localizationService;

    public LocalizationResourceManager(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.CultureChanged += (_, _) => OnPropertyChanged("Item[]");
    }

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var parts = key.Split(':');
            if (parts.Length != 2) return key;
            return _localizationService.GetString(parts[0], parts[1]);
        }
    }

    public void SetCulture(string cultureName)
    {
        _localizationService.SetCulture(cultureName);
        OnPropertyChanged("Item[]");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string? name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
