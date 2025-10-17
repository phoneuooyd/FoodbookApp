using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using Foodbook.Models;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels; // for SettingsViewModel
using CommunityToolkit.Maui.Extensions; // for ShowPopup extension

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

    public Task<object?> ResultTask => _tcs.Task;

    public FolderAwarePickerPopup(List<Recipe> recipes, List<Folder> folders, Func<Task<(List<Recipe> recipes, List<Folder> folders)>>? dataRefreshFunc = null)
    {
        _allRecipes = recipes ?? new List<Recipe>();
        _allFolders = folders ?? new List<Folder>();
        _dataRefreshFunc = dataRefreshFunc;
        Items = new ObservableCollection<FolderPickerItem>();
        Title = "Wybierz przepis";
        
        TapCommand = new Command<FolderPickerItem>(OnItemTapped);
        CloseCommand = new Command(() => CloseWithResult(null));
        RefreshCommand = new Command(async () => await RefreshDataAsync());
        BackCommand = new Command(GoBack);
        ClearSearchCommand = new Command(() => { SearchText = string.Empty; });
        
        InitializeComponent();
        
        LoadCurrentFolderContents();
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
                DisplayName = "Wróæ",
                Description = _breadcrumb.LastOrDefault()?.Name ?? "Root",
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
            TapAction = () => CloseWithResult(recipe)
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

    // NEW: filter/sort button handler (like RecipesPage)
    private async void OnFilterSortClicked(object? sender, EventArgs e)
    {
        try
        {
            // Labels available for filtering are managed in SettingsViewModel
            var settingsVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<SettingsViewModel>();
            var allLabels = settingsVm?.Labels?.ToList() ?? new List<RecipeLabel>();

            var popup = new FilterSortPopup(
                showLabels: true,
                labels: allLabels,
                preselectedLabelIds: _selectedLabelIds,
                sortOrder: _sortOrder);

            var hostPage = Application.Current?.MainPage ?? (Page?)this.Parent;
            hostPage?.ShowPopup(popup);
            var result = await popup.ResultTask;
            if (result != null)
            {
                _sortOrder = result.SortOrder;
                _selectedLabelIds.Clear();
                foreach (var id in result.SelectedLabelIds.Distinct()) _selectedLabelIds.Add(id);

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