using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;

namespace Foodbook.Views.Components;

public partial class SearchablePickerComponent : ContentView, INotifyPropertyChanged
{
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
        OpenSelectionDialogCommand = new Command(async () => await OpenSelectionDialog());
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
        try
        {
            var options = ItemsSource?.ToList() ?? new List<string>();
            var popup = new SearchablePickerPopup(options, SelectedItem);
            // Try to use the same pattern as FolderAwarePickerComponent to avoid extension method on Page
            var showTask = Application.Current!.MainPage!.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            await Task.WhenAny(showTask, resultTask);
            var result = resultTask.IsCompleted ? await resultTask : null;
            if (result is string selected)
            {
                SelectedItem = selected;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening SearchablePicker popup: {ex.Message}");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
