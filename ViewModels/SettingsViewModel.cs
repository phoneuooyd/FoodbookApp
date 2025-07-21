using System.Collections.ObjectModel;
using System.ComponentModel;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly LocalizationResourceManager _locManager;

    public ObservableCollection<string> SupportedCultures { get; } = new() { "en", "pl-PL" };

    private string _selectedCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
    public string SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (_selectedCulture == value) return;
            _selectedCulture = value;
            OnPropertyChanged(nameof(SelectedCulture));
            _locManager.SetCulture(value);
        }
    }

    public SettingsViewModel(LocalizationResourceManager locManager)
    {
        _locManager = locManager;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
