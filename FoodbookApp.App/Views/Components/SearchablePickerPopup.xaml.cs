using CommunityToolkit.Maui.Views;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Views;
using Foodbook.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;
using System.Collections.ObjectModel;

namespace Foodbook.Views.Components;

public partial class SearchablePickerPopup : Popup, INotifyPropertyChanged
{
    private readonly List<string> _allItems;
    private readonly TaskCompletionSource<object?> _tcs = new();
    private CancellationTokenSource? _searchDebounceCts;

    public Task<object?> ResultTask => _tcs.Task;

    public ICommand CloseCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand AddIngredientCommand { get; }
    public ICommand SelectItemCommand { get; }

    public ObservableCollection<string> FilteredItems { get; } = new();

    public bool ShowAddIngredientButton { get; private set; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            DebounceApplySearch();
        }
    }

    public SearchablePickerPopup(List<string> allItems, string? selected)
    {
        _allItems = allItems?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        CloseCommand = new Command(async () => await CloseWithResultAsync(null));
        ClearSearchCommand = new Command(() => SearchText = string.Empty);
        AddIngredientCommand = new Command(async () => await OnAddIngredientAsync());
        SelectItemCommand = new Command<string>(async item => await CloseWithResultAsync(item));

        InitializeComponent();
        BindingContext = this;
        DetectContext();
        ApplySearchCore();
    }

    private void DetectContext()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            ShowAddIngredientButton = currentPage is Foodbook.Views.AddRecipePage;
            OnPropertyChanged(nameof(ShowAddIngredientButton));
        }
        catch
        {
            ShowAddIngredientButton = false;
        }
    }

    private void DebounceApplySearch()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested)
                    return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                        ApplySearchCore();
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private void ApplySearchCore()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        IEnumerable<string> filtered = string.IsNullOrWhiteSpace(query)
            ? _allItems
            : _allItems.Where(x => x.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

        SynchronizeFilteredItems(filtered);
    }

    private void SynchronizeFilteredItems(IEnumerable<string> items)
    {
        var target = items.ToList();

        if (FilteredItems.Count == target.Count && FilteredItems.SequenceEqual(target))
            return;

        int commonCount = Math.Min(FilteredItems.Count, target.Count);

        for (int i = 0; i < commonCount; i++)
        {
            if (!string.Equals(FilteredItems[i], target[i], StringComparison.Ordinal))
                FilteredItems[i] = target[i];
        }

        while (FilteredItems.Count > target.Count)
            FilteredItems.RemoveAt(FilteredItems.Count - 1);

        for (int i = commonCount; i < target.Count; i++)
            FilteredItems.Add(target[i]);
    }

    private async Task OnAddIngredientAsync()
    {
        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            if (currentPage == null)
                return;

            var vm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IngredientFormViewModel>();
            if (vm == null)
            {
                await Shell.Current.DisplayAlert(
                    FoodbookApp.Localization.AddRecipePageResources.ErrorTitle,
                    FoodbookApp.Localization.AddRecipePageResources.CouldNotOpenRecipeSelectionDialog,
                    FoodbookApp.Localization.AddRecipePageResources.OKButton);
                return;
            }

            vm.Reset();
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormViewModel reset before opening");

            var formPage = new IngredientFormPage(vm);

            try
            {
                SearchablePickerComponent.RaiseGlobalPopupStateChanged(this, true);
                System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Notified GlobalPopupStateChanged: opening modal");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Failed to notify GlobalPopupStateChanged: {ex.Message}");
            }

            var dismissedTcs = new TaskCompletionSource();
            formPage.Disappearing += (_, __) =>
            {
                try
                {
                    dismissedTcs.TrySetResult();
                    SearchablePickerComponent.RaiseGlobalPopupStateChanged(this, false);
                    System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] Notified GlobalPopupStateChanged: closing modal");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Error in modal closing notification: {ex.Message}");
                }
            };

            await currentPage.Navigation.PushModalAsync(formPage);
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormPage opened modally");

            await dismissedTcs.Task;
            System.Diagnostics.Debug.WriteLine("[SearchablePickerPopup] IngredientFormPage dismissed");

            await SynchronizeIngredientsAfterAddAsync(currentPage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Error adding ingredient: {ex.Message}");
            try
            {
                SearchablePickerComponent.RaiseGlobalPopupStateChanged(this, false);
            }
            catch { }
        }
    }

    private async Task SynchronizeIngredientsAfterAddAsync(Page currentPage)
    {
        try
        {
            var ingredientService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IIngredientService>();
            if (ingredientService == null)
                return;

            try { ingredientService.InvalidateCache(); } catch { }

            var fresh = await ingredientService.GetIngredientsAsync();
            var freshNames = fresh
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (currentPage is Foodbook.Views.AddRecipePage arp && arp.BindingContext is Foodbook.ViewModels.AddRecipeViewModel recipeVm)
            {
                recipeVm.SyncAvailableIngredientNames(freshNames);
            }

            SynchronizeAllItems(freshNames);

            await MainThread.InvokeOnMainThreadAsync(ApplySearchCore);

            try
            {
                if (IngredientsPage.Current != null)
                    await IngredientsPage.Current.ForceReloadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] IngredientsPage reload error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchablePickerPopup] Post-save refresh failed: {ex.Message}");
        }
    }

    private void SynchronizeAllItems(IReadOnlyList<string> freshNames)
    {
        var target = freshNames.ToList();

        for (int i = _allItems.Count - 1; i >= 0; i--)
        {
            if (!target.Contains(_allItems[i], StringComparer.OrdinalIgnoreCase))
                _allItems.RemoveAt(i);
        }

        foreach (var name in target)
        {
            if (!_allItems.Contains(name, StringComparer.OrdinalIgnoreCase))
                _allItems.Add(name);
        }
    }

    private async Task CloseWithResultAsync(object? result)
    {
        if (!_tcs.Task.IsCompleted)
            _tcs.SetResult(result);
        await CloseAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
