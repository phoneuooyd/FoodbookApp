using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using Foodbook.Models;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels; // for SettingsViewModel
using CommunityToolkit.Maui.Extensions; // for ShowPopup extension
using Foodbook.Views; // for AddRecipePage
using CommunityToolkit.Maui.Core;
using FoodbookApp.Interfaces; // IIngredientService

namespace Foodbook.Views.Components;

public partial class FolderAwarePickerPopup : Popup, INotifyPropertyChanged
{
    private readonly List<Recipe> _allRecipes;
    private readonly List<Folder> _allFolders;
    private Folder? _currentFolder;
    private readonly List<Folder> _breadcrumb = new();
    private readonly TaskCompletionSource<object?> _tcs = new();
    private readonly Func<Task<(List<Recipe> recipes, List<Folder> folders)>>? _dataRefreshFunc;

    // NEW: sorting and label filtering state
    private SortOrder _sortOrder = SortOrder.Asc;
    private readonly HashSet<int> _selectedLabelIds = new();

    // NEW: ingredient names filter state
    private readonly HashSet<string> _selectedIngredientNames = new(System.StringComparer.OrdinalIgnoreCase);

    // Track if we're in edit mode to prevent unnecessary refreshes
    private bool _isEditingRecipe = false;
    
    // Simple flag to prevent tap after long press
    private volatile bool _suppressNextTap;
    
    // Track which item triggered long press
    private FolderPickerItem? _longPressedItem;

