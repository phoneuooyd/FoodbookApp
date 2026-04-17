using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.Views;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using FoodbookApp.Localization;
using CommunityToolkit.Mvvm.Messaging;
using FoodbookApp.Interfaces;
using Foodbook.Views.Components; // for SortBy

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
        private readonly Dictionary<Guid, List<Recipe>> _recipesByFolder = new();
        private readonly Dictionary<Guid, List<Folder>> _foldersByParent = new();
        private readonly HashSet<Guid> _folderIds = new();
        private CancellationTokenSource? _filterCts;
        private int _filterVersion;
        private int _pendingReloadRequests;
        private bool _suppressNextFolderEntryAnimation;
        private string _currentPathDisplay = "/";

        private const int SearchDebounceMs = 140;
        private Folder? _currentFolder;

        // drag reorder state for folders
        private object? _dragSource;

        // New: sorting and label filter state
        private SortOrder _sortOrder = SortOrder.Asc;
        public SortOrder SortOrder
        {
            get => _sortOrder;
            set { if (_sortOrder == value) return; _sortOrder = value; OnPropertyChanged(); FilterItems(); }
        }

        // NEW: selected SortBy (optional, overrides SortOrder when set)
        private SortBy? _currentSortBy;
        public SortBy? CurrentSortBy
        {
            get => _currentSortBy;
            set { if (_currentSortBy == value) return; _currentSortBy = value; OnPropertyChanged(); FilterItems(); }
        }

        private HashSet<Guid> _selectedLabelIds = new();
        public IReadOnlyCollection<Guid> SelectedLabelIds => _selectedLabelIds;

        // NEW: ingredient names filter state
        private HashSet<string> _selectedIngredientNames = new(System.StringComparer.OrdinalIgnoreCase);
        public IReadOnlyCollection<string> SelectedIngredientNames => _selectedIngredientNames;

        public void ApplySortingAndLabelFilter(SortOrder sortOrder, IEnumerable<Guid> labelIds)
        {
            _currentSortBy = null;
            _sortOrder = sortOrder;
            _selectedLabelIds = new HashSet<Guid>(labelIds ?? Enumerable.Empty<Guid>());
            OnPropertyChanged(nameof(SortOrder));
            FilterItems();
        }

        // NEW: overload that also accepts ingredient names
        public void ApplySortingLabelAndIngredientFilter(SortOrder sortOrder, IEnumerable<Guid> labelIds, IEnumerable<string> ingredientNames)
        {
            _currentSortBy = null;
            _sortOrder = sortOrder;
            _selectedLabelIds = new HashSet<Guid>(labelIds ?? Enumerable.Empty<Guid>());
            _selectedIngredientNames = new HashSet<string>(ingredientNames ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(SortOrder));
            FilterItems();
        }

        // NEW: Accept SortBy directly from popup
        public void ApplySortingLabelAndIngredientFilter(SortBy sortBy, IEnumerable<Guid> labelIds, IEnumerable<string> ingredientNames)
        {
            _currentSortBy = sortBy;
            _sortOrder = MapSortByToSortOrder(sortBy);
            _selectedLabelIds = new HashSet<Guid>(labelIds ?? Enumerable.Empty<Guid>());
            _selectedIngredientNames = new HashSet<string>(ingredientNames ?? Enumerable.Empty<string>(), System.StringComparer.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(CurrentSortBy));
            OnPropertyChanged(nameof(SortOrder));
            FilterItems();
        }

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
            set { if (_searchText == value) return; _searchText = value; OnPropertyChanged(); FilterItems(debounce: true); }
        }

        public bool CanGoBack => Breadcrumb.Count > 0;

        public ICommand AddRecipeCommand { get; }
        public ICommand EditRecipeCommand { get; }
        public ICommand DeleteRecipeCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public string CurrentPathDisplay => _currentPathDisplay;

        public RecipeViewModel(IRecipeService recipeService, IIngredientService ingredientService, IFolderService folderService)
        {
            _recipeService = recipeService;
            _ingredientService = ingredientService;
            _folderService = folderService;

            AddRecipeCommand = new Command(async () =>
            {
                var param = _currentFolder?.Id != null && _currentFolder.Id != Guid.Empty ? $"?folderId={_currentFolder.Id}" : string.Empty;
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
                // ? CRITICAL: Validate recipe and its ID before navigation
                if (r == null || r.Id == Guid.Empty)
                {
                    System.Diagnostics.Debug.WriteLine($"?? EditRecipeCommand: Invalid recipe (null or Guid.Empty)");
                    return;
                }
                System.Diagnostics.Debug.WriteLine($"?? EditRecipeCommand: Navigating to recipe {r.Id} ({r.Name})");
                await Shell.Current.GoToAsync($"{nameof(AddRecipePage)}?id={r.Id}");
            });
            DeleteRecipeCommand = new Command<Recipe>(async r => await DeleteRecipeAsync(r));

            EditItemCommand = new Command<object>(async o =>
            {
                switch (o)
                {
                    case Recipe r:
                        // ? CRITICAL: Validate recipe ID before navigation
                        if (r.Id == Guid.Empty)
                        {
                            System.Diagnostics.Debug.WriteLine($"?? EditItemCommand: Recipe has Guid.Empty ID");
                            return;
                        }
                        System.Diagnostics.Debug.WriteLine($"?? EditItemCommand: Navigating to recipe {r.Id} ({r.Name})");
                        await Shell.Current.GoToAsync($"{nameof(AddRecipePage)}?id={r.Id}");
                        break;
                    case Folder f:
                        if (f.Id == Guid.Empty)
                        {
                            System.Diagnostics.Debug.WriteLine($"?? EditItemCommand: Folder has Guid.Empty ID");
                            return;
                        }
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
                            GetFolderText("DeleteFolderTitle", "Delete folder"),
                            string.Format(GetFolderText("DeleteFolderConfirmFormat", "Are you sure you want to delete folder '{0}'? Recipes will be moved to the parent folder (or root)."), f.Name),
                            ButtonResources.Yes,
                            ButtonResources.No);
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

                    string newName = await Shell.Current.DisplayPromptAsync(title, prompt, initialValue: f.Name, maxLength: 200);
                    if (newName == null) return; // user cancelled
                    newName = newName.Trim();
                    if (string.IsNullOrWhiteSpace(newName))
                    {
                        await Shell.Current.DisplayAlert(title, FolderResources.ValidationNameRequired, ButtonResources.OK);
                        return;
                    }

                    // Only rename folder. Description editing dialog removed per request.
                    f.Name = newName;
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
            _suppressNextFolderEntryAnimation = true;
            UpdateNavigationPathState();
            FilterItems();
            await Task.CompletedTask;
        }

        private async Task NavigateIntoFolderAsync(Folder f)
        {
            _currentFolder = f;
            Breadcrumb.Add(f);
            _suppressNextFolderEntryAnimation = true;
            UpdateNavigationPathState();
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
            _suppressNextFolderEntryAnimation = true;
            UpdateNavigationPathState();
            FilterItems();
            await Task.CompletedTask;
        }

        private async Task CreateFolderAsync()
        {
            string result = await Shell.Current.DisplayPromptAsync(
                GetFolderText("CreateFolderTitle", "New folder"),
                GetFolderText("CreateFolderPrompt", "Enter folder name"),
                GetFolderText("CreateFolderAccept", "Create"),
                ButtonResources.Cancel,
                maxLength: 200,
                keyboard: Microsoft.Maui.Keyboard.Text);
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
            RebuildNavigationIndexes();

            Recipes.Clear();
            foreach (var r in _allRecipes) Recipes.Add(r);

            FilterItems();

            // All data ready for the page
            RaiseDataLoaded();
        }

        public async Task LoadRecipesAsync()
        {
            if (IsLoading)
            {
                Interlocked.Exchange(ref _pendingReloadRequests, 1);
                return;
            }

            IsLoading = true;
            try
            {
                while (true)
                {
                    Interlocked.Exchange(ref _pendingReloadRequests, 0);
                    await FetchAsync();

                    if (Interlocked.Exchange(ref _pendingReloadRequests, 0) == 0)
                    {
                        break;
                    }
                }
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

        private void FilterItems(bool debounce = false)
        {
            ScheduleFilter(debounce ? SearchDebounceMs : 0);
        }

        private void ScheduleFilter(int delayMs)
        {
            var nextCts = new CancellationTokenSource();
            var previousCts = Interlocked.Exchange(ref _filterCts, nextCts);
            previousCts?.Cancel();
            previousCts?.Dispose();

            var version = Interlocked.Increment(ref _filterVersion);
            _ = ApplyFilterAsync(version, delayMs, nextCts.Token);
        }

        private async Task ApplyFilterAsync(int version, int delayMs, CancellationToken cancellationToken)
        {
            try
            {
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }

                var resetInvalidFolderBreadcrumb = false;
                if (_currentFolder != null && !_folderIds.Contains(_currentFolder.Id))
                {
                    _currentFolder = null;
                    resetInvalidFolderBreadcrumb = true;
                }

                var currentFolderId = _currentFolder?.Id;
                var searchText = SearchText;
                var selectedLabelIds = _selectedLabelIds.ToArray();
                var selectedIngredientNames = _selectedIngredientNames.ToArray();
                var currentSortBy = CurrentSortBy;
                var currentSortOrder = SortOrder;
                var suppressFolderAnimation = _suppressNextFolderEntryAnimation;

                var folders = GetFoldersForCurrentFolder(currentFolderId);
                var recipes = GetRecipesForCurrentFolder(currentFolderId);

                var nextItems = await Task.Run(
                    () => ComposeFilteredItems(
                        folders,
                        recipes,
                        searchText,
                        selectedLabelIds,
                        selectedIngredientNames,
                        currentSortBy,
                        currentSortOrder,
                        suppressFolderAnimation),
                    cancellationToken).ConfigureAwait(false);

                var shouldAutoRecoverToRoot = nextItems.Count == 0
                    && (_allFolders.Count > 0 || _allRecipes.Count > 0)
                    && HasActiveVisibilityConstraints(searchText, selectedLabelIds, selectedIngredientNames);

                List<object>? recoveryItems = null;
                if (shouldAutoRecoverToRoot)
                {
                    var rootFolders = GetFoldersForCurrentFolder(null);
                    var rootRecipes = GetRecipesForCurrentFolder(null);

                    recoveryItems = await Task.Run(
                        () => ComposeFilteredItems(
                            rootFolders,
                            rootRecipes,
                            string.Empty,
                            Array.Empty<Guid>(),
                            Array.Empty<string>(),
                            currentSortBy,
                            currentSortOrder,
                            suppressFolderAnimation: false),
                        cancellationToken).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested || version != _filterVersion)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested || version != _filterVersion)
                    {
                        return;
                    }

                    if (resetInvalidFolderBreadcrumb)
                    {
                        Breadcrumb.Clear();
                    }

                    if (shouldAutoRecoverToRoot && recoveryItems != null)
                    {
                        ResetVisibilityConstraints();
                        ApplyItemsSnapshot(recoveryItems);
                    }
                    else
                    {
                        ApplyItemsSnapshot(nextItems);
                    }

                    _suppressNextFolderEntryAnimation = false;
                    UpdateNavigationPathState();
                });
            }
            catch (OperationCanceledException)
            {
                // Newer state superseded this filter request.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeViewModel] ApplyFilterAsync error: {ex.Message}");
            }
        }

        private static bool HasActiveVisibilityConstraints(
            string searchText,
            Guid[] selectedLabelIds,
            string[] selectedIngredientNames)
            => !string.IsNullOrWhiteSpace(searchText)
                || selectedLabelIds.Length > 0
                || selectedIngredientNames.Length > 0;

        private void ResetVisibilityConstraints()
        {
            _currentFolder = null;
            Breadcrumb.Clear();

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                _searchText = string.Empty;
                OnPropertyChanged(nameof(SearchText));
            }

            if (_selectedLabelIds.Count > 0)
            {
                _selectedLabelIds.Clear();
                OnPropertyChanged(nameof(SelectedLabelIds));
            }

            if (_selectedIngredientNames.Count > 0)
            {
                _selectedIngredientNames.Clear();
                OnPropertyChanged(nameof(SelectedIngredientNames));
            }
        }

        private List<Folder> GetFoldersForCurrentFolder(Guid? currentFolderId)
        {
            var lookupKey = NormalizeFolderKey(currentFolderId);

            if (_foldersByParent.TryGetValue(lookupKey, out var folders))
            {
                return folders.ToList();
            }

            return new List<Folder>();
        }

        private List<Recipe> GetRecipesForCurrentFolder(Guid? currentFolderId)
        {
            var lookupKey = NormalizeFolderKey(currentFolderId);

            if (currentFolderId is null)
            {
                if (_recipesByFolder.TryGetValue(lookupKey, out var rootRecipes))
                {
                    return rootRecipes.ToList();
                }

                return new List<Recipe>();
            }

            return _recipesByFolder.TryGetValue(lookupKey, out var recipes)
                ? recipes.ToList()
                : new List<Recipe>();
        }

        private static List<object> ComposeFilteredItems(
            List<Folder> folders,
            List<Recipe> recipes,
            string searchText,
            Guid[] selectedLabelIds,
            string[] selectedIngredientNames,
            SortBy? currentSortBy,
            SortOrder sortOrder,
            bool suppressFolderAnimation)
        {
            IEnumerable<Folder> folderQuery = folders;
            IEnumerable<Recipe> recipeQuery = recipes;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                folderQuery = folderQuery.Where(folder => folder.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase));
                recipeQuery = recipeQuery.Where(recipe =>
                    recipe.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || (recipe.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (selectedLabelIds.Length > 0)
            {
                var selectedLabelSet = new HashSet<Guid>(selectedLabelIds);
                recipeQuery = recipeQuery.Where(recipe => recipe.Labels?.Any(label => selectedLabelSet.Contains(label.Id)) ?? false);
            }

            if (selectedIngredientNames.Length > 0)
            {
                var selectedIngredientSet = new HashSet<string>(selectedIngredientNames, StringComparer.OrdinalIgnoreCase);
                recipeQuery = recipeQuery.Where(recipe =>
                    recipe.Ingredients?.Any(ingredient =>
                        !string.IsNullOrEmpty(ingredient.Name)
                        && selectedIngredientSet.Contains(ingredient.Name)) ?? false);
            }

            var orderedFolders = folderQuery
                .OrderBy(folder => folder.Order)
                .ThenBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (suppressFolderAnimation)
            {
                foreach (var folder in orderedFolders)
                {
                    folder.AnimateOnNextRender = false;
                    folder.EntryAnimationDelayMs = 0;
                }
            }

            var orderedRecipes = SortRecipes(recipeQuery, currentSortBy, sortOrder).ToList();

            var items = new List<object>(orderedFolders.Count + orderedRecipes.Count);
            items.AddRange(orderedFolders);
            items.AddRange(orderedRecipes);
            return items;
        }

        private static IEnumerable<Recipe> SortRecipes(
            IEnumerable<Recipe> recipeQuery,
            SortBy? currentSortBy,
            SortOrder sortOrder)
        {
            if (currentSortBy.HasValue)
            {
                return currentSortBy.Value switch
                {
                    SortBy.NameAsc => recipeQuery.OrderBy(recipe => recipe.Name, StringComparer.CurrentCultureIgnoreCase),
                    SortBy.NameDesc => recipeQuery.OrderByDescending(recipe => recipe.Name, StringComparer.CurrentCultureIgnoreCase),
                    SortBy.CaloriesAsc => recipeQuery.OrderBy(recipe => recipe.Calories),
                    SortBy.CaloriesDesc => recipeQuery.OrderByDescending(recipe => recipe.Calories),
                    SortBy.ProteinAsc => recipeQuery.OrderBy(recipe => recipe.Protein),
                    SortBy.ProteinDesc => recipeQuery.OrderByDescending(recipe => recipe.Protein),
                    SortBy.CarbsAsc => recipeQuery.OrderBy(recipe => recipe.Carbs),
                    SortBy.CarbsDesc => recipeQuery.OrderByDescending(recipe => recipe.Carbs),
                    SortBy.FatAsc => recipeQuery.OrderBy(recipe => recipe.Fat),
                    SortBy.FatDesc => recipeQuery.OrderByDescending(recipe => recipe.Fat),
                    _ => recipeQuery
                };
            }

            return sortOrder == SortOrder.Asc
                ? recipeQuery.OrderBy(recipe => recipe.Name, StringComparer.CurrentCultureIgnoreCase)
                : recipeQuery.OrderByDescending(recipe => recipe.Name, StringComparer.CurrentCultureIgnoreCase);
        }

        private void ApplyItemsSnapshot(IReadOnlyList<object> nextItems)
        {
            if (Items.Count == nextItems.Count)
            {
                var unchanged = true;
                for (var i = 0; i < nextItems.Count; i++)
                {
                    if (!ReferenceEquals(Items[i], nextItems[i]))
                    {
                        unchanged = false;
                        break;
                    }
                }

                if (unchanged)
                {
                    return;
                }
            }

            Items.Clear();
            foreach (var item in nextItems)
            {
                Items.Add(item);
            }
        }

        private void RebuildNavigationIndexes()
        {
            _foldersByParent.Clear();
            _recipesByFolder.Clear();
            _folderIds.Clear();

            foreach (var folder in _allFolders)
            {
                var parentKey = NormalizeFolderKey(folder.ParentFolderId);
                if (!_foldersByParent.TryGetValue(parentKey, out var folderBucket))
                {
                    folderBucket = new List<Folder>();
                    _foldersByParent[parentKey] = folderBucket;
                }

                folderBucket.Add(folder);
                _folderIds.Add(folder.Id);
            }

            foreach (var pair in _foldersByParent)
            {
                pair.Value.Sort(static (left, right) =>
                {
                    var byOrder = left.Order.CompareTo(right.Order);
                    return byOrder != 0
                        ? byOrder
                        : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
                });
            }

            foreach (var recipe in _allRecipes)
            {
                var folderKey = NormalizeFolderKey(recipe.FolderId);
                if (!_recipesByFolder.TryGetValue(folderKey, out var recipeBucket))
                {
                    recipeBucket = new List<Recipe>();
                    _recipesByFolder[folderKey] = recipeBucket;
                }

                recipeBucket.Add(recipe);
            }
        }

        private static Guid NormalizeFolderKey(Guid? folderId)
            => folderId is null || folderId == Guid.Empty
                ? Guid.Empty
                : folderId.Value;

        private void UpdateNavigationPathState()
        {
            _currentPathDisplay = Breadcrumb.Count == 0
                ? "/"
                : string.Join(" / ", Breadcrumb.Select(breadcrumb => breadcrumb.Name));

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
                                // ? CRITICAL FIX: Copy UnitWeight from database ingredient
                                ingredient.UnitWeight = dbIngredient.UnitWeight;
                            }
                            
                            // ? CRITICAL FIX: Pass UnitWeight to conversion factor calculation
                            double factor = GetUnitConversionFactor(ingredient.Unit, ingredient.Quantity, ingredient.UnitWeight);
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

        private double GetUnitConversionFactor(Unit unit, double quantity, double unitWeight) => unit switch
        {
            Unit.Gram => quantity / 100.0,
            Unit.Milliliter => quantity / 100.0,
            // ? CRITICAL FIX: Use unitWeight for Piece unit (matches Ingredient model logic)
            Unit.Piece => unitWeight > 0 ? (quantity * unitWeight) / 100.0 : quantity,
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
                GetFolderText("DeleteRecipeTitle", "Delete recipe"),
                string.Format(GetFolderText("DeleteRecipeConfirmFormat", "Are you sure you want to delete recipe '{0}'?"), recipe.Name),
                ButtonResources.Yes,
                ButtonResources.No);
            if (!confirm) return;

            try
            {
                await _recipeService.DeleteRecipeAsync(recipe.Id);
                Recipes.Remove(recipe);
                _allRecipes.Remove(recipe);
                RebuildNavigationIndexes();
                FilterItems();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting recipe: {ex.Message}");
            }
        }

        // New drag & drop commands
        public ICommand DragStartingCommand { get; private set; } = null!;
        public ICommand DragOverCommand { get; private set; } = null!;
        public ICommand DragLeaveCommand { get; private set; } = null!;
        public ICommand DropCommand { get; private set; } = null!;

        private void InitializeDragDrop()
        {
            DragStartingCommand = new Command<object>(o =>
            {
                _dragSource = o;
                switch (o)
                {
                    case Recipe r:
                        r.IsBeingDragged = true;
                        break;
                    case Folder f:
                        f.IsBeingDragged = true;
                        break;
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
                    switch (payload)
                    {
                        case Foodbook.Views.Components.DragDropInfo info:
                            await HandleDropAsync(info);
                            break;
                        case Folder f:
                            // backward compat: treat as drop ON
                            await HandleDropAsync(new Foodbook.Views.Components.DragDropInfo(_dragSource, f, DropIntent.On));
                            break;
                    }
                }
                finally
                {
                    // clear flags
                    switch (_dragSource)
                    {
                        case Recipe r2: r2.IsBeingDragged = false; break;
                        case Folder f2: f2.IsBeingDragged = false; break;
                    }
                    if (payload is Folder f3) f3.IsBeingDraggedOver = false;
                    _dragSource = null;
                }
            });
        }

        private async Task HandleDropAsync(Foodbook.Views.Components.DragDropInfo info)
        {
            var source = info.Source;
            var target = info.Target;

            // 1) Recipe dropped onto folder moves recipe under that folder
            if (source is Recipe draggedRecipe && target is Folder targetFolder && info.Intent == DropIntent.On)
            {
                draggedRecipe.FolderId = targetFolder.Id;
                await _recipeService.UpdateRecipeAsync(draggedRecipe);
                RebuildNavigationIndexes();
                FilterItems();
                return;
            }

            // 2) Folder dropped onto folder: move folder under target folder
            if (source is Folder draggedFolder)
            {
                if (target is Folder targetFolder2)
                {
                    // Same-parent reorder not handled here due to lack of intent/insert index in UI.Try move under folder when On
                    if (info.Intent == DropIntent.On)
                    {
                        if (await _folderService.IsValidFolderMoveAsync(draggedFolder.Id, targetFolder2.Id))
                        {
                            await _folderService.MoveFolderAsync(draggedFolder.Id, targetFolder2.Id);
                            await LoadRecipesAsync();
                        }
                        return;
                    }
                }

                // Reorder among siblings when target is Folder, intent Before/After
                if (target is Folder sibling && (info.Intent == DropIntent.Before || info.Intent == DropIntent.After))
                {
                    // Only when same parent; otherwise keep parent and move index in that parent
                    var parentId = sibling.ParentFolderId;
                    // Build siblings list for index
                    var siblings = (_currentFolder == null ? _allFolders.Where(f => f.ParentFolderId == null) : _allFolders.Where(f => f.ParentFolderId == _currentFolder.Id))
                        .OrderBy(f => f.Order).ThenBy(f => f.Name).ToList();

                    int targetIndex = siblings.FindIndex(f => f.Id == sibling.Id);
                    if (targetIndex < 0) return;
                    if (info.Intent == DropIntent.After) targetIndex++;

                    // If dragged folder has different parent, first set its parent to that parent
                    if (draggedFolder.ParentFolderId != parentId)
                    {
                        var moved = await _folderService.MoveFolderAsync(draggedFolder.Id, parentId);
                        if (!moved) return;
                        // refresh local copy parent
                        draggedFolder.ParentFolderId = parentId;
                    }

                    await _folderService.ReorderFolderAsync(draggedFolder.Id, parentId, targetIndex);
                    await LoadRecipesAsync();
                    return;
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Move dragged recipe one level up (to parent of current folder, or root when no parent)
        public async Task MoveRecipeUpAsync(Recipe recipe)
        {
            if (recipe == null) return;
            if (_currentFolder == null) return; // at root, nothing to move up to

            recipe.FolderId = _currentFolder.ParentFolderId; // null when parent is root
            await _recipeService.UpdateRecipeAsync(recipe);
            RebuildNavigationIndexes();
            FilterItems();
        }

        // Public API used by TabBar to reset folder navigation to root when switching tabs
        public void ResetFolderNavigation()
        {
            try
            {
                if (Breadcrumb.Count > 0 || _currentFolder != null)
                {
                    Breadcrumb.Clear();
                    _currentFolder = null;
                    _suppressNextFolderEntryAnimation = true;
                    UpdateNavigationPathState();
                    FilterItems();
                    OnPropertyChanged(nameof(CanGoBack));
                    (GoBackCommand as Command)?.ChangeCanExecute();
                    System.Diagnostics.Debug.WriteLine("[RecipeViewModel] Folder navigation reset to root");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RecipeViewModel] ResetFolderNavigation error: {ex.Message}");
            }
        }

        private static SortOrder MapSortByToSortOrder(SortBy sortBy)
            => sortBy is SortBy.NameDesc
                or SortBy.CaloriesDesc
                or SortBy.ProteinDesc
                or SortBy.CarbsDesc
                or SortBy.FatDesc
                ? SortOrder.Desc
                : SortOrder.Asc;

        private static string GetFolderText(string key, string fallback)
            => FolderResources.ResourceManager.GetString(key, FolderResources.Culture) ?? fallback;
    }
}