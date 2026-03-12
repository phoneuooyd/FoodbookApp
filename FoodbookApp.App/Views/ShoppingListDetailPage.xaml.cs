using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Foodbook.Views.Components;
using FoodbookApp.Localization;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "id")]
public partial class ShoppingListDetailPage : ContentPage
{
    private readonly ShoppingListDetailViewModel _viewModel;
    private readonly PageThemeHelper _themeHelper;
    private const double KeyboardLiftOffset = 213;
    private bool _isKeyboardLiftApplied;
    private double _lastAllocatedHeight;
    private bool _keyboardWasVisible;

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

        SubscribeToPopupStateChanged();
    }

    private void SubscribeToPopupStateChanged()
    {
        try
        {
            if (_popupSubscribed)
            {
                return;
            }

            SimplePicker.GlobalPopupStateChanged += OnGlobalPickerPopupStateChanged;
            _popupSubscribed = true;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Subscribed to GlobalPopupStateChanged");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to subscribe GlobalPopupStateChanged: {ex.Message}");
        }
    }

    private void UnsubscribeFromPopupStateChanged()
    {
        try
        {
            if (!_popupSubscribed)
            {
                return;
            }

            SimplePicker.GlobalPopupStateChanged -= OnGlobalPickerPopupStateChanged;
            _popupSubscribed = false;
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Unsubscribed from GlobalPopupStateChanged");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Failed to unsubscribe GlobalPopupStateChanged: {ex.Message}");
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

        SubscribeToPopupStateChanged();

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
            await DisplayAlert(GetShoppingListText("LoadErrorTitle", "Error"), GetShoppingListText("LoadErrorMessage", "Unable to load shopping list."), GetButtonText("OK", "OK"));
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        try
        {
            _isKeyboardLiftApplied = false;
            _keyboardWasVisible = false;
            _lastAllocatedHeight = 0;
            ContentHost.TranslationY = 0;
        }
        catch { }

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

        UnsubscribeFromPopupStateChanged();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        try
        {
            if (height <= 0)
                return;

            if (_lastAllocatedHeight <= 0)
            {
                _lastAllocatedHeight = height;
                return;
            }

            var delta = height - _lastAllocatedHeight;

            if (delta < -80)
            {
                _keyboardWasVisible = true;
            }
            else if (delta > 80 && _keyboardWasVisible)
            {
                _keyboardWasVisible = false;
                if (_isKeyboardLiftApplied)
                    _ = ResetKeyboardSafeOffsetAsync();
            }

            _lastAllocatedHeight = height;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] OnSizeAllocated keyboard detection error: {ex.Message}");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            if (_viewModel.HasUnsavedChanges)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    bool leave = await DisplayAlert(
                        GetShoppingListText("UnsavedChangesConfirmTitle", "Confirmation"),
                        GetShoppingListText("UnsavedChangesConfirmMessage", "You have unsaved changes. Do you want to leave this page without saving?"),
                        GetButtonText("Yes", "Yes"),
                        GetButtonText("No", "No"));
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
                    bool leave = await DisplayAlert(
                        GetShoppingListText("UnsavedChangesConfirmTitle", "Confirmation"),
                        GetShoppingListText("UnsavedChangesConfirmMessage", "You have unsaved changes. Do you want to leave this page without saving?"),
                        GetButtonText("Yes", "Yes"),
                        GetButtonText("No", "No"));
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

    private static string GetShoppingListText(string key, string fallback)
        => ShoppingListDetailPageResources.ResourceManager.GetString(key) ?? fallback;

    private static string GetButtonText(string key, string fallback)
        => ButtonResources.ResourceManager.GetString(key) ?? fallback;

    #region Component Event Handlers

    private void OnEntryFocused(object sender, EventArgs e)
    {
        _viewModel.IsEditing = true;
        _ = EnsureKeyboardSafeOffsetAsync(sender as Element);
    }

    private void OnEntryUnfocused(object sender, EventArgs e)
    {
        _viewModel.IsEditing = false;
        _ = ResetKeyboardSafeOffsetAsync();
    }
    
    private async Task EnsureKeyboardSafeOffsetAsync(Element? source)
    {
        try
        {
            await Task.Delay(80);

            var sourceY = GetElementYRelativeToPage(source);
            var pageHeight = Height;
            if (pageHeight <= 0 || sourceY <= 0)
                return;

            // Lift only when focus target is near bottom area.
            var isNearBottom = sourceY > pageHeight * 0.62;
            if (!isNearBottom)
                return;

            if (_isKeyboardLiftApplied)
                return;

            _isKeyboardLiftApplied = true;
            await ContentHost.TranslateTo(0, -KeyboardLiftOffset, 180, Easing.CubicOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] EnsureKeyboardSafeOffsetAsync error: {ex.Message}");
        }
    }

    private async Task ResetKeyboardSafeOffsetAsync()
    {
        try
        {
            if (!_isKeyboardLiftApplied)
                return;

            _isKeyboardLiftApplied = false;
            await ContentHost.TranslateTo(0, 0, 140, Easing.CubicOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] ResetKeyboardSafeOffsetAsync error: {ex.Message}");
        }
    }

    private static double GetElementYRelativeToPage(Element? element)
    {
        double y = 0;
        Element? current = element;
        while (current != null)
        {
            if (current is VisualElement ve)
                y += ve.Y;
            current = current.Parent;
        }
        return y;
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
