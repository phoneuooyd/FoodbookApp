using Foodbook.ViewModels;
using Microsoft.Maui.Controls;
using Foodbook.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Foodbook.Views;

public partial class HomePage : ContentPage
{
    private HomeViewModel ViewModel => BindingContext as HomeViewModel;
    private IThemeService? _themeService;
    private IFontService? _fontService;
    private ILocalizationService? _localizationService;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize services and subscribe to events
        InitializeThemeAndFontHandling();
        
        if (ViewModel != null)
            await ViewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unsubscribe from events to prevent memory leaks
        CleanupThemeAndFontHandling();
    }

    private void InitializeThemeAndFontHandling()
    {
        try
        {
            // Get services from dependency injection
            var serviceProvider = FoodbookApp.MauiProgram.ServiceProvider;
            if (serviceProvider != null)
            {
                _themeService = serviceProvider.GetService<IThemeService>();
                _fontService = serviceProvider.GetService<IFontService>();
                _localizationService = serviceProvider.GetService<ILocalizationService>();

                // Subscribe to change events
                if (_fontService != null)
                {
                    _fontService.FontSettingsChanged += OnFontSettingsChanged;
                }

                if (_localizationService != null)
                {
                    _localizationService.CultureChanged += OnCultureChanged;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Error initializing theme and font handling: {ex.Message}");
        }
    }

    private void CleanupThemeAndFontHandling()
    {
        try
        {
            // Unsubscribe from events to prevent memory leaks
            if (_fontService != null)
            {
                _fontService.FontSettingsChanged -= OnFontSettingsChanged;
            }

            if (_localizationService != null)
            {
                _localizationService.CultureChanged -= OnCultureChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Error cleaning up theme and font handling: {ex.Message}");
        }
    }

    private void OnFontSettingsChanged(object? sender, FontSettingsChangedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Font changes are automatically handled by DynamicResource in XAML
                // No additional font handling needed for HomePage
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Error in OnFontSettingsChanged: {ex.Message}");
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Most localization is handled automatically by the Translate markup extension
                // No additional handling needed for HomePage
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Error in OnCultureChanged: {ex.Message}");
        }
    }

    private async void OnArchiveClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ArchivePage));
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SettingsPage));
    }
}

