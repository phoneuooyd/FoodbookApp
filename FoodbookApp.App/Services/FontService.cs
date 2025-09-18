using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.Services;

/// <summary>
/// Implementation of font service for managing dynamic font changes
/// </summary>
public class FontService : IFontService
{
    private readonly IPreferencesService _preferencesService;
    private FontSettings _currentFontSettings;

    /// <inheritdoc/>
    public event EventHandler<FontSettingsChangedEventArgs>? FontSettingsChanged;

    /// <inheritdoc/>
    public FontSettings CurrentFontSettings => _currentFontSettings.Clone();

    public FontService(IPreferencesService preferencesService)
    {
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _currentFontSettings = new FontSettings(); // Initialize with defaults
        
        System.Diagnostics.Debug.WriteLine("[FontService] Initialized");
    }

    /// <inheritdoc/>
    public AppFontFamily[] GetAvailableFontFamilies()
    {
        return Enum.GetValues<AppFontFamily>();
    }

    /// <inheritdoc/>
    public AppFontSize[] GetAvailableFontSizes()
    {
        return Enum.GetValues<AppFontSize>();
    }

    /// <inheritdoc/>
    public void SetFontFamily(AppFontFamily fontFamily)
    {
        try
        {
            var oldSettings = _currentFontSettings.Clone();
            _currentFontSettings.FontFamily = fontFamily;
            
            // Save to preferences
            _preferencesService.SaveFontFamily(fontFamily);
            
            // Apply changes and raise event
            ApplyFontSettings();
            OnFontSettingsChanged(oldSettings, _currentFontSettings);
            
            System.Diagnostics.Debug.WriteLine($"[FontService] Font family changed to: {fontFamily}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Failed to set font family: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void SetFontSize(AppFontSize fontSize)
    {
        try
        {
            var oldSettings = _currentFontSettings.Clone();
            _currentFontSettings.FontSize = fontSize;
            
            // Save to preferences
            _preferencesService.SaveFontSize(fontSize);
            
            // Apply changes and raise event
            ApplyFontSettings();
            OnFontSettingsChanged(oldSettings, _currentFontSettings);
            
            System.Diagnostics.Debug.WriteLine($"[FontService] Font size changed to: {fontSize}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Failed to set font size: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void SetFontSettings(FontSettings settings)
    {
        try
        {
            if (settings == null)
            {
                System.Diagnostics.Debug.WriteLine("[FontService] Cannot set null font settings");
                return;
            }
            
            var oldSettings = _currentFontSettings.Clone();
            _currentFontSettings = settings.Clone();
            
            // Save to preferences
            _preferencesService.SaveFontSettings(_currentFontSettings);
            
            // Apply changes and raise event
            ApplyFontSettings();
            OnFontSettingsChanged(oldSettings, _currentFontSettings);
            
            System.Diagnostics.Debug.WriteLine($"[FontService] Complete font settings changed to: {settings.FontFamily}, {settings.FontSize}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Failed to set font settings: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void LoadSavedSettings()
    {
        try
        {
            var savedSettings = _preferencesService.GetFontSettings();
            _currentFontSettings = savedSettings.Clone();
            
            System.Diagnostics.Debug.WriteLine($"[FontService] Loaded saved settings: {savedSettings.FontFamily}, {savedSettings.FontSize}");
            
            // Apply the loaded settings
            ApplyFontSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Failed to load saved settings: {ex.Message}");
            _currentFontSettings = new FontSettings(); // Fallback to defaults
        }
    }

    /// <inheritdoc/>
    public void ApplyFontSettings()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var app = Application.Current;
                if (app?.Resources == null)
                {
                    System.Diagnostics.Debug.WriteLine("[FontService] Application resources not available");
                    return;
                }

                // Update dynamic resources for font family
                if (_currentFontSettings.PlatformFontFamily != null)
                {
                    app.Resources["AppFontFamily"] = _currentFontSettings.PlatformFontFamily;
                }
                else
                {
                    // Remove custom font family to use system default
                    if (app.Resources.ContainsKey("AppFontFamily"))
                    {
                        app.Resources.Remove("AppFontFamily");
                    }
                }

                // Update dynamic resources for font size
                app.Resources["AppFontSize"] = _currentFontSettings.PlatformFontSize;
                app.Resources["AppSmallFontSize"] = _currentFontSettings.PlatformFontSize - 2;
                app.Resources["AppLargeFontSize"] = _currentFontSettings.PlatformFontSize + 4;
                app.Resources["AppTitleFontSize"] = _currentFontSettings.PlatformFontSize + 8;

                System.Diagnostics.Debug.WriteLine($"[FontService] Applied font settings: Family={_currentFontSettings.PlatformFontFamily ?? "Default"}, Size={_currentFontSettings.PlatformFontSize}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Failed to apply font settings: {ex.Message}");
        }
    }

    private void OnFontSettingsChanged(FontSettings oldSettings, FontSettings newSettings)
    {
        try
        {
            FontSettingsChanged?.Invoke(this, new FontSettingsChangedEventArgs(oldSettings, newSettings));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FontService] Error raising FontSettingsChanged event: {ex.Message}");
        }
    }
}