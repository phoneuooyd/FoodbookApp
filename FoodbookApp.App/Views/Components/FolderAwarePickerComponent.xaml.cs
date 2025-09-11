using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;

namespace Foodbook.Views.Components;

public partial class FolderAwarePickerComponent : ContentView, INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IFolderService _folderService;
    
    private bool _isExpanded;
    private Recipe? _selectedRecipe;
    private Folder? _currentFolder;
    private List<Recipe> _allRecipes = new();
    private List<Folder> _allFolders = new();

    public static readonly BindableProperty SelectedRecipeProperty = 
        BindableProperty.Create(nameof(SelectedRecipe), typeof(Recipe), typeof(FolderAwarePickerComponent), 
            defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnSelectedRecipeChanged);

    public static readonly BindableProperty PlaceholderTextProperty = 
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(FolderAwarePickerComponent), "Select recipe...");

    public ObservableCollection<object> Items { get; } = new();
    public ObservableCollection<Folder> Breadcrumb { get; } = new();

    public ICommand ToggleCommand { get; }
    public ICommand FolderTapCommand { get; }
    public ICommand RecipeTapCommand { get; }
    public ICommand GoBackCommand { get; }

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

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool ShowBreadcrumb => Breadcrumb.Count > 0;
    public bool CanGoBack => Breadcrumb.Count > 0;
    public string CurrentPathDisplay => Breadcrumb.Count == 0 ? "/" : string.Join(" / ", Breadcrumb.Select(b => b.Name));
    
    public string DisplayText => SelectedRecipe?.Name ?? PlaceholderText;

    public FolderAwarePickerComponent()
    {
        // Get services from DI container
        _recipeService = IPlatformApplication.Current?.Services?.GetService<IRecipeService>() 
                        ?? throw new InvalidOperationException("IRecipeService not found");
        _folderService = IPlatformApplication.Current?.Services?.GetService<IFolderService>() 
                        ?? throw new InvalidOperationException("IFolderService not found");

        ToggleCommand = new Command(() => IsExpanded = !IsExpanded);
        FolderTapCommand = new Command<Folder>(NavigateIntoFolder);
        RecipeTapCommand = new Command<Recipe>(SelectRecipe);
        GoBackCommand = new Command(GoBack, () => CanGoBack);

        InitializeComponent();
        _ = LoadDataAsync();
    }

    private static void OnSelectedRecipeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FolderAwarePickerComponent component)
        {
            component._selectedRecipe = newValue as Recipe;
            component.OnPropertyChanged(nameof(DisplayText));
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            _allRecipes = await _recipeService.GetRecipesAsync();
            _allFolders = await _folderService.GetFoldersAsync();
            FilterItems();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data in FolderAwarePickerComponent: {ex.Message}");
        }
    }

    private void NavigateIntoFolder(Folder? folder)
    {
        if (folder == null) return;
        
        _currentFolder = folder;
        Breadcrumb.Add(folder);
        FilterItems();
        UpdateBreadcrumbProperties();
    }

    private void SelectRecipe(Recipe? recipe)
    {
        if (recipe == null) return;
        
        SelectedRecipe = recipe;
        IsExpanded = false;
        
        // Navigate back to root after selection for better UX
        Breadcrumb.Clear();
        _currentFolder = null;
        FilterItems();
        UpdateBreadcrumbProperties();
    }

    private void GoBack()
    {
        if (Breadcrumb.Count == 0) return;
        
        Breadcrumb.RemoveAt(Breadcrumb.Count - 1);
        _currentFolder = Breadcrumb.LastOrDefault();
        FilterItems();
        UpdateBreadcrumbProperties();
    }

    private void FilterItems()
    {
        IEnumerable<object> items;
        
        if (_currentFolder == null)
        {
            // Root level: show folders without parent and recipes without folder
            var folders = _allFolders.Where(f => f.ParentFolderId == null).Cast<object>();
            var recipes = _allRecipes.Where(r => r.FolderId == null).Cast<object>();
            
            // Folders first, then recipes
            items = folders.Concat(recipes);
        }
        else
        {
            // Inside folder: show subfolders and recipes in this folder
            var subfolders = _allFolders.Where(f => f.ParentFolderId == _currentFolder.Id).Cast<object>();
            var recipesInFolder = _allRecipes.Where(r => r.FolderId == _currentFolder.Id).Cast<object>();
            
            // Folders first, then recipes
            items = subfolders.Concat(recipesInFolder);
        }

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    private void UpdateBreadcrumbProperties()
    {
        OnPropertyChanged(nameof(ShowBreadcrumb));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CurrentPathDisplay));
        (GoBackCommand as Command)?.ChangeCanExecute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}