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

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class IngredientFormPage : ContentPage
{
    private IngredientFormViewModel ViewModel => BindingContext as IngredientFormViewModel;
    private readonly PageThemeHelper _themeHelper;

    public IngredientFormPage(IngredientFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _themeHelper = new PageThemeHelper();

        // Subscribe to ViewModel events (if we expose Saved event)
        try
        {
            vm.PropertyChanged += OnVmPropertyChanged;
        }
        catch { }
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
            // Subscribe to SavedAsync by adding an async handler that sets the TCS
            ViewModel.SavedAsync += async () => { OnSavedHandler(); await Task.CompletedTask; };
        }

        this.Disappearing += OnPageDisappearing;

        // Timeout fallback
        var ct = new CancellationTokenSource(timeoutMs);
        ct.Token.Register(() => tcs.TrySetResult());

        return tcs.Task;
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IngredientFormViewModel.ValidationMessage))
        {
            // ignore
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
        _themeHelper.ThemeChanged += OnThemeChanged;
        _themeHelper.CultureChanged += OnCultureChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.ThemeChanged -= OnThemeChanged;
        _themeHelper.CultureChanged -= OnCultureChanged;
        _themeHelper.Cleanup();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            if (ViewModel == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Re-raise properties so BoolToColorConverter bindings recompute with new palette
                ViewModel.SelectedTabIndex = ViewModel.SelectedTabIndex; // updates Is*TabSelected
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
                
                // Force refresh of all SimplePicker controls by triggering property changes
                RefreshUnitPickers();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IngredientFormPage] OnCultureChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh all unit pickers by finding SimplePicker controls in the visual tree
    /// </summary>
    private void RefreshUnitPickers()
    {
        try
        {
            // Find all SimplePicker controls and trigger their DisplayText refresh
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

    /// <summary>
    /// Helper method to find all visual children of a specific type
    /// </summary>
    private static IEnumerable<T> FindVisualChildren<T>(Element element) where T : Element
    {
        if (element is T match)
            yield return match;

        // Special handling for TabComponent - search all tabs, not just the visible one
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

    protected override bool OnBackButtonPressed()
    {
        try
        {
            if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                ViewModel.CancelCommand.Execute(null);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnBackButtonPressed: {ex.Message}");
            return base.OnBackButtonPressed();
        }
    }
}
