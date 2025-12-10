using Foodbook.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Views.Components;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using FoodbookApp.Interfaces;

namespace Foodbook.Views;

public partial class HomePage : ContentPage
{
    private HomeViewModel ViewModel => BindingContext as HomeViewModel;
    private IThemeService? _themeService;
    private IFontService? _fontService;
    private ILocalizationService? _localizationService;
    private bool _isMealsPopupOpen = false; // Protection against multiple opens

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

    /// <summary>
    /// Handler for meals popup with protection against multiple opens
    /// </summary>
    private async void OnShowMealsPopupClicked(object sender, EventArgs e)
    {
        // Protection against multiple opens
        if (_isMealsPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? HomePage: Meals popup already open, ignoring request");
            return;
        }

        try
        {
            _isMealsPopupOpen = true;
            System.Diagnostics.Debug.WriteLine("?? HomePage: Opening meals popup");

            if (ViewModel?.ShowMealsPopupCommand?.CanExecute(null) == true)
            {
                ViewModel.ShowMealsPopupCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? HomePage: Error opening meals popup: {ex.Message}");
            
            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? HomePage: Attempting to close any existing modal pages");
                
                try
                {
                    // Try to dismiss any existing modal pages
                    while (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("? HomePage: Modal stack cleared");
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"?? HomePage: Could not clear modal stack: {modalEx.Message}");
                }
            }
        }
        finally
        {
            _isMealsPopupOpen = false;
            System.Diagnostics.Debug.WriteLine("?? HomePage: Meals popup protection released");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool exit = await DisplayAlert("Potwierdzenie", "Czy na pewno chcesz wyjœæ z aplikacji?", "Tak", "Nie");
                if (exit)
                {
                    try
                    {
                        Application.Current?.Quit();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HomePage] Quit failed: {ex.Message}");
                        // Fallback for platforms without Quit support
#if ANDROID
                        // On Android, simulate back to close
                        await Task.Delay(50);
                        System.Diagnostics.Process.GetCurrentProcess().CloseMainWindow();
#endif
                    }
                }
            });
            return true; // consume back press
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnBackButtonPressed error: {ex.Message}");
            return base.OnBackButtonPressed();
        }
    }
}

