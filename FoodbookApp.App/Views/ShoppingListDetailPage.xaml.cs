using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Foodbook.Views.Components;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "id")]
public partial class ShoppingListDetailPage : ContentPage
{
    private readonly ShoppingListDetailViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;

    // ? Debouncing support for text changes (disabled for auto-save)
    private CancellationTokenSource? _debounceCts;
    private const int DebounceDelayMs = 300;

    // ? Page lifecycle cancellation token
    private CancellationTokenSource? _pageCts;

    public int PlanId { get; set; }

    // Shell navigation handling
    private bool _isSubscribedToShellNavigating = false;
    private bool _suppressShellNavigating = false;

    // Popup state control to avoid reload after closing unit picker
    private bool _skipNextAppearReload = false;
    private bool _popupSubscribed = false;

    public ShoppingListDetailPage(ShoppingListDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();

        // ? CRITICAL: Subscribe to SaveCompletedAsync event to navigate back after save
        try
        {
            _viewModel.SaveCompletedAsync += OnSaveCompletedAsync;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Subscribed to SaveCompletedAsync event");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe SaveCompletedAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// ? NEW: Handler for SaveCompletedAsync event - navigates back to ShoppingListPage
    /// </summary>
    private async Task OnSaveCompletedAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] OnSaveCompletedAsync - navigating back");
            
            // Small delay to ensure UI updates before navigation
            await Task.Delay(100);
            
            // Suppress shell navigating handler during this navigation
            _suppressShellNavigating = true;
            
            try
            {
                await Shell.Current.GoToAsync("..");
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Successfully navigated back after save");
            }
            finally
            {
                await Task.Delay(200);
                _suppressShellNavigating = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Navigation error after save: {ex.Message}");
        }
    }

    private void OnGlobalPickerPopupStateChanged(object? sender, bool isOpen)
    {
        try
        {
            // When popup closes, skip the next OnAppearing-triggered reload
            if (!isOpen)
            {
                _skipNextAppearReload = true;
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Picker popup closed - will skip next reload on appearing");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] GlobalPopupStateChanged handler error: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        _pageCts = new CancellationTokenSource();
        _themeHelper.Initialize();

        // Subscribe Shell navigating for unsaved changes prompt
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
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe Shell.Navigating: {ex.Message}");
        }
        
        try
        {
            if (_skipNextAppearReload)
            {
                // Clear the flag and do not reload data (preserve in-memory edits like Unit changes)
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Skipping reload on appearing (popup just closed)");
                _skipNextAppearReload = false;
            }
            else
            {
                await _viewModel.LoadAsync(PlanId);
            }
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Load cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Load error: {ex.Message}");
            await DisplayAlert("B³¹d", "Nie mo¿na za³adowaæ listy zakupów", "OK");
        }

        // Subscribe to global popup state changes to avoid reload when unit picker closes
        try
        {
            if (!_popupSubscribed)
            {
                SimplePicker.GlobalPopupStateChanged += OnGlobalPickerPopupStateChanged;
                _popupSubscribed = true;
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Subscribed to GlobalPopupStateChanged");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe GlobalPopupStateChanged: {ex.Message}");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pageCts = null;

        _themeHelper.Cleanup();

        // Unsubscribe Shell navigating
        try
        {
            if (_isSubscribedToShellNavigating && Shell.Current != null)
            {
                Shell.Current.Navigating -= OnShellNavigating;
                _isSubscribedToShellNavigating = false;
            }
        }
        catch { }

        // Unsubscribe popup event to avoid leaks
        try
        {
            if (_popupSubscribed)
            {
                SimplePicker.GlobalPopupStateChanged -= OnGlobalPickerPopupStateChanged;
                _popupSubscribed = false;
            }
        }
        catch { }

        // ? CRITICAL: Unsubscribe SaveCompletedAsync to avoid memory leaks
        try
        {
            _viewModel.SaveCompletedAsync -= OnSaveCompletedAsync;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Unsubscribed from SaveCompletedAsync event");
        }
        catch { }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            if (_viewModel.HasUnsavedChanges)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool leave = await DisplayAlert("Potwierdzenie", "Masz niezapisane zmiany. Czy chcesz opuœciæ stronê bez zapisu?", "Tak", "Nie");
                    if (leave)
                    {
                        try { _viewModel.DiscardChanges(); } catch { }
                        _suppressShellNavigating = true;
                        try { await Shell.Current.GoToAsync("..", false); } catch { }
                        finally { await Task.Delay(200); _suppressShellNavigating = false; }
                    }
                });
                return true; // block default behavior
            }

            SafeNavigateBack();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] BackButton error: {ex.Message}");
            return base.OnBackButtonPressed();
        }
    }

    private void SafeNavigateBack()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Navigation error: {ex.Message}");
            }
        });
    }

    // Shell navigation handler
    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        try
        {
            if (_suppressShellNavigating) return;
            if (e.Source == ShellNavigationSource.Push) return;

            if (_viewModel.HasUnsavedChanges)
            {
                e.Cancel();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool leave = await DisplayAlert("Potwierdzenie", "Masz niezapisane zmiany. Czy chcesz opuœciæ stronê bez zapisu?", "Tak", "Nie");
                    if (leave)
                    {
                        try { _viewModel.DiscardChanges(); } catch { }
                        _suppressShellNavigating = true;
                        try
                        {
                            var targetLoc = e.Target?.Location?.OriginalString ?? string.Empty;
                            if (!string.IsNullOrEmpty(targetLoc))
                                await Shell.Current.GoToAsync(targetLoc);
                            else
                                await Shell.Current.GoToAsync("..", false);
                        }
                        catch (Exception navEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Navigation after discard failed: {navEx.Message}");
                        }
                        finally
                        {
                            await Task.Delay(200);
                            _suppressShellNavigating = false;
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] OnShellNavigating error: {ex.Message}");
        }
    }

    private void OnEntryFocused(object sender, EventArgs e)
    {
        _viewModel.IsEditing = true;
    }

    private void OnEntryUnfocused(object sender, EventArgs e)
    {
        _viewModel.IsEditing = false;
    }

    private void OnUnitPickerSelectionChanged(object? sender, EventArgs e)
    {
        try
        {
            // Extract ingredient from component's BindingContext
            if (sender is ShoppingListItemComponent component && component.BindingContext is Ingredient ing)
            {
                _viewModel.ChangeUnit(ing, ing.Unit);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] OnUnitPickerSelectionChanged error: {ex.Message}");
        }
    }
}
