using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class IngredientFormViewModel : INotifyPropertyChanged
{
    private readonly IIngredientService _service;
    private Ingredient? _ingredient;

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    private string _name = string.Empty;

    public string Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }
    private string _quantity = string.Empty;

    public Unit SelectedUnit { get => _unit; set { _unit = value; OnPropertyChanged(); } }
    private Unit _unit;

    public IEnumerable<Unit> Units { get; } = Enum.GetValues(typeof(Unit)).Cast<Unit>();

    public ICommand SaveCommand { get; }

    public IngredientFormViewModel(IIngredientService service)
    {
        _service = service;
        SaveCommand = new Command(async () => await SaveAsync());
    }

    public async Task LoadAsync(int id)
    {
        var ing = await _service.GetIngredientAsync(id);
        if (ing != null)
        {
            _ingredient = ing;
            Name = ing.Name;
            Quantity = ing.Quantity.ToString();
            SelectedUnit = ing.Unit;
        }
    }

    private async Task SaveAsync()
    {
        var qty = double.TryParse(Quantity, out var q) ? q : 0;
        if (_ingredient == null)
        {
            var newIng = new Ingredient { Name = Name, Quantity = qty, Unit = SelectedUnit };
            await _service.AddIngredientAsync(newIng);
        }
        else
        {
            _ingredient.Name = Name;
            _ingredient.Quantity = qty;
            _ingredient.Unit = SelectedUnit;
            await _service.UpdateIngredientAsync(_ingredient);
        }
        await Shell.Current.GoToAsync("..");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
