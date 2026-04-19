using Foodbook.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Views.Components;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Foodbook.Services;
using FoodbookApp.Localization;

namespace Foodbook.Views;

public partial class HomePage : ContentPage, ITabLoadable
{
    private HomeViewModel ViewModel => BindingContext as HomeViewModel;
    private IThemeService? _themeService;
    private IFontService? _fontService;
    private ILocalizationService? _localizationService;
    private bool _isMealsPopupOpen = false; // Protection against multiple opens
    private ISupabaseAuthService? _supabaseAuth;
    private bool _hasLoadedOnce;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private ISupabaseAuthService? GetSupabaseAuth()
        => _supabaseAuth ??= FoodbookApp.MauiProgram.ServiceProvider?.GetService<ISupabaseAuthService>();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize services and subscribe to events
        InitializeThemeAndFontHandling();
        
        // Subscribe to data-change events so counts refresh without leaving the page
        AppEvents.RecipesChangedAsync += OnDataChangedAsync;
        AppEvents.PlanChangedAsync += OnDataChangedAsync;

        if (ViewModel != null)
        {
            if (_hasLoadedOnce)
            {
                try
                {
                    await ViewModel.LoadAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[HomePage] Re-appearing LoadAsync failed: {ex.Message}");
                }
            }
            // First appearance: skip — TabBarComponent.TriggerInitialLoadAsync handles the initial load
            _hasLoadedOnce = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Unsubscribe from events to prevent memory leaks
        CleanupThemeAndFontHandling();

        AppEvents.RecipesChangedAsync -= OnDataChangedAsync;
        AppEvents.PlanChangedAsync -= OnDataChangedAsync;
    }

    private async Task OnDataChangedAsync()
    {
        try
        {
            if (ViewModel != null)
                await ViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnDataChangedAsync error: {ex.Message}");
        }
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

    private async void OnOpenProfileClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(ProfilePage));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] Failed to open ProfilePage: {ex.Message}");
            await DisplayAlert(
                GetHomeText("ProfileOpenErrorTitle", "Error"),
                GetHomeText("ProfileOpenErrorMessage", "Unable to open profile page."),
                GetButtonText("OK", "OK"));
        }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool exit = await DisplayAlert(
                    HomePageResources.ExitAppTitle,
                    HomePageResources.ExitAppConfirmation,
                    GetButtonText("Yes", "Yes"),
                    GetButtonText("No", "No"));
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

    /// <summary>
    /// Called by TabBarComponent when this tab is activated.
    /// </summary>
    public async Task OnTabActivatedAsync()
    {
        try
        {
            InitializeThemeAndFontHandling();

            if (ViewModel != null)
            {
                await ViewModel.LoadAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnTabActivatedAsync error: {ex.Message}");
        }
    }
    private static string GetHomeText(string key, string fallback)
        => HomePageResources.ResourceManager.GetString(key) ?? fallback;

    private static string GetButtonText(string key, string fallback)
        => ButtonResources.ResourceManager.GetString(key) ?? fallback;

}
