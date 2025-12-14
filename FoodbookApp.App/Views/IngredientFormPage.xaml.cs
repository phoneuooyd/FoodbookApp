using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Foodbook.Views.Components;
using System.Threading.Tasks;
using Foodbook.Models;
using FoodbookApp;
using CommunityToolkit.Maui.Views;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Graphics;
using FoodbookApp.Interfaces;

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class IngredientFormPage : ContentPage
{
    private IngredientFormViewModel ViewModel => BindingContext as IngredientFormViewModel;
    private readonly PageThemeHelper _themeHelper;

    // Store original resources to restore later
    private object? _originalPageBackgroundColorResource;
    private object? _originalPageBackgroundBrushResource;
    private bool _appliedOpaqueBackground = false;
    private bool _appliedLocalOverride = false;

    // ? SIMPLIFIED: Shell navigation handling for unsaved changes (based on ShoppingListDetailPage)
    private bool _isSubscribedToShellNavigating = false;
    private bool _suppressShellNavigating = false;

    // Track if the page has ever loaded data (similar to AddRecipePage)
    private bool _hasEverLoaded = false;

    public IngredientFormPage(IngredientFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _themeHelper = new PageThemeHelper();
    }

    // Returns a task that completes when ViewModel raises SavedAsync or the page disappears
    public Task AwaitSaveAsync(int timeoutMs = 30000)
    {
        var tcs = new TaskCompletionSource();

        void OnSavedHandler()
        {
            try { tcs.TrySetResult(); } catch { }
        }

        void OnPageDisappearing(object? s, EventArgs e)
        {
            try { tcs.TrySetResult(); } catch { }
            try { this.Disappearing -= OnPageDisappearing; } catch { }
        }

        if (ViewModel != null)
        {
            ViewModel.SavedAsync += async () => { OnSavedHandler(); await Task.CompletedTask; };
        }

        this.Disappearing += OnPageDisappearing;

        // Timeout fallback
        var ct = new CancellationTokenSource(timeoutMs);
        ct.Token.Register(() => tcs.TrySetResult());

        return tcs.Task;
    }

    protected override void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            _themeHelper.Initialize();
            _themeHelper.ThemeChanged += OnThemeChanged;
            _themeHelper.CultureChanged += OnCultureChanged;

            // ? SIMPLIFIED: Subscribe Shell.Navigating for unsaved changes prompt
            try
            {
                if (!_isSubscribedToShellNavigating && Shell.Current != null)
                {
                    Shell.Current.Navigating += OnShellNavigating;
                    _isSubscribedToShellNavigating = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Failed to subscribe Shell.Navigating: {ex.Message}");
            }

            // Handle modal presentation background
            try
            {
                if (IsModalPage())
                {
                    HideUnderlyingContent();

                    var themeService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IThemeService>();
                    bool wallpaperEnabled = themeService?.IsWallpaperBackgroundEnabled() == true;
                    bool colorfulEnabled = themeService?.GetIsColorfulBackgroundEnabled() == true;

                    if (wallpaperEnabled || colorfulEnabled)
                    {
                        ApplyOpaqueLocalBackground();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnAppearing modal handling error: {ex.Message}");
            }

            // Reset or load data if this is the first appearance
            if (!_hasEverLoaded)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] First load - ItemId: {ItemId}");
                
                if (ItemId == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[IngredientFormPage] First load - resetting form for new ingredient");
                    ViewModel?.Reset();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] First load - loading ingredient {ItemId}");
                }
                _hasEverLoaded = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Subsequent appearance - skipping reset");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnAppearing error: {ex.Message}");
            if (ViewModel != null)
            {
                ViewModel.ValidationMessage = $"B³¹d ³adowania strony: {ex.Message}";
            }
        }
    }

    private void ApplyOpaqueLocalBackground(bool storeOriginals = false)
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources == null) return;

            if (storeOriginals)
            {
                if (_originalPageBackgroundColorResource == null && app.Resources.TryGetValue("PageBackgroundColor", out var origColor))
                    _originalPageBackgroundColorResource = origColor;
                if (_originalPageBackgroundBrushResource == null && app.Resources.TryGetValue("PageBackgroundBrush", out var origBrush))
                    _originalPageBackgroundBrushResource = origBrush;
            }

            Color pageBg = Colors.White;
            if (app.Resources.TryGetValue("PageBackgroundColor", out var pb) && pb is Color c)
                pageBg = c;

            var overlay = Color.FromRgb(pageBg.Red, pageBg.Green, pageBg.Blue); // force alpha 1

            this.Resources["PageBackgroundColor"] = overlay;
            this.Resources["PageBackgroundBrush"] = new SolidColorBrush(overlay);
            this.BackgroundColor = overlay;

            _appliedLocalOverride = true;
            if (storeOriginals) _appliedOpaqueBackground = true;
            System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Applied opaque local background (wallpaper/colorful mode)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] ApplyOpaqueLocalBackground error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Restore visibility of underlying content
        RestoreUnderlyingContent();

        // Cleanup theme handling
        _themeHelper.ThemeChanged -= OnThemeChanged;
        _themeHelper.CultureChanged -= OnCultureChanged;
        _themeHelper.Cleanup();

        // ? SIMPLIFIED: Unsubscribe Shell.Navigating
        try
        {
            if (_isSubscribedToShellNavigating && Shell.Current != null)
            {
                Shell.Current.Navigating -= OnShellNavigating;
                _isSubscribedToShellNavigating = false;
            }
        }
        catch { }

        // Clean up local overrides
        try
        {
            if (_appliedLocalOverride)
            {
                try { this.Resources.Remove("PageBackgroundColor"); } catch { }
                try { this.Resources.Remove("PageBackgroundBrush"); } catch { }
                _appliedLocalOverride = false;
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Removed local background overrides");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Failed to cleanup local override: {ex.Message}");
        }

        // Restore original page background resources if we applied override
        try
        {
            if (_appliedOpaqueBackground)
            {
                var app = Application.Current;
                if (app?.Resources != null)
                {
                    if (_originalPageBackgroundColorResource != null)
                    {
                        app.Resources["PageBackgroundColor"] = _originalPageBackgroundColorResource;
                        _originalPageBackgroundColorResource = null;
                    }
                    if (_originalPageBackgroundBrushResource != null)
                    {
                        app.Resources["PageBackgroundBrush"] = _originalPageBackgroundBrushResource;
                        _originalPageBackgroundBrushResource = null;
                    }
                }

                try { this.Resources.Remove("PageBackgroundColor"); } catch { }
                try { this.Resources.Remove("PageBackgroundBrush"); } catch { }
                _appliedOpaqueBackground = false;
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Restored original background resources");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Failed to restore background override: {ex.Message}");
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ViewModel.SelectedTabIndex = ViewModel.SelectedTabIndex;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnThemeChanged error: {ex.Message}");
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Culture changed - refreshing unit pickers");
                RefreshUnitPickers();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnCultureChanged error: {ex.Message}");
        }
    }

    private void RefreshUnitPickers()
    {
        try
        {
            var pickers = FindVisualChildren<SimplePicker>(this);
            foreach (var picker in pickers)
            {
                picker.RefreshDisplayText();
            }
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Refreshed {pickers.Count()} unit pickers");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error refreshing unit pickers: {ex.Message}");
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(Element element) where T : Element
    {
        if (element is T match)
            yield return match;

        if (element is TabComponent tabComponent)
        {
            foreach (var tab in tabComponent.Tabs)
            {
                if (tab.Content != null)
                {
                    foreach (var descendant in FindVisualChildren<T>(tab.Content))
                        yield return descendant;
                }
            }
        }
        else if (element is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is Element childElement)
                {
                    foreach (var descendant in FindVisualChildren<T>(childElement))
                        yield return descendant;
                }
            }
        }
        else if (element is ContentView contentView && contentView.Content != null)
        {
            foreach (var descendant in FindVisualChildren<T>(contentView.Content))
                yield return descendant;
        }
        else if (element is ScrollView scrollView && scrollView.Content != null)
        {
            foreach (var descendant in FindVisualChildren<T>(scrollView.Content))
                yield return descendant;
        }
    }

    private int _itemId;
    public int ItemId
    {
        get => _itemId;
        set
        {
            try
            {
                _itemId = value;
                if (value > 0)
                    Task.Run(async () => await ViewModel.LoadAsync(value));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting ItemId: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B³¹d ³adowania sk³adnika: {ex.Message}";
                }
            }
        }
    }

    /// <summary>
    /// ? SIMPLIFIED: Handle Shell navigation for unsaved changes (based on ShoppingListDetailPage)
    /// </summary>
    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnShellNavigating - Source: {e.Source}, Target: {e.Target?.Location}");
            
            // If suppressed, allow navigation
            if (_suppressShellNavigating)
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Navigation suppressed flag is set, allowing navigation");
                return;
            }
            
            // If pushing a new page, allow
            if (e.Source == ShellNavigationSource.Push)
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Push navigation detected, allowing");
                return;
            }

            // Check for unsaved changes
            bool hasChanges = ViewModel?.HasUnsavedChanges == true;
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] HasUnsavedChanges: {hasChanges}");
            
            if (hasChanges)
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Unsaved changes detected, canceling navigation and showing prompt");
                e.Cancel();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool leave = await DisplayAlert(
                        FoodbookApp.Localization.AddRecipePageResources.ConfirmTitle, 
                        FoodbookApp.Localization.AddRecipePageResources.UnsavedChangesMessage, 
                        FoodbookApp.Localization.AddRecipePageResources.YesButton, 
                        FoodbookApp.Localization.AddRecipePageResources.NoButton);
                    
                    System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] User chose to leave: {leave}");
                    
                    if (leave)
                    {
                        try { ViewModel?.DiscardChanges(); } catch { }
                        _suppressShellNavigating = true;
                        try
                        {
                            var targetLoc = e.Target?.Location?.OriginalString ?? string.Empty;
                            var nav = Shell.Current?.Navigation;
                            
                            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Navigating away - Modal stack count: {nav?.ModalStack?.Count ?? 0}");
                            
                            // If modal, pop modal first
                            if (nav?.ModalStack?.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Popping modal");
                                await nav.PopModalAsync(false);
                            }
                            else if (!string.IsNullOrEmpty(targetLoc))
                            {
                                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Navigating to: {targetLoc}");
                                await Shell.Current.GoToAsync(targetLoc);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Navigating back");
                                await Shell.Current.GoToAsync("..", false);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Navigation error: {ex.Message}");
                        }
                        finally
                        {
                            await Task.Delay(200);
                            _suppressShellNavigating = false;
                            System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Navigation suppression flag cleared");
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnShellNavigating error: {ex.Message}");
        }
    }

    /// <summary>
    /// ? SIMPLIFIED: Handle hardware back button (based on ShoppingListDetailPage)
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnBackButtonPressed - HasUnsavedChanges: {ViewModel?.HasUnsavedChanges == true}");
            
            if (ViewModel?.HasUnsavedChanges == true)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool leave = await DisplayAlert(
                        FoodbookApp.Localization.AddRecipePageResources.ConfirmTitle, 
                        FoodbookApp.Localization.AddRecipePageResources.UnsavedChangesMessage, 
                        FoodbookApp.Localization.AddRecipePageResources.YesButton, 
                        FoodbookApp.Localization.AddRecipePageResources.NoButton);
                    
                    System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Back button - User chose to leave: {leave}");
                    
                    if (leave)
                    {
                        try { ViewModel?.DiscardChanges(); } catch { }
                        _suppressShellNavigating = true;
                        try
                        {
                            var nav = Shell.Current?.Navigation;
                            if (nav?.ModalStack?.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Back button - Popping modal");
                                await nav.PopModalAsync(false);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Back button - Navigating back");
                                await Shell.Current.GoToAsync("..", false);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Back button navigation error: {ex.Message}");
                        }
                        finally
                        {
                            await Task.Delay(200);
                            _suppressShellNavigating = false;
                        }
                    }
                });
                return true; // Cancel default back, we handle it
            }

            // No unsaved changes - use CancelCommand if available
            System.Diagnostics.Debug.WriteLine("[IngredientFormPage] No unsaved changes, executing CancelCommand");
            if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                ViewModel.CancelCommand.Execute(null);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnBackButtonPressed error: {ex.Message}");
            return base.OnBackButtonPressed();
        }
    }

    // Helper to detect whether this page was pushed modally
    private bool IsModalPage()
    {
        try
        {
            var nav = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
            if (nav?.ModalStack == null) return false;
            return nav.ModalStack.Contains(this);
        }
        catch { return false; }
    }

    private void HideUnderlyingContent()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            if (currentPage is Foodbook.Views.AddRecipePage addRecipePage)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    addRecipePage.Opacity = 0;
                    System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Hidden AddRecipePage (Opacity=0)");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Failed to hide underlying content: {ex.Message}");
        }
    }

    private void RestoreUnderlyingContent()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            if (currentPage is Foodbook.Views.AddRecipePage addRecipePage)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    addRecipePage.Opacity = 1;
                    System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Restored AddRecipePage visibility (Opacity=1)");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Failed to restore underlying content: {ex.Message}");
        }
    }
}
