using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;
using Foodbook.Views;
using Foodbook.Data;

namespace Foodbook.ViewModels;

public class IngredientsViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private bool _isLoading;
    private bool _isRefreshing;
    private string _searchText = string.Empty;
    private List<Ingredient> _allIngredients = new();

    public ObservableCollection<Ingredient> Ingredients { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            FilterIngredients();
        }
    }

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RefreshCommand { get; }

    public IngredientsViewModel(IIngredientService service)
    {
        _service = service;
        AddCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(IngredientFormPage)));
        EditCommand = new Command<Ingredient>(async ing =>
        {
            if (ing != null)
                await Shell.Current.GoToAsync($"{nameof(IngredientFormPage)}?id={ing.Id}");
        });
        DeleteCommand = new Command<Ingredient>(async ing => await DeleteIngredientAsync(ing));
        RefreshCommand = new Command(async () => await ReloadAsync());
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            var list = await _service.GetIngredientsAsync();
            _allIngredients = list;
            
            // Clear and add in batches to improve UI responsiveness
            Ingredients.Clear();
            
            // Add items in smaller batches to prevent UI blocking
            const int batchSize = 50;
            for (int i = 0; i < list.Count; i += batchSize)
            {
                var batch = list.Skip(i).Take(batchSize);
                foreach (var ingredient in batch)
                {
                    Ingredients.Add(ingredient);
                }
                
                // Allow UI to update between batches
                if (i + batchSize < list.Count)
                {
                    await Task.Delay(1);
                }
            }
            
            FilterIngredients();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading ingredients: {ex.Message}");
            // Could show user-friendly error message here
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadAsync()
    {
        if (IsRefreshing) return;
        
        IsRefreshing = true;
        try
        {
            await LoadAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void FilterIngredients()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // If no search text, show all ingredients
            if (Ingredients.Count != _allIngredients.Count)
            {
                Ingredients.Clear();
                foreach (var ingredient in _allIngredients)
                {
                    Ingredients.Add(ingredient);
                }
            }
        }
        else
        {
            // Filter ingredients based on search text
            var filtered = _allIngredients
                .Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            Ingredients.Clear();
            foreach (var ingredient in filtered)
            {
                Ingredients.Add(ingredient);
            }
        }
    }

    private async Task DeleteIngredientAsync(Ingredient? ing)
    {
        if (ing == null) return;
        
        try
        {
            await _service.DeleteIngredientAsync(ing.Id);
            Ingredients.Remove(ing);
            _allIngredients.Remove(ing);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting ingredient: {ex.Message}");
            // Could show user-friendly error message here
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
