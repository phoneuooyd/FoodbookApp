using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.Linq;
using FoodbookApp.Localization;
using FoodbookApp.Localization;
using CommunityToolkit.Mvvm.Messaging;
using FoodbookApp.Interfaces;

namespace Foodbook.ViewModels
{
    // Messenger message to collapse FAB
    public sealed class FabCollapseMessage
    {
        public static readonly FabCollapseMessage Instance = new();
        private FabCollapseMessage() { }
    }

    public partial class RecipeViewModel : INotifyPropertyChanged
    {
        private readonly IRecipeService _recipeService;
        private readonly IIngredientService _ingredientService;
        private readonly IFolderService _folderService;
        private bool _isLoading;
        private bool _isRefreshing;
        private string _searchText = string.Empty;
        private List<Recipe> _allRecipes = new();
        private List<Folder> _allFolders = new();
        private Folder? _currentFolder;

        public event EventHandler? DataLoaded; // Raised when recipes and folders are loaded and filtered

        // Mixed list: folders on top, then recipes
        public ObservableCollection<object> Items { get; } = new();
        public ObservableCollection<Folder> Breadcrumb { get; } = new();

        public ObservableCollection<Recipe> Recipes { get; } = new(); // kept for compatibility if used elsewhere

        public ICommand AddFolderCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand BreadcrumbNavigateCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand FolderEditCommand { get; }

        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { if (_isRefreshing == value) return; _isRefreshing = value; OnPropertyChanged(); }
        }
        public string SearchText
        {
            get => _searchText;
            set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); FilterItems(); }
        }

        public bool CanGoBack => Breadcrumb.Count > 0;

        public ICommand AddRecipeCommand { get; }
        public ICommand EditRecipeCommand { get; }
        public ICommand DeleteRecipeCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public string CurrentPathDisplay => Breadcrumb.Count == 0 ? "/" : string.Join(" / ", Breadcrumb.Select(b => b.Name));

        public RecipeViewModel(IRecipeService recipeService, IIngredientService ingredientService, IFolderService folderService)
        {
            _recipeService = recipeService;
            _ingredientService = ingredientService;
            _folderService = folderService;

            AddRecipeCommand = new Command(async () =>
            {
                var param = _currentFolder?.Id > 0 ? $"?folderId={_currentFolder.Id}" : string.Empty;
                await Shell.Current.GoToAsync($"{nameof(AddRecipePage)}{param}");
                WeakReferenceMessenger.Default.Send(FabCollapseMessage.Instance);
            });

            AddFolderCommand = new Command(async () =>
            {
                await CreateFolderAsync();
                WeakReferenceMessenger.Default.Send(FabCollapseMessage.Instance);
            });

            EditRecipeCommand = new Command<Recipe>(async r =>
            {
                if (r != null)
                    await Shell.Current.GoToAsync($"{nameof(AddRecipePage)}?id={r.Id}");
            });
            DeleteRecipeCommand = new Command<Recipe>(async r => await DeleteRecipeAsync(r));

            EditItemCommand = new Command<object>(async o =>
            {
                switch (o)
                {
                    case Recipe r:
                        await Shell.Current.GoToAsync($"{nameof(AddRecipePage)}?id={r.Id}");
                        break;
                    case Folder f:
                        await NavigateIntoFolderAsync(f);
                        break;
                }
            });

            DeleteItemCommand = new Command<object>(async o =>
            {
                switch (o)
                {
                    case Recipe r:
                        await DeleteRecipeAsync(r);
                        break;
                    case Folder f:
                        bool confirm = await Shell.Current.DisplayAlert(
                            "Usuwanie folderu",
                            $"Czy na pewno chcesz usun¹æ folder '{f.Name}'? Przepisy zostan¹ przeniesione do folderu nadrzêdnego (lub root).",
                            "Tak",
                            "Nie");
                        if (confirm)
                        {
                            await _folderService.DeleteFolderAsync(f.Id);
                            await LoadRecipesAsync();
                        }
                        break;
                }
            });

            BreadcrumbNavigateCommand = new Command<Folder>(async f => await NavigateToBreadcrumbAsync(f));
            GoBackCommand = new Command(async () => await GoBackAsync(), () => CanGoBack);

            RefreshCommand = new Command(async () => await ReloadAsync());
            ClearSearchCommand = new Command(() => SearchText = string.Empty);

            FolderEditCommand = new Command<object>(async o =>
            {
                if ( o is Folder f)
                {
                    var title = FolderResources.RenameFolderTitle;
                    var prompt = FolderResources.RenameFolderPrompt;
                    var descTitle = FolderResources.EditDescriptionTitle;
                    var descPrompt = FolderResources.EditDescriptionPrompt;

                    string newName = await Shell.Current.DisplayPromptAsync(title, prompt, initialValue: f.Name, maxLength: 200);
                    if (newName == null) return; // user cancelled
                    newName = newName.Trim();
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        await Shell.Current.DisplayAlert(title, FolderResources.ValidationNameRequired, "OK");
                        return;
                    }
                    string newDesc = await Shell.Current.DisplayPromptAsync(descTitle, descPrompt, initialValue: f.Description ?? string.Empty, maxLength: 1000);
                    f.Name = newName;
                    f.Description = string.IsNullOrWhiteSpace(newDesc) ? null : newDesc.Trim();
                    await _folderService.UpdateFolderAsync(f);
                    FilterItems();
                }
            });

            InitializeDragDrop();
        }

        private void RaiseDataLoaded()
        {
            try { DataLoaded?.Invoke(this, EventArgs.Empty); } catch { }
        }

        private async Task GoBackAsync()
        {
            if (Breadcrumb.Count == 0)
                return;

            Breadcrumb.RemoveAt(Breadcrumb.Count - 1);
            _currentFolder = Breadcrumb.LastOrDefault();
            FilterItems();
            await Task.CompletedTask;
        }

        private async Task NavigateIntoFolderAsync(Folder f)
        {
            _currentFolder = f;
            Breadcrumb.Add(f);
            FilterItems();
            await Task.CompletedTask;
        }

        private async Task NavigateToBreadcrumbAsync(Folder f)
        {
            for (int i = Breadcrumb.Count - 1; i >= 0; i--)
            {
                if (Breadcrumb[i].Id == f.Id)
                    break;
                Breadcrumb.RemoveAt(i);
            }
            _currentFolder = f;
            FilterItems();
            await Task.CompletedTask;
        }

        private async Task CreateFolderAsync()
        {
            string result = await Shell.Current.DisplayPromptAsync("Nowy folder", "Podaj nazwê folderu", "Utwórz", "Anuluj", maxLength: 200, keyboard: Keyboard.Text);
            if (string.IsNullOrWhiteSpace(result)) return;

            var folder = new Folder { Name = result.Trim(), ParentFolderId = _currentFolder?.Id };
            await _folderService.AddFolderAsync(folder);
            await LoadRecipesAsync();
        }

        private async Task FetchAsync()
        {
            var recipes = await _recipeService.GetRecipesAsync();
            await RecalculateNutritionalValuesForRecipes(recipes);
            _allRecipes = recipes;

            _allFolders = await _folderService.GetFoldersAsync();

            Recipes.Clear();
            foreach (var r in _allRecipes) Recipes.Add(r);

            FilterItems();

            // All data ready for the page
            RaiseDataLoaded();
        }

        public async Task LoadRecipesAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                await FetchAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading recipes/folders: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterItems()
        {
            IEnumerable<object> src;
            if (_currentFolder == null)
            {
                src = _allFolders.Where(f => f.ParentFolderId == null).Cast<object>()
                      .Concat(_allRecipes.Where(r => r.FolderId == null));
            }
            else
            {
                src = _allFolders.Where(f => f.ParentFolderId == _currentFolder.Id).Cast<object>()
                      .Concat(_allRecipes.Where(r => r.FolderId == _currentFolder.Id));
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                src = src.Where(o => o is Recipe rr && (
                                         rr.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                         (rr.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                                      || o is Folder ff && ff.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            Items.Clear();
            foreach (var o in src)
                Items.Add(o);

            OnPropertyChanged(nameof(CurrentPathDisplay));
            OnPropertyChanged(nameof(CanGoBack));
            (GoBackCommand as Command)?.ChangeCanExecute();
        }

        private async Task RecalculateNutritionalValuesForRecipes(List<Recipe> recipes)
        {
            try
            {
                var allIngredients = await _ingredientService.GetIngredientsAsync();
                foreach (var recipe in recipes)
                {
                    if (recipe.Ingredients?.Any() == true)
                    {
                        double totalCalories = 0, totalProtein = 0, totalFat = 0, totalCarbs = 0;
                        foreach (var ingredient in recipe.Ingredients)
                        {
                            var dbIngredient = allIngredients.FirstOrDefault(i => i.Name == ingredient.Name);
                            if (dbIngredient != null)
                            {
                                ingredient.Calories = dbIngredient.Calories;
                                ingredient.Protein = dbIngredient.Protein;
                                ingredient.Fat = dbIngredient.Fat;
                                ingredient.Carbs = dbIngredient.Carbs;
                            }
                            double factor = GetUnitConversionFactor(ingredient.Unit, ingredient.Quantity);
                            totalCalories += ingredient.Calories * factor;
                            totalProtein += ingredient.Protein * factor;
                            totalFat += ingredient.Fat * factor;
                            totalCarbs += ingredient.Carbs * factor;
                        }
                        recipe.Calories = totalCalories;
                        recipe.Protein = totalProtein;
                        recipe.Fat = totalFat;
                        recipe.Carbs = totalCarbs;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error recalculating nutritional values: {ex.Message}");
            }
        }

        private double GetUnitConversionFactor(Unit unit, double quantity) => unit switch
        {
            Unit.Gram => quantity / 100.0,
            Unit.Milliliter => quantity / 100.0,
            Unit.Piece => quantity,
            _ => quantity / 100.0
        };

        public async Task ReloadAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                var fetchTask = FetchAsync();
                var timeoutTask = Task.Delay(15000); // 15s max
                var completed = await Task.WhenAny(fetchTask, timeoutTask);
                if (completed == fetchTask)
                {
                    await fetchTask; // surface exceptions
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[RecipeViewModel] Refresh timeout reached - stopping spinner");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeViewModel] Reload error: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private async Task DeleteRecipeAsync(Recipe? recipe)
        {
            if (recipe == null) return;
            bool confirm = await Shell.Current.DisplayAlert(
                "Usuwanie przepisu",
                $"Czy na pewno chcesz usun¹æ przepis '{recipe.Name}'?",
                "Tak",
                "Nie");
            if (!confirm) return;

            try
            {
                await _recipeService.DeleteRecipeAsync(recipe.Id);
                Recipes.Remove(recipe);
                _allRecipes.Remove(recipe);
                Items.Remove(recipe);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting recipe: {ex.Message}");
            }
        }

        // New drag & drop commands
        public ICommand DragStartingCommand { get; private set; }
        public ICommand DragOverCommand { get; private set; }
        public ICommand DragLeaveCommand { get; private set; }
        public ICommand DropCommand { get; private set; }

        private object? _dragSource;

        private void InitializeDragDrop()
        {
            DragStartingCommand = new Command<object>(o =>
            {
                _dragSource = o;
                if (o is Recipe r)
                {
                    r.IsBeingDragged = true;
                }
            });

            DragOverCommand = new Command<object>(o =>
            {
                if (o is Folder f)
                {
                    f.IsBeingDraggedOver = true;
                }
            });

            DragLeaveCommand = new Command<object>(o =>
            {
                if (o is Folder f)
                {
                    f.IsBeingDraggedOver = false;
                }
            });

            DropCommand = new Command<object>(async payload =>
            {
                try
                {
                    // payload may be a DragDropInfo or direct target binding context
                    if (_dragSource is not Recipe dragged)
                        return;

                    Folder? targetFolder = null;
                    switch (payload)
                    {
                        case Foodbook.Views.Components.DragDropInfo info:
                            targetFolder = info.Target as Folder;
                            break;
                        case Folder f:
                            targetFolder = f;
                            break;
                    }

                    if (targetFolder == null)
                        return; // we only allow dropping onto folders

                    // Update folder id and persist
                    dragged.FolderId = targetFolder.Id;
                    await _recipeService.UpdateRecipeAsync(dragged);

                    // Refresh lists locally without full reload
                    FilterItems();
                }
                finally
                {
                    // clear flags
                    if (_dragSource is Recipe r2) r2.IsBeingDragged = false;
                    if (payload is Folder f2) f2.IsBeingDraggedOver = false;
                    _dragSource = null;
                }
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Move dragged recipe one level up (to parent of current folder, or root when no parent)
        public async Task MoveRecipeUpAsync(Recipe recipe)
        {
            if (recipe == null) return;
            if (_currentFolder == null) return; // at root, nothing to move up to

            recipe.FolderId = _currentFolder.ParentFolderId; // null when parent is root
            await _recipeService.UpdateRecipeAsync(recipe);
            FilterItems();
        }
    }
}