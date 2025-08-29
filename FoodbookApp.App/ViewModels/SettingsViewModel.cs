using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
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

    private bool _isMigrationInProgress;
    public bool IsMigrationInProgress
    {
        get => _isMigrationInProgress;
        set
        {
            if (_isMigrationInProgress == value) return;
            _isMigrationInProgress = value;
            OnPropertyChanged(nameof(IsMigrationInProgress));
            OnPropertyChanged(nameof(CanExecuteMigration));
        }
    }

    private string _migrationStatus = string.Empty;
    public string MigrationStatus
    {
        get => _migrationStatus;
        set
        {
            if (_migrationStatus == value) return;
            _migrationStatus = value;
            OnPropertyChanged(nameof(MigrationStatus));
        }
    }

    public bool CanExecuteMigration => !IsMigrationInProgress;

    // Commands for database operations
    public ICommand MigrateDatabaseCommand { get; }
    public ICommand ResetDatabaseCommand { get; }

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

        // Initialize commands
        MigrateDatabaseCommand = new Command(async () => await MigrateDatabaseAsync(), () => CanExecuteMigration);
        ResetDatabaseCommand = new Command(async () => await ResetDatabaseAsync(), () => CanExecuteMigration);
    }

    private async Task MigrateDatabaseAsync()
    {
        try
        {
            IsMigrationInProgress = true;
            MigrationStatus = "Wykonywanie migracji bazy danych...";
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database migration");
            
            var success = await FoodbookApp.MauiProgram.MigrateDatabaseAsync();
            
            if (success)
            {
                MigrationStatus = "Migracja zako�czona pomy�lnie!";
                await Application.Current?.MainPage?.DisplayAlert(
                    "Sukces", 
                    "Migracja bazy danych zosta�a wykonana pomy�lnie.", 
                    "OK");
            }
            else
            {
                MigrationStatus = "Migracja nieudana.";
                await Application.Current?.MainPage?.DisplayAlert(
                    "B��d", 
                    "Nie uda�o si� wykona� migracji bazy danych. Sprawd� logi aplikacji.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Migration error: {ex.Message}");
            MigrationStatus = $"B��d migracji: {ex.Message}";
            await Application.Current?.MainPage?.DisplayAlert(
                "B��d", 
                $"Wyst�pi� b��d podczas migracji: {ex.Message}", 
                "OK");
        }
        finally
        {
            IsMigrationInProgress = false;
            
            // Clear status after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() => MigrationStatus = string.Empty);
            });
        }
    }

    private async Task ResetDatabaseAsync()
    {
        try
        {
            bool confirm = await Application.Current?.MainPage?.DisplayAlert(
                "Resetuj baz� danych", 
                "Czy na pewno chcesz usun�� wszystkie dane? Ta operacja jest nieodwracalna.\n\nWszystkie przepisy, plany i listy zakup�w zostan� utracone.", 
                "Tak, resetuj", "Anuluj") == true;
                
            if (!confirm) return;

            IsMigrationInProgress = true;
            MigrationStatus = "Resetowanie bazy danych...";
            
            System.Diagnostics.Debug.WriteLine("[SettingsViewModel] Starting database reset");
            
            var success = await FoodbookApp.MauiProgram.ResetDatabaseAsync();
            
            if (success)
            {
                MigrationStatus = "Baza danych zosta�a zresetowana!";
                await Application.Current?.MainPage?.DisplayAlert(
                    "Sukces", 
                    "Baza danych zosta�a zresetowana. Aplikacja zostanie zamkni�ta - uruchom j� ponownie.", 
                    "OK");
                
                // Close application after reset
                Application.Current?.Quit();
            }
            else
            {
                MigrationStatus = "Reset nieudany.";
                await Application.Current?.MainPage?.DisplayAlert(
                    "B��d", 
                    "Nie uda�o si� zresetowa� bazy danych. Sprawd� logi aplikacji.", 
                    "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Reset error: {ex.Message}");
            MigrationStatus = $"B��d resetu: {ex.Message}";
            await Application.Current?.MainPage?.DisplayAlert(
                "B��d", 
                $"Wyst�pi� b��d podczas resetowania: {ex.Message}", 
                "OK");
        }
        finally
        {
            IsMigrationInProgress = false;
            
            // Clear status after 3 seconds
            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() => MigrationStatus = string.Empty);
            });
        }
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
