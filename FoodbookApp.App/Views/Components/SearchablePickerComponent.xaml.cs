using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;

namespace Foodbook.Views.Components;

public partial class SearchablePickerComponent : ContentView, INotifyPropertyChanged
{
    private bool _isPopupOpen = false; // Protection against multiple opens

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList<string>), typeof(SearchablePickerComponent), default(IList<string>));

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(string), typeof(SearchablePickerComponent), default(string), BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public static readonly BindableProperty PlaceholderTextProperty =
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(SearchablePickerComponent), "Wybierz...");

    public IList<string>? ItemsSource
    {
        get => (IList<string>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedItem
    {
        get => (string?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string DisplayText => string.IsNullOrWhiteSpace(SelectedItem) ? PlaceholderText : SelectedItem!;

    public ICommand OpenSelectionDialogCommand { get; }

    // Raised when SelectedItem changes (for XAML event hookup)
    public event EventHandler? SelectionChanged;

    public SearchablePickerComponent()
    {
        OpenSelectionDialogCommand = new Command(async () => await OpenSelectionDialog(), () => !_isPopupOpen);
        InitializeComponent();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SearchablePickerComponent component)
        {
            component.OnPropertyChanged(nameof(DisplayText));
            component.SelectionChanged?.Invoke(component, EventArgs.Empty);
        }
    }

    private async Task OpenSelectionDialog()
    {
        // Protection against multiple opens
        if (_isPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Popup already open, ignoring request");
            return;
        }

        try
        {
            _isPopupOpen = true;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();

            var options = ItemsSource?.ToList() ?? new List<string>();
            var popup = new SearchablePickerPopup(options, SelectedItem);
            
            // Get the current page to show popup on
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null)
            {
                await Shell.Current.DisplayAlert("Error", "Unable to resolve current page.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Opening popup");

            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            
            // Wait for either the popup to be dismissed or a result to be set
            await Task.WhenAny(showTask, resultTask);
            
            // Get the result
            var result = resultTask.IsCompleted ? await resultTask : null;
            
            System.Diagnostics.Debug.WriteLine($"? SearchablePickerComponent: Popup result: {result}");
            
            // Handle the result
            if (result is string selected)
            {
                SelectedItem = selected;
            }
            else if (result == null)
            {
                // User cancelled or closed popup - keep current selection
                // Do not reset or change current selection
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? SearchablePickerComponent: Error opening popup: {ex.Message}");
            
            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Attempting to close any existing modal pages");
                
                try
                {
                    // Try to dismiss any existing modal pages
                    while (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("? SearchablePickerComponent: Modal stack cleared");
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"?? SearchablePickerComponent: Could not clear modal stack: {modalEx.Message}");
                }
            }
            
            // Fallback to display alert
            await Shell.Current.DisplayAlert("Error", "Could not open selection dialog", "OK");
        }
        finally
        {
            _isPopupOpen = false;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Popup protection released");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
