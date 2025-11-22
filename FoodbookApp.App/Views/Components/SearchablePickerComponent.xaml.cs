using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;

namespace Foodbook.Views.Components;

public partial class SearchablePickerComponent : ContentView, INotifyPropertyChanged
{
    private bool _isPopupOpen = false; // Protection against multiple opens

    // Static event that SearchablePickerComponent instances can fire to notify AddRecipePage (or any page) about popup state
    public static event EventHandler<bool>? GlobalPopupStateChanged;

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
            // ? Notify global event listeners
            GlobalPopupStateChanged?.Invoke(this, true);
            System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Notified popup opening");

            _isPopupOpen = true;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();

            // ? KRYTYCZNA OPTYMALIZACJA: U¿yj lightweight metody tylko z nazwami
            List<string> options;
            try
            {
                var svc = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IIngredientService>();
                if (svc != null)
                {
                    System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Fetching ingredient names (lightweight)...");
                    
                    // ? U¿yj nowej metody GetIngredientNamesAsync zamiast pe³nego GetIngredientsAsync
                    options = await svc.GetIngredientNamesAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"? SearchablePickerComponent: Fetched {options.Count} ingredient names");
                }
                else
                {
                    options = (ItemsSource?.ToList() ?? new List<string>());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? SearchablePickerComponent: Error fetching ingredients: {ex.Message}");
                options = (ItemsSource?.ToList() ?? new List<string>());
            }

            var popup = new SearchablePickerPopup(options, SelectedItem);
            
            // Get the current page to show popup on
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null)
            {
                await Shell.Current.DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.UnableToResolveCurrentPage, FoodbookApp.Localization.AddRecipePageResources.OKButton);
                return;
            }

            System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Opening popup");

            // Show popup and await the popup's own ResultTask to get object result
            page.ShowPopup(popup);
            var result = await popup.ResultTask;
            
            System.Diagnostics.Debug.WriteLine($"? SearchablePickerComponent: Popup result: {result}");
            
            if (result is string selected)
            {
                SelectedItem = selected;
            }
            // else keep current selection on cancel/close
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? SearchablePickerComponent: Error opening popup: {ex.Message}");
            await Shell.Current.DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.CouldNotOpenRecipeSelectionDialog, FoodbookApp.Localization.AddRecipePageResources.OKButton);
        }
        finally
        {
            GlobalPopupStateChanged?.Invoke(this, false);
            System.Diagnostics.Debug.WriteLine("?? SearchablePickerComponent: Notified popup closing");

            _isPopupOpen = false;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("? SearchablePickerComponent: Popup protection released");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
