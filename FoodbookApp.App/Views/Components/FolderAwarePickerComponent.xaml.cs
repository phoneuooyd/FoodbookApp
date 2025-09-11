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
    
    private List<Recipe> _allRecipes = new();
    private List<Folder> _allFolders = new();
    private Folder? _currentFolder;
    private List<Folder> _breadcrumb = new();

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
            // Reset to root when opening
            _currentFolder = null;
            _breadcrumb.Clear();
            
            await ShowFolderContentsDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening recipe selection dialog: {ex.Message}");
        }
    }

    private async Task ShowFolderContentsDialog()
    {
        // Get current folder contents
        var folders = _currentFolder == null 
            ? _allFolders.Where(f => f.ParentFolderId == null).ToList()
            : _allFolders.Where(f => f.ParentFolderId == _currentFolder.Id).ToList();

        var recipes = _currentFolder == null
            ? _allRecipes.Where(r => r.FolderId == null).ToList()
            : _allRecipes.Where(r => r.FolderId == _currentFolder.Id).ToList();

        // Build options list
        var options = new List<string>();
        var actions = new List<Func<Task>>();

        // Add breadcrumb/back option if not at root
        if (_breadcrumb.Count > 0)
        {
            options.Add("? Back");
            actions.Add(async () => await GoBack());
        }

        // Add clear selection option
        options.Add("? Clear Selection");
        actions.Add(async () => 
        {
            SelectedRecipe = null;
            await Task.CompletedTask;
        });

        // Add folders (with folder icon)
        foreach (var folder in folders)
        {
            options.Add($"[FOLDER] {folder.Name}");
            actions.Add(async () => await NavigateToFolder(folder));
        }

        // Add recipes (with recipe icon)
        foreach (var recipe in recipes)
        {
            options.Add($"[RECIPE] {recipe.Name}");
            actions.Add(async () => 
            {
                SelectedRecipe = recipe;
                await Task.CompletedTask;
            });
        }

        if (!options.Any())
        {
            await Shell.Current.DisplayAlert("Empty Folder", "This folder is empty.", "OK");
            return;
        }

        // Show dialog
        var title = _currentFolder?.Name ?? "Select Recipe";
        if (_breadcrumb.Count > 0)
        {
            title = $"{string.Join(" / ", _breadcrumb.Select(b => b.Name))} / {title}";
        }

        var result = await Shell.Current.DisplayActionSheet(
            title, 
            "Cancel", 
            null, 
            options.ToArray());

        if (result == "Cancel" || string.IsNullOrEmpty(result))
            return;

        // Execute corresponding action
        var index = options.IndexOf(result);
        if (index >= 0 && index < actions.Count)
        {
            await actions[index]();
            
            // If action was selecting a recipe, we're done
            // If action was navigation, show dialog again
            if (result.StartsWith("?") || result.StartsWith("[FOLDER]"))
            {
                await ShowFolderContentsDialog();
            }
        }
    }

    private async Task NavigateToFolder(Folder folder)
    {
        _breadcrumb.Add(_currentFolder ?? new Folder { Name = "Root", Id = 0 });
        _currentFolder = folder;
        await Task.CompletedTask;
    }

    private async Task GoBack()
    {
        if (_breadcrumb.Count > 0)
        {
            var previous = _breadcrumb.Last();
            _breadcrumb.RemoveAt(_breadcrumb.Count - 1);
            
            _currentFolder = previous.Id == 0 ? null : previous;
        }
        await Task.CompletedTask;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}