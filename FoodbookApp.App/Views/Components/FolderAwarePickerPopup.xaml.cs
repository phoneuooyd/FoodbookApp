using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Maui.Views;
using Foodbook.Models;
using Microsoft.Maui.Controls;

namespace Foodbook.Views.Components;

public partial class FolderAwarePickerPopup : Popup, INotifyPropertyChanged
{
    private readonly List<Recipe> _allRecipes;
    private readonly List<Folder> _allFolders;
    private Folder? _currentFolder;
    private readonly List<Folder> _breadcrumb = new();
    private readonly TaskCompletionSource<object?> _tcs = new();
    
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

    public ICommand TapCommand { get; }
    public ICommand CloseCommand { get; }

    public Task<object?> ResultTask => _tcs.Task;

    public FolderAwarePickerPopup(List<Recipe> recipes, List<Folder> folders)
    {
        _allRecipes = recipes ?? new List<Recipe>();
        _allFolders = folders ?? new List<Folder>();
        Items = new ObservableCollection<FolderPickerItem>();
        Title = "Wybierz przepis";
        
        TapCommand = new Command<FolderPickerItem>(OnItemTapped);
        CloseCommand = new Command(() => CloseWithResult(null));
        
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
                DisplayName = "\u2190 Wróæ", // Unicode left arrow
                Description = _breadcrumb.LastOrDefault()?.Name ?? "Root",
                Icon = "\u2190", // Unicode left arrow
                FontAttributes = FontAttributes.None,
                ShowArrow = false,
                TapAction = GoBack
            });
        }

        // Add clear selection option
        Items.Add(new FolderPickerItem
        {
            ItemType = FolderPickerItemType.Navigation,
            DisplayName = "Wyczyœæ wybór",
            Description = "Usuñ wybrany przepis",
            Icon = "\u2716", // Unicode heavy multiplication X
            FontAttributes = FontAttributes.None,
            ShowArrow = false,
            TapAction = () => CloseWithResult(null)
        });

        // Get current folder contents
        var folders = _currentFolder == null 
            ? _allFolders.Where(f => f.ParentFolderId == null).ToList()
            : _allFolders.Where(f => f.ParentFolderId == _currentFolder.Id).ToList();

        var recipes = _currentFolder == null
            ? _allRecipes.Where(r => r.FolderId == null).ToList()
            : _allRecipes.Where(r => r.FolderId == _currentFolder.Id).ToList();

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
        foreach (var recipe in recipes.OrderBy(r => r.Name))
        {
            var nutritionInfo = $"{recipe.Calories:F0} kcal";
            if (recipe.IloscPorcji > 1)
                nutritionInfo += $" \u2022 {recipe.IloscPorcji} porcji";

            Items.Add(new FolderPickerItem
            {
                ItemType = FolderPickerItemType.Recipe,
                DisplayName = recipe.Name,
                Description = nutritionInfo,
                Icon = "\uD83C\uDF7D", // Recipe icon
                FontAttributes = FontAttributes.None,
                ShowArrow = false,
                Data = recipe,
                TapAction = () => CloseWithResult(recipe)
            });
        }

        // Update UI properties
        OnPropertyChanged(nameof(BreadcrumbText));
        OnPropertyChanged(nameof(HasBreadcrumb));
        OnPropertyChanged(nameof(Title));
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
        LoadCurrentFolderContents();
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
    public object? Data { get; set; }
    public Action? TapAction { get; set; }
}