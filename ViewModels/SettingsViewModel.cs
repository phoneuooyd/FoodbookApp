using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _localizationService;

    private (string Code, string Name) _selectedLanguage;
    public (string Code, string Name) SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!_selectedLanguage.Equals(value))
            {
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
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == currentCode);
        ChangeLanguageCommand = new Command<(string Code, string Name)>(async (lang) => await ChangeLanguageAsync(lang.Code));
    }

    private async Task ChangeLanguageAsync(string languageCode)
    {
        try
        {
            await _localizationService.SetLanguageAsync(languageCode);
            await Shell.Current.DisplayAlert(
                "Language Changed",
                "Please restart the application to see the changes.",
                "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error changing language: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}