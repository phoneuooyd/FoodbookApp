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

        OpenSelectionDialogCommand = new Command(async () => await OpenSelectionDialog());
        
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
        try
        {
            // Create and show the popup
            var popup = new FolderAwarePickerPopup(_allRecipes, _allFolders);
            
            // Show the popup and get the result
            var showTask = Application.Current.MainPage.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            
            // Wait for either the popup to be dismissed or a result to be set
            await Task.WhenAny(showTask, resultTask);
            
            // Get the result
            var result = resultTask.IsCompleted ? await resultTask : null;
            
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
            System.Diagnostics.Debug.WriteLine($"Error opening recipe selection dialog: {ex.Message}");
            // Fallback to display alert
            await Shell.Current.DisplayAlert("Error", "Could not open recipe selection dialog", "OK");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}