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

    // Page lifecycle cancellation token
    private CancellationTokenSource? _pageCts;

    private Guid _planIdGuid;

    public string? PlanId
    {
        get => _planIdGuid == Guid.Empty ? null : _planIdGuid.ToString();
        set
        {
            if (Guid.TryParse(value, out var parsed))
            {
                _planIdGuid = parsed;
            }
            else
            {
                _planIdGuid = Guid.Empty;
                System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Invalid plan id query parameter: '{value}'");
            }
        }
    }

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

        // Subscribe to SaveCompletedAsync event to navigate back after save
        try
        {
            _viewModel.SaveCompletedAsync += OnSaveCompletedAsync;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Subscribed to SaveCompletedAsync event");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe SaveCompletedAsync: {ex.Message}");
        }

        // Subscribe to global popup state changes early (before any interaction)
        try
        {
            SimplePicker.GlobalPopupStateChanged += OnGlobalPickerPopupStateChanged;
            _popupSubscribed = true;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Subscribed to GlobalPopupStateChanged in constructor");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe GlobalPopupStateChanged: {ex.Message}");
        }
    }

    /// <summary>
    /// Handler for SaveCompletedAsync event - navigates back to ShoppingListPage
    /// </summary>
    private async Task OnSaveCompletedAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] OnSaveCompletedAsync - navigating back");

            await Task.Delay(100);
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
            // Set flag when popup OPENS (true) to prevent reload during popup interaction
            // AND when popup CLOSES (false) to skip reload after popup closes
            if (isOpen)
            {
                _skipNextAppearReload = true;
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Picker popup opened - setting skip reload flag");
            }
            else
            {
                // Keep the flag set when popup closes - it will be consumed in OnAppearing
                _skipNextAppearReload = true;
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Picker popup closed - keeping skip reload flag");
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

        // Re-subscribe to popup events if needed (may have been unsubscribed in OnDisappearing)
        try
        {
            if (!_popupSubscribed)
            {
                SimplePicker.GlobalPopupStateChanged += OnGlobalPickerPopupStateChanged;
                _popupSubscribed = true;
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Re-subscribed to GlobalPopupStateChanged");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe GlobalPopupStateChanged: {ex.Message}");
        }

        try
        {
            if (_skipNextAppearReload)
            {
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Skipping reload on appearing (popup interaction)");
                _skipNextAppearReload = false;
                return; // Don't load - just skip
            }
            
            if (_planIdGuid == Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] PlanId is empty/invalid - skipping load");
            }
            else
            {
                await _viewModel.LoadAsync(_planIdGuid);
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
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

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

        // NOTE: Do NOT unsubscribe popup event here - we need it to set skip flag
        // when popup closes. The popup may cause OnDisappearing to fire.
        // Unsubscription is done only on page destruction or explicit navigation away.
        
        // Don't unsubscribe SaveCompletedAsync - it's needed for save flow
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
                return true;
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
                        catch { }
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

    #region Component Event Handlers

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

    #endregion
}
