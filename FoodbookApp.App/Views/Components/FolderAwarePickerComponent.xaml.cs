using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;

namespace Foodbook.Views.Components;

public partial class FolderAwarePickerComponent : ContentView, INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IFolderService _folderService;
    
    private List<Recipe> _allRecipes = new();
    private List<Folder> _allFolders = new();
    private bool _isPopupOpen = false; // Protection against multiple opens

    public static readonly BindableProperty SelectedRecipeProperty = 
        BindableProperty.Create(nameof(SelectedRecipe), typeof(Recipe), typeof(FolderAwarePickerComponent), 
            defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnSelectedRecipeChanged);

    public static readonly BindableProperty PlaceholderTextProperty = 
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(FolderAwarePickerComponent), "Select recipe...");

    public ICommand OpenSelectionDialogCommand { get; }

    public Recipe? SelectedRecipe
    {
        get => (Recipe?)GetValue(SelectedRecipeProperty);
        set => SetValue(SelectedRecipeProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string DisplayText => SelectedRecipe?.Name ?? PlaceholderText;

    public FolderAwarePickerComponent()
    {
        // Get services from DI container
        _recipeService = IPlatformApplication.Current?.Services?.GetService<IRecipeService>() 
                        ?? throw new InvalidOperationException("IRecipeService not found");
        _folderService = IPlatformApplication.Current?.Services?.GetService<IFolderService>() 
                        ?? throw new InvalidOperationException("IFolderService not found");

        OpenSelectionDialogCommand = new Command(async () => await OpenSelectionDialog(), () => !_isPopupOpen);
        
        InitializeComponent();
        _ = LoadDataAsync();
    }

    private static void OnSelectedRecipeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FolderAwarePickerComponent component)
        {
            component.OnPropertyChanged(nameof(DisplayText));
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _allRecipes = await _recipeService.GetRecipesAsync();
            _allFolders = await _folderService.GetFoldersAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data in FolderAwarePickerComponent: {ex.Message}");
        }
    }

    private async Task OpenSelectionDialog()
    {
        // Protection against multiple opens
        if (_isPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Popup already open, ignoring request");
            return;
        }

        try
        {
            _isPopupOpen = true;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();

            // Create and show the popup
            var popup = new FolderAwarePickerPopup(_allRecipes, _allFolders);
            
            // Show the popup and get the result
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null)
            {
                await Shell.Current.DisplayAlert("Error", "Unable to resolve current page.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Opening popup");

            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            
            // Wait for either the popup to be dismissed or a result to be set
            await Task.WhenAny(showTask, resultTask);
            
            // Get the result
            var result = resultTask.IsCompleted ? await resultTask : null;
            
            System.Diagnostics.Debug.WriteLine($"? FolderAwarePickerComponent: Popup result: {result?.GetType().Name}");
            
            // Handle the result
            if (result is Recipe selectedRecipe)
            {
                SelectedRecipe = selectedRecipe;
            }
            else if (result == null)
            {
                // User cleared selection or cancelled
                // Keep current selection for cancel, clear for explicit clear
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? FolderAwarePickerComponent: Error opening popup: {ex.Message}");
            
            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Attempting to close any existing modal pages");
                
                try
                {
                    // Try to dismiss any existing modal pages
                    while (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("? FolderAwarePickerComponent: Modal stack cleared");
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"?? FolderAwarePickerComponent: Could not clear modal stack: {modalEx.Message}");
                }
            }
            
            // Fallback to display alert
            await Shell.Current.DisplayAlert("Error", "Could not open recipe selection dialog", "OK");
        }
        finally
        {
            _isPopupOpen = false;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Popup protection released");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        base.OnPropertyChanged(propertyName);
    }
}