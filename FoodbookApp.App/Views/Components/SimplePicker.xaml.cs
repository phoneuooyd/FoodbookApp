using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Foodbook.Utils;
using FoodbookApp.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Foodbook.Views.Components;

public partial class SimplePicker : ContentView, INotifyPropertyChanged
{
    private bool _isPopupOpen = false; // Protection against multiple opens
    private ILocalizationService? _localizationService; // Localization service subscription

    // Static event that SimplePicker instances can fire to notify AddRecipePage (or any page) about popup state
    public static event EventHandler<bool>? GlobalPopupStateChanged;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SimplePicker), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SimplePicker), null, BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public static readonly BindableProperty ItemDisplayBindingProperty =
        BindableProperty.Create(nameof(ItemDisplayBinding), typeof(BindingBase), typeof(SimplePicker), null);

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(SimplePicker), "Wybierz...");

    public static readonly BindableProperty PlaceholderTextProperty =
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(SimplePicker), "Wybierz...");

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set
        {
            System.Diagnostics.Debug.WriteLine($"[SimplePicker] SelectedItem setter called. Old: {GetValue(SelectedItemProperty)}, New: {value}");
            SetValue(SelectedItemProperty, value);
            
            // Property change notifications are handled by OnSelectedItemChanged callback
        }
    }

    public BindingBase? ItemDisplayBinding
    {
        get => (BindingBase?)GetValue(ItemDisplayBindingProperty);
        set => SetValue(ItemDisplayBindingProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string DisplayText
    {
        get
        {
            if (SelectedItem == null)
                return PlaceholderText;

            // Try ItemDisplayBinding converter first (with current UI culture)
            if (ItemDisplayBinding is Binding binding && binding.Converter != null)
            {
                try
                {
                    var converted = binding.Converter.Convert(SelectedItem, typeof(string), binding.ConverterParameter, CultureInfo.CurrentUICulture);
                    var convertedText = converted?.ToString();
                    if (!string.IsNullOrWhiteSpace(convertedText))
                        return convertedText!;
                }
                catch
                {
                    // Fallback below
                }
            }

            // Fallback: enum display attributes
            if (SelectedItem is Enum e)
            {
                var name = e.GetDisplayName();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }

            return SelectedItem?.ToString() ?? PlaceholderText;
        }
    }

    public ICommand OpenSelectionDialogCommand { get; }

    // Raised when SelectedItem changes (for XAML event hookup)
    public event EventHandler? SelectionChanged;

    public SimplePicker()
    {
        OpenSelectionDialogCommand = new Command(async () => await OpenSelectionDialog(), () => !_isPopupOpen);
        InitializeComponent();
        SubscribeLocalization();
    }

    private void SubscribeLocalization()
    {
        try
        {
            var sp = FoodbookApp.MauiProgram.ServiceProvider;
            _localizationService = sp?.GetService<ILocalizationService>();
            if (_localizationService != null)
            {
                _localizationService.CultureChanged += OnCultureChanged;
                // Subscribe to explicit picker refresh requests
                _localizationService.PickerRefreshRequested += OnPickerRefreshRequested;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimplePicker] Failed to subscribe localization: {ex.Message}");
        }
    }

    private void UnsubscribeLocalization()
    {
        try
        {
            if (_localizationService != null)
            {
                _localizationService.CultureChanged -= OnCultureChanged;
                _localizationService.PickerRefreshRequested -= OnPickerRefreshRequested;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimplePicker] Failed to unsubscribe localization: {ex.Message}");
        }
    }

    private void OnPickerRefreshRequested(object? sender, EventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Force refresh of display text and any consumer listening to selection text changes
                OnPropertyChanged(nameof(DisplayText));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimplePicker] OnPickerRefreshRequested error: {ex.Message}");
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Recompute text using ItemDisplayBinding converter with new culture
                OnPropertyChanged(nameof(DisplayText));
                // Notify any listeners that selection visual text changed
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SimplePicker] OnCultureChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// Determine display text for an option, honoring ItemDisplayBinding and falling back to Enum Display attributes.
    /// Always uses the current UI culture at call time.
    /// </summary>
    private string GetOptionText(object? item)
    {
        if (item == null) return string.Empty;

        // Prefer ItemDisplayBinding converter if present
        if (ItemDisplayBinding is Binding binding && binding.Converter != null)
        {
            try
            {
                var converted = binding.Converter.Convert(item, typeof(string), binding.ConverterParameter, CultureInfo.CurrentUICulture);
                var text = converted?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text!;
            }
            catch
            {
                // Ignore and fallback below
            }
        }

        // Fallback for enums via Display attributes (resx-backed)
        if (item is Enum e)
        {
            var name = e.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return item.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Refresh the DisplayText - useful when culture/language changes
    /// </summary>
    public void RefreshDisplayText()
    {
        OnPropertyChanged(nameof(DisplayText));
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SimplePicker picker)
        {
            picker.OnPropertyChanged(nameof(DisplayText));
            picker.SelectionChanged?.Invoke(picker, EventArgs.Empty);
            
            // ? SIMPLIFIED: Only propagate to Ingredient model (for shopping lists)
            // IngredientFormViewModel now handles unit changes internally with immediate DB save
            try
            {
                if (newValue != oldValue && newValue is Enum enumValue)
                {
                    // Case: Ingredient model bound in lists (ShoppingList/Recipe ingredients)
                    if (picker.BindingContext is Foodbook.Models.Ingredient ingredient)
                    {
                        if (enumValue is Foodbook.Models.Unit unit)
                        {
                            var oldUnit = ingredient.Unit;
                            ingredient.Unit = unit; // raises PropertyChanged in Ingredient
                            System.Diagnostics.Debug.WriteLine($"[SimplePicker] Ingredient.Unit changed: {oldUnit} -> {unit}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SimplePicker] Failed to propagate SelectedItem: {ex.Message}");
            }
        }
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (Parent == null)
        {
            UnsubscribeLocalization();
        }
    }

    private async Task OpenSelectionDialog()
    {
        // Protection against multiple opens
        if (_isPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? SimplePicker: Popup already open, ignoring request");
            return;
        }

        try
        {
            // ?? Notify global event listeners
            GlobalPopupStateChanged?.Invoke(this, true);
            System.Diagnostics.Debug.WriteLine("?? SimplePicker: Notified popup opening");

            _isPopupOpen = true;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();

            // IMPORTANT: Build option list using CURRENT UI culture at open time
            var options = ItemsSource?.Cast<object>().Select(GetOptionText).ToList() ?? new List<string>();

            var currentSelection = DisplayText == PlaceholderText ? null : DisplayText;
            
            System.Diagnostics.Debug.WriteLine($"[SimplePicker] Opening selection dialog. Current selection: {currentSelection}, SelectedItem: {SelectedItem}");
            
            var popup = new SearchablePickerPopup(options, currentSelection);

            // Get the current page to show popup on
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null)
            {
                await Shell.Current.DisplayAlert("Error", "Unable to resolve current page.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("?? SimplePicker: Opening popup");

            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;

            // Wait for either the popup to be dismissed or a result to be set
            await Task.WhenAny(showTask, resultTask);

            // Get the result
            var result = resultTask.IsCompleted ? await resultTask : null;

            System.Diagnostics.Debug.WriteLine($"? SimplePicker: Popup result: {result}");

            // Handle the result
            if (result is string selectedText && !string.IsNullOrEmpty(selectedText))
            {
                // Find the corresponding object in ItemsSource by recomputing display text with current culture
                var matchingItem = ItemsSource?.Cast<object>().FirstOrDefault(item => GetOptionText(item) == selectedText);

                if (matchingItem != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SimplePicker] Setting SelectedItem from '{SelectedItem}' to '{matchingItem}'");
                    
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        // Update picker selected item
                        SelectedItem = matchingItem;
                        OnPropertyChanged(nameof(SelectedItem));
                        OnPropertyChanged(nameof(DisplayText));
                        
                        // ? SIMPLIFIED: Only explicit propagation for Ingredient lists
                        // IngredientFormViewModel handles unit changes via setter with immediate DB save
                        try
                        {
                            if (BindingContext is Foodbook.Models.Ingredient ingredient && matchingItem is Foodbook.Models.Unit unit)
                            {
                                var oldUnit = ingredient.Unit;
                                ingredient.Unit = unit;
                                System.Diagnostics.Debug.WriteLine($"[SimplePicker] Ingredient.Unit changed (popup): {oldUnit} -> {unit}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SimplePicker] Failed to set Unit on BindingContext: {ex.Message}");
                        }
                    });
                    
                    // Give binding system time to propagate changes
                    await Task.Delay(50);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SimplePicker] ?? Could not find matching item for selected text: {selectedText}");
                }
            }
            else if (result == null)
            {
                System.Diagnostics.Debug.WriteLine("[SimplePicker] User cancelled or closed popup - keeping current selection");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? SimplePicker: Error opening popup: {ex.Message}");

            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? SimplePicker: Attempting to close any existing modal pages");

                try
                {
                    // Try to dismiss any existing modal pages
                    while (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }

                    System.Diagnostics.Debug.WriteLine("? SimplePicker: Modal stack cleared");
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"? SimplePicker: Could not clear modal stack: {modalEx.Message}");
                }
            }

            // Fallback to display alert
            await Shell.Current.DisplayAlert("Error", "Could not open selection dialog", "OK");
        }
        finally
        {
            GlobalPopupStateChanged?.Invoke(this, false);
            System.Diagnostics.Debug.WriteLine("?? SimplePicker: Notified popup closing");

            _isPopupOpen = false;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? SimplePicker: Popup protection released");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
