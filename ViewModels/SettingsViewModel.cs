using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localizationService;
    private bool _isChangingLanguage = false;

    private (string Code, string Name) _selectedLanguage;
    public (string Code, string Name) SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!_selectedLanguage.Equals(value) && !_isChangingLanguage)
            {
                System.Diagnostics.Debug.WriteLine($"?? Language selection changed from {_selectedLanguage.Name} ({_selectedLanguage.Code}) to {value.Name} ({value.Code})");
                _selectedLanguage = value;
                OnPropertyChanged();
                _ = Task.Run(async () => await ChangeLanguageAsync(value.Code));
            }
        }
    }

    public ObservableCollection<(string Code, string Name)> AvailableLanguages { get; }

    public ICommand ChangeLanguageCommand { get; }

    public SettingsViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
        var langs = _localizationService.GetAvailableLanguages();
        AvailableLanguages = new ObservableCollection<(string Code, string Name)>(langs);
        
        var currentCode = _localizationService.GetCurrentLanguage();
        var selectedLang = AvailableLanguages.FirstOrDefault(l => l.Code == currentCode);
        
        // Initialize without triggering change event
        _selectedLanguage = selectedLang.Equals(default((string, string))) ? 
            AvailableLanguages.FirstOrDefault() : selectedLang;
        
        System.Diagnostics.Debug.WriteLine($"?? SettingsViewModel initialized with language: {_selectedLanguage.Name} ({_selectedLanguage.Code})");
        System.Diagnostics.Debug.WriteLine($"?? Available languages count: {AvailableLanguages.Count}");
        
        // Notify property changed for initial binding
        OnPropertyChanged(nameof(SelectedLanguage));
        
        ChangeLanguageCommand = new Command<(string Code, string Name)>(async (lang) => await ChangeLanguageAsync(lang.Code));
    }

    private async Task ChangeLanguageAsync(string languageCode)
    {
        if (_isChangingLanguage) return;
        
        try
        {
            _isChangingLanguage = true;
            System.Diagnostics.Debug.WriteLine($"?? Starting language change to: {languageCode}");
            
            await _localizationService.SetLanguageAsync(languageCode);
            
            // Show confirmation message with localized text
            var title = languageCode == "pl-PL" ? "Jêzyk zmieniony" : "Language Changed";
            var message = languageCode == "pl-PL" ? 
                "Uruchom ponownie aplikacjê, aby zobaczyæ zmiany." : 
                "Please restart the application to see the changes.";
            var okText = languageCode == "pl-PL" ? "OK" : "OK";
            
            System.Diagnostics.Debug.WriteLine($"?? Showing confirmation dialog in {languageCode}");
            await Shell.Current.DisplayAlert(title, message, okText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? Error changing language: {ex.Message}");
            await Shell.Current.DisplayAlert(
                "Error",
                "Failed to change language. Please try again.",
                "OK");
        }
        finally
        {
            _isChangingLanguage = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        System.Diagnostics.Debug.WriteLine($"?? PropertyChanged fired for: {name}");
    }
}