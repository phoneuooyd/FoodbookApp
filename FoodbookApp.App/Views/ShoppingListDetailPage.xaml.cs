using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

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

    public ShoppingListDetailPage(ShoppingListDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _themeHelper = new PageThemeHelper();
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
            await _viewModel.LoadAsync(PlanId);
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
        
        // Auto-save disabled intentionally – manual save button only
        // if (!_viewModel.IsEditing) { await SaveAllStatesSafelyAsync(); }
    }

    private async Task SaveAllStatesSafelyAsync()
    {
        try
        {
            await _viewModel.SaveAllStatesAsync();
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[ShoppingListDetailPage] Save cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Save error: {ex.Message}");
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
                    bool leave = await DisplayAlert("Potwierdzenie", "Masz niezapisane zmiany. Czy chcesz opuœciæ stronê bez zapisu?", "Tak", "Nie");
                    if (leave)
                    {
                        try { _viewModel.DiscardChanges(); } catch { }
                        _suppressShellNavigating = true;
                        try { await Shell.Current.GoToAsync("..", false); } catch { }
                        finally { await Task.Delay(200); _suppressShellNavigating = false; }
                    }
                });
                return true; // blokujemy domyœlne zachowanie
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

    // Shell navigation handler – similar logic to AddRecipePage
    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        try
        {
            if (_suppressShellNavigating) return;
            if (e.Source == ShellNavigationSource.Push) return; // wchodzimy na now¹ stronê

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

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        _viewModel.IsEditing = true;
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        _viewModel.IsEditing = false;
        // Auto-save disabled intentionally
    }

    private void OnEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        // Auto-save disabled intentionally
    }

    private void SaveItemWithDebounceAsync(Ingredient ingredient)
    {
        // Auto-save disabled deliberately – method retained for future use
    }

    private void OnDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            e.Data.Properties["SourceItem"] = ing;
        }
    }

    private void OnTopInsertDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertBefore = true;
        }
    }

    private void OnTopInsertDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertBefore = false;
        }
    }

    private async void OnTopInsertDrop(object? sender, DropEventArgs e)
    {
        await HandleDropAsync(sender, e, insertAfter: false);
    }

    private void OnBottomInsertDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertAfter = true;
        }
    }

    private void OnBottomInsertDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is VisualElement el && el.BindingContext is Ingredient ing)
        {
            ing.ShowInsertAfter = false;
        }
    }

    private async void OnBottomInsertDrop(object? sender, DropEventArgs e)
    {
        await HandleDropAsync(sender, e, insertAfter: true);
    }

    private async Task HandleDropAsync(object? sender, DropEventArgs e, bool insertAfter)
    {
        try
        {
            if (!e.Data.Properties.TryGetValue("SourceItem", out var src)) return;
            if (src is not Ingredient dragged) return;
            if (sender is not VisualElement el || el.BindingContext is not Ingredient target) return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                target.ShowInsertBefore = false;
                target.ShowInsertAfter = false;
                await _viewModel.ReorderItemsAsync(dragged, target, insertAfter);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShoppingListDetailPage] Drop error: {ex.Message}");
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("B³¹d", "Nie mo¿na zmieniæ kolejnoœci elementu", "OK");
            });
        }
    }
}
