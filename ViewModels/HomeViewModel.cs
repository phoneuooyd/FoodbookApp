using System.ComponentModel;
using System.Runtime.CompilerServices;
using Foodbook.Services;

namespace Foodbook.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    private readonly IRecipeService _recipeService;
    private readonly IPlanService _planService;

    private int _recipeCount;
    public int RecipeCount
    {
        get => _recipeCount;
        set { if (_recipeCount != value) { _recipeCount = value; OnPropertyChanged(); } }
    }

    private int _planCount;
    public int PlanCount
    {
        get => _planCount;
        set { if (_planCount != value) { _planCount = value; OnPropertyChanged(); } }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); } }
    }

    public HomeViewModel(IRecipeService recipeService, IPlanService planService)
    {
        _recipeService = recipeService;
        _planService = planService;
    }

    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        var recipes = await _recipeService.GetRecipesAsync();
        RecipeCount = recipes.Count;

        var plans = await _planService.GetPlansAsync();
        PlanCount = plans.Count;

        IsLoading = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

