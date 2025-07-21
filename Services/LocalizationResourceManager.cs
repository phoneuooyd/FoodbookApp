using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.Services;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    private readonly ILocalizationService _localizationService;

    public LocalizationResourceManager(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        _localizationService.CultureChanged += (_, __) => OnPropertyChanged(null);
    }

    public string this[string key]
    {
        get
        {
            var parts = key.Split('.');
            if (parts.Length == 2)
            {
                return _localizationService.GetString(parts[0], parts[1]);
            }
            return key;
        }
    }

    public static LocalizationResourceManager Instance =>
        MauiProgram.ServiceProvider!.GetRequiredService<LocalizationResourceManager>();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
