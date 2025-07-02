using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Foodbook.Models;
using Foodbook.Services;
using Microsoft.Maui.Controls;

namespace Foodbook.ViewModels;

public class PlannedMealFormViewModel : INotifyPropertyChanged
{
    private readonly IPlannerService _plannerService;
    private readonly IRecipeService _recipeService;
    private PlannedMeal? _meal;

    public ObservableCollection<Recipe> Recipes { get; } = new();

    public string Title => _meal == null ? FoodbookApp.Resources.MealFormPage.NewTitle : FoodbookApp.Resources.MealFormPage.EditTitle;

    public string SaveButtonText => _meal == null ? FoodbookApp.Resources.MealFormPage.AddButton : FoodbookApp.Resources.MealFormPage.SaveButton;

    public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; OnPropertyChanged(); } }
    private string _validationMessage = string.Empty;

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationMessage);

    public Recipe? SelectedRecipe { get => _selectedRecipe; set { _selectedRecipe = value; OnPropertyChanged(); ValidateInput(); } }
    private Recipe? _selectedRecipe;

    public DateTime Date { get => _date; set { _date = value; OnPropertyChanged(); ValidateInput(); } }
    private DateTime _date = DateTime.Today;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public PlannedMealFormViewModel(IPlannerService plannerService, IRecipeService recipeService)
    {
        _plannerService = plannerService;
        _recipeService = recipeService;
        SaveCommand = new Command(async () => await SaveAsync(), CanSave);
        CancelCommand = new Command(async () => await CancelAsync());
        ValidateInput();
    }

    public async Task LoadAsync(int id)
    {
        await LoadRecipesAsync();
        var meal = await _plannerService.GetPlannedMealAsync(id);
        if (meal != null)
        {
            _meal = meal;
            Date = meal.Date;
            SelectedRecipe = Recipes.FirstOrDefault(r => r.Id == meal.RecipeId);
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SaveButtonText));
        }
        ValidateInput();
    }

    public async Task LoadRecipesAsync()
    {
        Recipes.Clear();
        var rec = await _recipeService.GetRecipesAsync();
        foreach (var r in rec)
            Recipes.Add(r);
        ValidateInput();
    }

    private bool CanSave()
    {
        return !HasValidationError;
    }

    private void ValidateInput()
    {
        ValidationMessage = string.Empty;

        if (SelectedRecipe == null)
        {
            ValidationMessage = FoodbookApp.Resources.MealFormPage.SelectRecipe;
        }

        OnPropertyChanged(nameof(HasValidationError));
        ((Command)SaveCommand).ChangeCanExecute();
    }

    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private async Task SaveAsync()
    {
        ValidateInput();
        if (HasValidationError)
            return;
        if (_meal == null)
        {
            var m = new PlannedMeal { RecipeId = SelectedRecipe.Id, Date = Date };
            await _plannerService.AddPlannedMealAsync(m);
        }
        else
        {
            _meal.RecipeId = SelectedRecipe.Id;
            _meal.Date = Date;
            await _plannerService.UpdatePlannedMealAsync(_meal);
        }
        await Shell.Current.GoToAsync("..");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
