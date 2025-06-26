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
    private string _quantity = "100";  // Default value

    public Unit SelectedUnit { get => _unit; set { _unit = value; OnPropertyChanged(); } }
    private Unit _unit = Unit.Gram;  // Default value

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
        // Don't save if the name is empty
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("B³¹d", "Nazwa sk³adnika nie mo¿e byæ pusta", "OK");
            return;
        }

        var qty = double.TryParse(Quantity, out var q) ? q : 0;
        
        try
        {
            if (_ingredient == null)
            {
                var newIng = new Ingredient 
                { 
                    Name = Name, 
                    Quantity = qty, 
                    Unit = SelectedUnit,
                    RecipeId = null  // Explicitly set to null for standalone ingredients
                };
                
                await _service.AddIngredientAsync(newIng);
                System.Diagnostics.Debug.WriteLine($"Added new ingredient: {Name}, {qty}, {SelectedUnit}");
            }
            else
            {
                _ingredient.Name = Name;
                _ingredient.Quantity = qty;
                _ingredient.Unit = SelectedUnit;
                // Preserve RecipeId
                await _service.UpdateIngredientAsync(_ingredient);
                System.Diagnostics.Debug.WriteLine($"Updated ingredient: {Name}, {qty}, {SelectedUnit}");
            }
            
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving ingredient: {ex.Message}");
            await Shell.Current.DisplayAlert("B³¹d", $"Nie uda³o siê zapisaæ sk³adnika: {ex.Message}", "OK");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
