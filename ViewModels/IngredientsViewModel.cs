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

    public ObservableCollection<Ingredient> Ingredients { get; } = new();

    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }

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
    }

    public async Task LoadAsync()
    {
        Ingredients.Clear();
        var list = await _service.GetIngredientsAsync();
        foreach (var i in list)
            Ingredients.Add(i);
    }

    private async Task DeleteIngredientAsync(Ingredient? ing)
    {
        if (ing == null) return;
        await _service.DeleteIngredientAsync(ing.Id);
        Ingredients.Remove(ing);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
