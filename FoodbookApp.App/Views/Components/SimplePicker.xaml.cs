using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using System.Collections; // Added for IEnumerable

namespace Foodbook.Views.Components;

public partial class SimplePicker : ContentView, INotifyPropertyChanged
{
    private bool _isPopupOpen = false; // Protection against multiple opens

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
        set => SetValue(SelectedItemProperty, value);
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

            // If ItemDisplayBinding is provided, use converter
            if (ItemDisplayBinding is Binding binding && binding.Converter != null)
            {
                try
                {
                    var converted = binding.Converter.Convert(SelectedItem, typeof(string), binding.ConverterParameter, System.Globalization.CultureInfo.CurrentCulture);
                    return converted?.ToString() ?? PlaceholderText;
                }
                catch
                {
                    // Fallback to ToString if conversion fails
                }
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
            _isPopupOpen = true;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();

            // Convert items to string list for SearchablePickerPopup
            var options = ItemsSource?.Cast<object>().Select(item =>
            {
                // Use ItemDisplayBinding converter if available
                if (ItemDisplayBinding is Binding binding && binding.Converter != null)
                {
                    try
                    {
                        var converted = binding.Converter.Convert(item, typeof(string), binding.ConverterParameter, System.Globalization.CultureInfo.CurrentCulture);
                        return converted?.ToString() ?? item?.ToString() ?? string.Empty;
                    }
                    catch
                    {
                        // Fallback to ToString if conversion fails
                    }
                }
                return item?.ToString() ?? string.Empty;
            }).ToList() ?? new List<string>();

            var currentSelection = DisplayText == PlaceholderText ? null : DisplayText;
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
                // Find the corresponding object in ItemsSource
                var matchingItem = ItemsSource?.Cast<object>().FirstOrDefault(item =>
                {
                    // Use ItemDisplayBinding converter if available
                    if (ItemDisplayBinding is Binding binding && binding.Converter != null)
                    {
                        try
                        {
                            var converted = binding.Converter.Convert(item, typeof(string), binding.ConverterParameter, System.Globalization.CultureInfo.CurrentCulture);
                            return converted?.ToString() == selectedText;
                        }
                        catch
                        {
                            // Fallback to ToString comparison
                        }
                    }
                    return item?.ToString() == selectedText;
                });

                if (matchingItem != null)
                {
                    SelectedItem = matchingItem;
                }
            }
            else if (result == null)
            {
                // User cancelled or closed popup - keep current selection
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
                    System.Diagnostics.Debug.WriteLine($"?? SimplePicker: Could not clear modal stack: {modalEx.Message}");
                }
            }

            // Fallback to display alert
            await Shell.Current.DisplayAlert("Error", "Could not open selection dialog", "OK");
        }
        finally
        {
            _isPopupOpen = false;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? SimplePicker: Popup protection released");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