    public static readonly BindableProperty TitleProperty = 
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FolderAwarePickerPopup), "Select Recipe");

    public static readonly BindableProperty ItemsProperty = 
        BindableProperty.Create(nameof(Items), typeof(ObservableCollection<FolderPickerItem>), typeof(FolderAwarePickerPopup));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ObservableCollection<FolderPickerItem> Items
    {
        get => (ObservableCollection<FolderPickerItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string BreadcrumbText => _breadcrumb.Count > 0 
        ? string.Join(" / ", _breadcrumb.Select(b => b.Name)) + (_currentFolder != null ? $" / {_currentFolder.Name}" : "")
        : (_currentFolder?.Name ?? "Root");

    public bool HasBreadcrumb => _breadcrumb.Count > 0 || _currentFolder != null;

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            ApplySearch();
        }
    }

    public ICommand TapCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand LongPressCommand { get; }

    public Task<object?> ResultTask => _tcs.Task;

    public FolderAwarePickerPopup(List<Recipe> recipes, List<Folder> folders, Func<Task<(List<Recipe> recipes, List<Folder> folders)>>? dataRefreshFunc = null)
    {
        _allRecipes = recipes ?? new List<Recipe>();
        _allFolders = folders ?? new List<Folder>();
        _dataRefreshFunc = dataRefreshFunc;
        Items = new ObservableCollection<FolderPickerItem>();
        Title = FoodbookApp.Localization.FolderResources.ChooseRecipeTitle;
        
        TapCommand = new Command<FolderPickerItem>(OnItemTapped);
        CloseCommand = new Command(() => CloseWithResult(null));
        RefreshCommand = new Command(async () => await RefreshDataAsync());
        BackCommand = new Command(GoBack);
        ClearSearchCommand = new Command(() => { SearchText = string.Empty; });
        LongPressCommand = new Command<FolderPickerItem>(async (item) => 
        {
            System.Diagnostics.Debug.WriteLine($"============ LONG PRESS COMMAND EXECUTED ============");
            System.Diagnostics.Debug.WriteLine($"Item: {item?.DisplayName}, Type: {item?.ItemType}");
            
            // Mark that long press happened - this will suppress the tap command
            _suppressNextTap = true;
            _longPressedItem = item;
            
            // Add haptic feedback for long press
            try
            {
#if ANDROID || IOS
                Microsoft.Maui.Devices.HapticFeedback.Perform(Microsoft.Maui.Devices.HapticFeedbackType.LongPress);
#endif
            }
            catch { /* Haptic not supported */ }
            
            await OnItemLongPressedAsync(item);
        });
        
        System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Constructor - Using command-based long press detection");
        
        InitializeComponent();
        
        LoadCurrentFolderContents();
    }

    // NEW: Command-based long press handling
    private async Task OnItemLongPressedAsync(FolderPickerItem? item)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Long press detected on item: {item?.DisplayName}");
            
            if (item == null || item.ItemType != FolderPickerItemType.Recipe)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Item is not a recipe, ignoring. ItemType: {item?.ItemType}");
                _suppressNextTap = false; // Reset flag if not a recipe
                return;
            }

            if (item.Data is not Recipe recipe)
            {
                System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Item data is not a Recipe");
                _suppressNextTap = false;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Opening edit page for recipe: {recipe.Name} (ID: {recipe.Id})");

            // Set flag to indicate we're editing
            _isEditingRecipe = true;

            // Resolve edit page from DI
            var editPage = FoodbookApp.MauiProgram.ServiceProvider?.GetService<AddRecipePage>();
            if (editPage == null)
            {
                System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Failed to resolve AddRecipePage from DI");
                _isEditingRecipe = false;
                _suppressNextTap = false;
                return;
            }

            // Pass parameters
            editPage.RecipeId = recipe.Id;
            if (_currentFolder != null)
                editPage.FolderId = _currentFolder.Id;

            void OnEditPageDisappearing(object? s, EventArgs ev)
            {
                try
                {
                    if (s is AddRecipePage p)
                        p.Disappearing -= OnEditPageDisappearing;
                    
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Edit page closed, refreshing popup data");
                        // Only refresh popup's internal data, don't trigger full parent refresh
                        await RefreshPopupDataOnlyAsync();
                        
                        // Delay resetting flags to avoid immediate tap
                        await Task.Delay(500);
                        _suppressNextTap = false;
                        _isEditingRecipe = false;
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Refresh after edit failed: {ex.Message}");
                    _isEditingRecipe = false;
                    _suppressNextTap = false;
                }
            }
            editPage.Disappearing += OnEditPageDisappearing;

            var nav = Application.Current?.MainPage?.Navigation;
            if (nav != null)
            {
                await nav.PushModalAsync(editPage);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Navigation is null");
                _isEditingRecipe = false;
                _suppressNextTap = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] OnItemLongPressedAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Stack trace: {ex.StackTrace}");
            _isEditingRecipe = false;
            _suppressNextTap = false;
        }
    }

    // Event handler: Called when touch gesture ends
    private void OnItemTouchCompleted(object? sender, EventArgs e)
    {
        try
        {
            // If long press was NOT triggered, allow normal tap behavior
            if (_longPressedItem != null)
            {
                System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Touch completed but long press was active - resetting");
                _longPressedItem = null;
                // Keep _suppressNextTap true for a moment to block the tap
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] OnItemTouchCompleted error: {ex.Message}");
        }
    }

    private async void CloseWithResult(object? result)
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.SetResult(result);
        }
        await CloseAsync();
    }

    private void LoadCurrentFolderContents()
    {
        Items.Clear();

        // Add back navigation if not at root
        if (_breadcrumb.Count > 0)
        {
            Items.Add(new FolderPickerItem
            {
                ItemType = FolderPickerItemType.Navigation,
                DisplayName = FoodbookApp.Localization.FolderResources.BackButton,
                Description = _breadcrumb.LastOrDefault()?.Name ?? FoodbookApp.Localization.FolderResources.Root,
                Icon = "\u2190", // Unicode left arrow
                FontAttributes = FontAttributes.None,
                ShowArrow = false,
                TapAction = GoBack
            });
        }

        // Get current folder contents
        var folders = _currentFolder == null 
            ? _allFolders.Where(f => f.ParentFolderId == null).ToList()
            : _allFolders.Where(f => f.ParentFolderId == _currentFolder.Id).ToList();

        var recipes = _currentFolder == null
            ? _allRecipes.Where(r => r.FolderId == null).ToList()
            : _allRecipes.Where(r => r.FolderId == _currentFolder.Id).ToList();

        // Apply label filter (if any)
        if (_selectedLabelIds.Count > 0)
        {
            recipes = recipes.Where(r => (r.Labels?.Any(l => _selectedLabelIds.Contains(l.Id)) ?? false)).ToList();
        }
        // NEW: apply ingredient names filter (if any)
        if (_selectedIngredientNames.Count > 0)
        {
            recipes = recipes.Where(r => (r.Ingredients?.Any(i => !string.IsNullOrEmpty(i.Name) && _selectedIngredientNames.Contains(i.Name)) ?? false)).ToList();
        }

        // Sort
        recipes = (_sortOrder == SortOrder.Desc)
            ? recipes.OrderByDescending(r => r.Name).ToList()
            : recipes.OrderBy(r => r.Name).ToList();

        // Add folders first
        foreach (var folder in folders.OrderBy(f => f.Name))
        {
            Items.Add(new FolderPickerItem
            {
                ItemType = FolderPickerItemType.Folder,
                DisplayName = folder.Name,
                Description = folder.Description,
                Icon = "\uD83D\uDCC1", // Folder icon
                FontAttributes = FontAttributes.Bold,
                ShowArrow = true,
                Data = folder,
                TapAction = () => NavigateToFolder(folder)
            });
        }

        // Add recipes
        foreach (var recipe in recipes)
        {
            Items.Add(CreateRecipeItem(recipe));
        }

        // Update UI properties
        OnPropertyChanged(nameof(BreadcrumbText));
        OnPropertyChanged(nameof(HasBreadcrumb));
        OnPropertyChanged(nameof(Title));

        // Apply current search if any
        if (!string.IsNullOrWhiteSpace(_searchText))
            ApplySearch();
    }

    private FolderPickerItem CreateRecipeItem(Recipe recipe)
    {
        var nutritionInfo = $"{recipe.Calories:F0} kcal";
        if (recipe.IloscPorcji > 1)
            nutritionInfo += $" \u2022 {recipe.IloscPorcji} porcji";

        return new FolderPickerItem
        {
            ItemType = FolderPickerItemType.Recipe,
            DisplayName = recipe.Name,
            Description = nutritionInfo,
            Icon = "\uD83C\uDF7D",
            FontAttributes = FontAttributes.None,
            ShowArrow = false,
            Data = recipe,
            TapAction = () => 
            { 
                if (!_suppressNextTap)
                {
                    System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Tap action for recipe: {recipe.Name}");
                    CloseWithResult(recipe);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Tap suppressed (after long press)");
                }
            }
        };
    }

    private void ApplySearch()
    {
        try
        {
            var text = _searchText?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                // show current folder contents with current filters
                LoadCurrentFolderContents();
                return;
            }

            var query = text.ToLowerInvariant();
            Items.Clear();

            // If user typed a folder name, show recipes from that folder too
            var matchingFolders = _allFolders.Where(f => (f.Name?.ToLower().Contains(query) ?? false)).ToList();
            var folderIds = matchingFolders.Select(f => (int?)f.Id).ToHashSet();

            // Search recipes by name/description OR located in matched folders
            var matchedRecipes = _allRecipes.Where(r =>
                    (r.Name?.ToLower().Contains(query) ?? false)
                 || (r.Description?.ToLower().Contains(query) ?? false)
                 || (folderIds.Contains(r.FolderId)))
                .ToList();

            // If currently in a specific folder, limit to it + children navigation imply? keep original behavior
            if (_currentFolder != null)
            {
                matchedRecipes = matchedRecipes.Where(r => r.FolderId == _currentFolder.Id).ToList();
            }

            // Apply label filter
            if (_selectedLabelIds.Count > 0)
            {
                matchedRecipes = matchedRecipes.Where(r => (r.Labels?.Any(l => _selectedLabelIds.Contains(l.Id)) ?? false)).ToList();
            }
            // Apply ingredient names filter
            if (_selectedIngredientNames.Count > 0)
            {
                matchedRecipes = matchedRecipes.Where(r => (r.Ingredients?.Any(i => !string.IsNullOrEmpty(i.Name) && _selectedIngredientNames.Contains(i.Name)) ?? false)).ToList();
            }

            // Sort
            matchedRecipes = (_sortOrder == SortOrder.Desc)
                ? matchedRecipes.OrderByDescending(r => r.Name).ToList()
                : matchedRecipes.OrderBy(r => r.Name).ToList();

            foreach (var recipe in matchedRecipes)
            {
                Items.Add(CreateRecipeItem(recipe));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FolderAwarePickerPopup.ApplySearch error: {ex.Message}");
        }
    }

    private void OnItemTapped(FolderPickerItem item)
    {
        // Check if this tap is after a long press
        if (_suppressNextTap)
        {
            System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Tap ignored (suppressed after long press)");
            // Reset the flag after short delay
            Task.Delay(300).ContinueWith(_ => _suppressNextTap = false);
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Tap command for item: {item?.DisplayName}");
        item?.TapAction?.Invoke();
    }

    private void NavigateToFolder(Folder folder)
    {
        if (_currentFolder != null)
        {
            _breadcrumb.Add(_currentFolder);
        }
        _currentFolder = folder;
        _searchText = string.Empty; // reset search when navigating
        OnPropertyChanged(nameof(SearchText));
        LoadCurrentFolderContents();
    }

    private void GoBack()
    {
        if (_breadcrumb.Count > 0)
        {
            _currentFolder = _breadcrumb.LastOrDefault();
            _breadcrumb.RemoveAt(_breadcrumb.Count - 1);
        }
        else
        {
            _currentFolder = null;
        }
        _searchText = string.Empty; // reset search when navigating back
        OnPropertyChanged(nameof(SearchText));
        LoadCurrentFolderContents();
    }

    private async Task RefreshDataAsync()
    {
        // Don't refresh if we're just editing a recipe
        if (_isEditingRecipe)
        {
            System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Skipping full refresh - editing recipe");
            return;
        }

        if (_dataRefreshFunc == null) return;

        try
        {
            var (recipes, folders) = await _dataRefreshFunc();
            
            // Update internal data
            _allRecipes.Clear();
            _allRecipes.AddRange(recipes);
            
            _allFolders.Clear();
            _allFolders.AddRange(folders);
            
            // Reload current folder contents with fresh data
            LoadCurrentFolderContents();
        }
        catch (Exception ex)
        {
            // Handle refresh error - could show toast or log
            System.Diagnostics.Debug.WriteLine($"Error refreshing data: {ex.Message}");
        }
    }

    // New method: Refresh only popup's internal data without triggering parent refresh
    private async Task RefreshPopupDataOnlyAsync()
    {
        if (_dataRefreshFunc == null)
        {
            // If no refresh function provided, just reload current view with existing data
            LoadCurrentFolderContents();
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Refreshing popup data only (lightweight)");
            
            var (recipes, folders) = await _dataRefreshFunc();
            
            // Update internal data
            _allRecipes.Clear();
            _allRecipes.AddRange(recipes);
            
            _allFolders.Clear();
            _allFolders.AddRange(folders);
            
            // Reload current folder contents with fresh data
            LoadCurrentFolderContents();
            
            System.Diagnostics.Debug.WriteLine("[FolderAwarePickerPopup] Popup data refreshed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] Error refreshing popup data: {ex.Message}");
            // Fallback to reloading with existing data
            LoadCurrentFolderContents();
        }
    }

    // NEW: filter/sort button handler (like RecipesPage)
    private async void OnFilterSortClicked(object? sender, EventArgs e)
    {
        try
        {
            // Labels available for filtering are managed in SettingsViewModel
            var settingsVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<SettingsViewModel>();
            var allLabels = settingsVm?.Labels?.ToList() ?? new List<RecipeLabel>();

            // Ingredients from DB (not from recipe list)
            var ingredientService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IIngredientService>();
            var allIngredients = ingredientService != null ? await ingredientService.GetIngredientsAsync() : new List<Ingredient>();

            var popup = new FilterSortPopup(
                showLabels: true,
                labels: allLabels,
                preselectedLabelIds: _selectedLabelIds,
                sortOrder: _sortOrder,
                showIngredients: true,
                ingredients: allIngredients,
                preselectedIngredientNames: _selectedIngredientNames);

            var hostPage = Application.Current?.MainPage ?? (Page?)this.Parent;
            hostPage?.ShowPopup(popup);
            var result = await popup.ResultTask;
            if (result != null)
            {
                _sortOrder = result.SortOrder;
                _selectedLabelIds.Clear();
                foreach (var id in result.SelectedLabelIds.Distinct()) _selectedLabelIds.Add(id);

                _selectedIngredientNames.Clear();
                foreach (var name in result.SelectedIngredientNames.Distinct(StringComparer.OrdinalIgnoreCase))
                    _selectedIngredientNames.Add(name);

                // Refresh current listing with applied filters
                if (string.IsNullOrWhiteSpace(_searchText))
                    LoadCurrentFolderContents();
                else
                    ApplySearch();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderAwarePickerPopup] OnFilterSortClicked error: {ex.Message}");
        }
    }

    public async Task<object?> ShowAndRefreshAsync()
    {
        // Auto refresh data when popup is shown
        await RefreshDataAsync();
        return await ResultTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum FolderPickerItemType
{
    Navigation,
    Folder,
    Recipe
}

public class FolderPickerItem
{
    public FolderPickerItemType ItemType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Icon { get; set; } = string.Empty;
    public FontAttributes FontAttributes { get; set; }
    public bool ShowArrow { get; set; }
    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public bool HasIcon => !string.IsNullOrEmpty(Icon);
    public object? Data { get; set; }
    public Action? TapAction { get; set; }
}