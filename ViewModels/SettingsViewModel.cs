using System.Collections.ObjectModel;
using System.ComponentModel;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly LocalizationResourceManager _locManager;
    private readonly IPreferencesService _preferencesService;

    public ObservableCollection<string> SupportedCultures { get; }

    private string _selectedCulture;
    public string SelectedCulture
    {
        get => _selectedCulture;
        set
        {
            if (_selectedCulture == value) return;
            _selectedCulture = value;
            OnPropertyChanged(nameof(SelectedCulture));
            _locManager.SetCulture(value);
            
            // Save the selected culture to preferences
            _preferencesService.SaveLanguage(value);
        }
    }

    public SettingsViewModel(LocalizationResourceManager locManager, IPreferencesService preferencesService)
    {
        _locManager = locManager;
        _preferencesService = preferencesService;
        
        // Initialize supported cultures from preferences service
        SupportedCultures = new ObservableCollection<string>(_preferencesService.GetSupportedCultures());
        
        // Load the saved culture preference or use system default
        _selectedCulture = LoadSelectedCulture();
        
        // Set the culture without triggering the setter to avoid recursive calls
        _locManager.SetCulture(_selectedCulture);
    }

    private string LoadSelectedCulture()
    {
        try
        {
            var savedCulture = _preferencesService.GetSavedLanguage();
            
            if (!string.IsNullOrEmpty(savedCulture))
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded saved culture preference: {savedCulture}");
                return savedCulture;
            }
            
            // Fall back to system culture if no preference saved
            var systemCulture = System.Globalization.CultureInfo.CurrentUICulture.Name;
            var supportedCultures = _preferencesService.GetSupportedCultures();
            var fallbackCulture = supportedCultures.Contains(systemCulture) ? systemCulture : supportedCultures[0];
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] No saved preference, using fallback: {fallbackCulture}");
            return fallbackCulture;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Failed to load culture preference: {ex.Message}");
            return _preferencesService.GetSupportedCultures()[0]; // Default to first supported culture
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
