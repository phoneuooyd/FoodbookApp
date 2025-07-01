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

    private int _archivedPlanCount;
    public int ArchivedPlanCount
    {
        get => _archivedPlanCount;
        set { if (_archivedPlanCount != value) { _archivedPlanCount = value; OnPropertyChanged(); } }
    }

    private bool _isLoading = true; // Zaczynamy z true, ¿eby pokazaæ loader
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
        try
        {
            IsLoading = true;

            // £aduj przepisy
            var recipes = await _recipeService.GetRecipesAsync();
            RecipeCount = recipes?.Count ?? 0;

            // £aduj plany
            var allPlans = await _planService.GetPlansAsync();
            if (allPlans != null)
            {
                PlanCount = allPlans.Count(p => !p.IsArchived);
                ArchivedPlanCount = allPlans.Count(p => p.IsArchived);
            }
            else
            {
                PlanCount = 0;
                ArchivedPlanCount = 0;
            }
        }
        catch (Exception ex)
        {
            // W przypadku b³êdu, ustaw wartoœci na 0
            RecipeCount = 0;
            PlanCount = 0;
            ArchivedPlanCount = 0;
            
            // Log b³êdu (opcjonalne)
            System.Diagnostics.Debug.WriteLine($"Error loading home data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

