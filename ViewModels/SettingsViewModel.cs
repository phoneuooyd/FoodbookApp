using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localizationService;

    public ObservableCollection<CultureInfo> AvailableCultures { get; } = new()
    {
        new CultureInfo("en"),
        new CultureInfo("pl-PL")
    };

    private CultureInfo _selectedCulture;
    public CultureInfo SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (_selectedCulture != value)
            {
                _selectedCulture = value;
                OnPropertyChanged();
                _localizationService.SetCulture(value.Name);
            }
        }
    }

    public SettingsViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        _selectedCulture = localizationService.CurrentCulture;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
