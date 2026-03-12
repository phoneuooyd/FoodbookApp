using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using System.Threading.Tasks;
using System;

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class IngredientFormPage : ContentPage
{
    private IngredientFormViewModel ViewModel => BindingContext as IngredientFormViewModel ?? throw new InvalidOperationException("ViewModel not set");
    private const double KeyboardLiftOffset = 213;
    private bool _isKeyboardLiftApplied;
    private double _lastAllocatedHeight;
    private bool _keyboardWasVisible;
    
    // ? CRITICAL: Track whether we're awaiting load to prevent race conditions
    private Task? _loadTask;

    public IngredientFormPage(IngredientFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Constructor called");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] OnAppearing");
        
        // ? CRITICAL: Wait for any pending load operation to complete before showing the page
        if (_loadTask != null)
        {
            try
            {
                await _loadTask;
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Load task completed in OnAppearing");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error awaiting load task: {ex.Message}");
            }
        }
    }

    private Guid _itemId;

    public string? ItemId
    {
        get => _itemId == Guid.Empty ? null : _itemId.ToString();
        set
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] ItemId setter called with value: '{value}'");
            
            if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Valid GUID parsed for EDIT mode: {parsed}");

                // If same ID is set again (for example after returning from a popup),
                // avoid reloading the ingredient which would overwrite user changes made in-place.
                if (_itemId == parsed)
                {
                    // If there is an in-flight load that hasn't completed, we allow it to continue.
                    if (_loadTask != null && !_loadTask.IsCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Same ID received and load in progress - waiting for existing load");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Same ID received - skipping reload to avoid overwriting UI state");
                        return;
                    }
                }

                _itemId = parsed;
                
                // ? CRITICAL: Store the load task so OnAppearing can await it
                // This ensures the form is fully loaded before being displayed
                _loadTask = LoadIngredientAsync(parsed);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[IngredientFormPage] No valid ID - NEW ingredient mode");
                _itemId = Guid.Empty;
                
                // ? CRITICAL: Reset the form for new ingredient mode
                _loadTask = Task.Run(() =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            ViewModel.Reset();
                            System.Diagnostics.Debug.WriteLine("[IngredientFormPage] Form reset for new ingredient");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error resetting form: {ex.Message}");
                        }
                    });
                });
            }
        }
    }

    private async Task LoadIngredientAsync(Guid id)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] LoadIngredientAsync started for ID: {id}");
            await ViewModel.LoadAsync(id);
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] LoadIngredientAsync completed for ID: {id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] Error loading ingredient: {ex.Message}");
            
            // Show error to user on main thread
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert(
                    "B³¹d ³adowania", 
                    $"Nie uda³o siê za³adowaæ sk³adnika: {ex.Message}", 
                    "OK");
            });
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        try
        {
            _isKeyboardLiftApplied = false;
            ContentHost.TranslationY = 0;
        }
        catch { }
    }

    protected override bool OnBackButtonPressed()
    {
        if (ViewModel?.CancelCommand?.CanExecute(null) == true)
            ViewModel.CancelCommand.Execute(null);
        return true;
    }

    private void OnInputFocused(object sender, FocusEventArgs e)
    {
        _ = EnsureKeyboardSafeOffsetAsync(sender as Element);
    }

    private void OnInputUnfocused(object sender, FocusEventArgs e)
    {
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

            var isNearBottom = sourceY > pageHeight * 0.62;
            if (!isNearBottom || _isKeyboardLiftApplied)
                return;

            _isKeyboardLiftApplied = true;
            await ContentHost.TranslateTo(0, -KeyboardLiftOffset, 180, Easing.CubicOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] EnsureKeyboardSafeOffsetAsync error: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] ResetKeyboardSafeOffsetAsync error: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnSizeAllocated keyboard detection error: {ex.Message}");
        }
    }
}
