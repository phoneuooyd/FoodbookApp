using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Foodbook.Localization;

public class LocalizationResourceManager : INotifyPropertyChanged
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static LocalizationResourceManager Instance { get; } = new LocalizationResourceManager();

    private LocalizationResourceManager()
    {
        _resourceManager = new ResourceManager("FoodbookApp.Resources.Localization.Strings", typeof(LocalizationResourceManager).Assembly);
    }

    public string this[string text] => _resourceManager.GetString(text, _currentCulture) ?? text;

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            }
        }
    }
}
