using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class LanguageItem
{
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public override string ToString() => DisplayName;
}

public class SettingsViewModel : INotifyPropertyChanged
{
    public ObservableCollection<LanguageItem> Languages { get; } = new()
    {
        new LanguageItem{ Code = "en", DisplayName = new CultureInfo("en").NativeName },
        new LanguageItem{ Code = "pl-PL", DisplayName = new CultureInfo("pl-PL").NativeName }
    };

    private LanguageItem _selectedLanguage;
    public LanguageItem SelectedLanguage
    {
        get => _selectedLanguage;
        set { if (_selectedLanguage != value) { _selectedLanguage = value; OnPropertyChanged(); } }
    }

    public ICommand ApplyLanguageCommand { get; }

    public SettingsViewModel()
    {
        var current = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code.StartsWith(current)) ?? Languages.First();
        ApplyLanguageCommand = new Command(ApplyLanguage);
        OnPropertyChanged(nameof(SelectedLanguage));
    }

    private void ApplyLanguage()
    {
        var culture = new CultureInfo(SelectedLanguage.Code);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Application.Current.MainPage = new AppShell();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
