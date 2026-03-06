using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions;
using FoodbookApp.Interfaces;

namespace Foodbook.Views.Components;

public partial class FolderAwarePickerComponent : ContentView, INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IFolderService _folderService;
    
    private List<Recipe> _allRecipes = new();
    private List<Folder> _allFolders = new();
    private bool _isPopupOpen = false; // Protection against multiple opens
    private bool _isDetailsPopupOpen = false; // Protection for details popup
    private bool _suppressTap = false; // Suppress tap when long-press/details popup is active

    public static readonly BindableProperty SelectedRecipeProperty = 
        BindableProperty.Create(nameof(SelectedRecipe), typeof(Recipe), typeof(FolderAwarePickerComponent), 
            defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnSelectedRecipeChanged);

    public static readonly BindableProperty PlaceholderTextProperty = 
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(FolderAwarePickerComponent), "Select recipe...", propertyChanged: OnPlaceholderChanged);

    public ICommand OpenSelectionDialogCommand { get; }
    public ICommand ShowRecipeDetailsCommand { get; }

    // Event fired when recipe selection changes
    public event EventHandler? SelectionChanged;

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

        // Note: CanExecute now also checks suppression state so tap won't fire while details popup is active
        OpenSelectionDialogCommand = new Command(async () => await OpenSelectionDialog(), () => !_isPopupOpen && !_isDetailsPopupOpen && !_suppressTap);
        ShowRecipeDetailsCommand = new Command<Recipe>(async (recipe) => await ShowRecipeDetailsAsync(recipe), (recipe) => !_isDetailsPopupOpen && recipe != null);
        
        InitializeComponent();
        _ = LoadDataAsync();
    }

    private static void OnSelectedRecipeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FolderAwarePickerComponent component)
        {
            component.OnPropertyChanged(nameof(DisplayText));
            component.SelectionChanged?.Invoke(component, EventArgs.Empty);
        }
    }

    private static void OnPlaceholderChanged(BindableObject bindable, object oldValue, object newValue)
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
        // If a details popup or suppression is active, ignore tap
        if (_suppressTap || _isDetailsPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Tap suppressed due to active long-press/details popup");
            return;
        }

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

            // Create refresh function that reloads data from services
            Func<Task<(List<Recipe> recipes, List<Folder> folders)>> refreshFunc = async () =>
            {
                var recipes = await _recipeService.GetRecipesAsync();
                var folders = await _folderService.GetFoldersAsync();
                return (recipes, folders);
            };

            // Create and show the popup WITH refresh function
            var popup = new FolderAwarePickerPopup(_allRecipes, _allFolders, refreshFunc);
            
            // Show the popup and get the result
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page == null)
            {
                await Shell.Current.DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.UnableToResolveCurrentPage, FoodbookApp.Localization.AddRecipePageResources.OKButton);
                return;
            }

            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Opening popup");

            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            
            // Wait for either the popup to be dismissed or a result to be set
            await Task.WhenAny(showTask, resultTask);
            
            // Get the result
            var result = resultTask.IsCompleted ? await resultTask : null;
            
            System.Diagnostics.Debug.WriteLine($"?? FolderAwarePickerComponent: Popup result: {result?.GetType().Name}");
            
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

            // After popup closes, refresh local data to stay in sync
            await LoadDataAsync();
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
                    System.Diagnostics.Debug.WriteLine($"? FolderAwarePickerComponent: Could not clear modal stack: {modalEx.Message}");
                }
            }
            
            // Fallback to display alert
            await Shell.Current.DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.CouldNotOpenRecipeSelectionDialog, FoodbookApp.Localization.AddRecipePageResources.OKButton);
        }
        finally
        {
            _isPopupOpen = false;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Popup protection released");
        }
    }

    /// <summary>
    /// Shows recipe details popup similar to HomePage meal details
    /// </summary>
    private async Task ShowRecipeDetailsAsync(Recipe? recipe)
    {
        // Protection against multiple opens
        if (_isDetailsPopupOpen)
        {
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Details popup already open, ignoring request");
            return;
        }

        if (recipe == null)
        {
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: No recipe selected");
            return;
        }

        try
        {
            // Suppress tap gestures while details popup is active
            _suppressTap = true;
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();

            _isDetailsPopupOpen = true;
            ((Command<Recipe>)ShowRecipeDetailsCommand).ChangeCanExecute();

            System.Diagnostics.Debug.WriteLine($"?? FolderAwarePickerComponent: Opening details popup for recipe: {recipe.Name}");

            // Add haptic feedback for long press
            try
            {
#if ANDROID || IOS
                Microsoft.Maui.Devices.HapticFeedback.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
#endif
            }
            catch { /* Haptic not supported */ }

            var page = Application.Current?.MainPage;
            if (page == null)
            {
                System.Diagnostics.Debug.WriteLine("? FolderAwarePickerComponent: Cannot resolve current page");
                return;
            }

            // Get full recipe with ingredients
            var fullRecipe = await _recipeService.GetRecipeAsync(recipe.Id);
            if (fullRecipe == null)
            {
                await page.DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.FailedToFetchRecipeDetails, FoodbookApp.Localization.AddRecipePageResources.OKButton);
                return;
            }

            var items = new List<object>();

            // Header with recipe name
            items.Add(new Views.Components.SimpleListPopup.SectionHeader 
            { 
                Text = fullRecipe.Name 
            });

            // Create a PlannedMeal with default portions for display
            var displayMeal = new PlannedMeal 
            { 
                Recipe = fullRecipe,
                Portions = fullRecipe.IloscPorcji 
            };

            // Add meal preview block with ingredients and portions control
            items.Add(new Views.Components.SimpleListPopup.MealPreviewBlock
            {
                Meal = displayMeal,
                Recipe = fullRecipe
            });

            // Add macro information (per portion)
            var perPortion = Math.Max(fullRecipe.IloscPorcji, 1);
            var onePortionMultiplier = 1.0 / perPortion;
            items.Add(new Views.Components.SimpleListPopup.MacroRow
            {
                Calories = fullRecipe.Calories * onePortionMultiplier,
                Protein = fullRecipe.Protein * onePortionMultiplier,
                Fat = fullRecipe.Fat * onePortionMultiplier,
                Carbs = fullRecipe.Carbs * onePortionMultiplier
            });

            // Add description if available
            if (!string.IsNullOrWhiteSpace(fullRecipe.Description))
            {
                items.Add(new Views.Components.SimpleListPopup.Description 
                { 
                    Text = fullRecipe.Description 
                });
            }

            var popup = new Views.Components.SimpleListPopup
            {
                TitleText = fullRecipe.Name,
                Items = items,
                IsBulleted = false
            };

            // Show the popup
            var showTask = page.ShowPopupAsync(popup);
            var resultTask = popup.ResultTask;
            
            await Task.WhenAny(showTask, resultTask);
            
            var result = resultTask.IsCompleted ? await resultTask : null;
            
            System.Diagnostics.Debug.WriteLine("? FolderAwarePickerComponent: Details popup closed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? FolderAwarePickerComponent: Error showing recipe details: {ex.Message}");
            
            // Handle specific popup exception
            if (ex.Message.Contains("PopupBlockedException") || ex.Message.Contains("blocked by the Modal Page"))
            {
                System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Attempting to close any existing modal pages");
                
                try
                {
                    if (Application.Current?.MainPage?.Navigation?.ModalStack?.Count > 0)
                    {
                        await Application.Current.MainPage.Navigation.PopModalAsync(false);
                    }
                    
                    System.Diagnostics.Debug.WriteLine("? FolderAwarePickerComponent: Modal stack cleared");
                }
                catch (Exception modalEx)
                {
                    System.Diagnostics.Debug.WriteLine($"? FolderAwarePickerComponent: Could not clear modal stack: {modalEx.Message}");
                }
            }
            
            // Fallback to display alert
            await Shell.Current.DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.FailedToOpenRecipeDetails, FoodbookApp.Localization.AddRecipePageResources.OKButton);
        }
        finally
        {
            _isDetailsPopupOpen = false;
            _suppressTap = false; // release suppression when details popup closes
            ((Command<Recipe>)ShowRecipeDetailsCommand).ChangeCanExecute();
            ((Command)OpenSelectionDialogCommand).ChangeCanExecute();
            System.Diagnostics.Debug.WriteLine("?? FolderAwarePickerComponent: Details popup protection released");
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        base.OnPropertyChanged(propertyName);
    }
}