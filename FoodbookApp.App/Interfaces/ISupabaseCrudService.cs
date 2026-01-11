using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface ISupabaseCrudService
{
    Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default);
    Task<Folder?> GetFolderAsync(Guid id, CancellationToken ct = default);
    Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default);
    Task UpdateFolderAsync(Folder folder, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid id, CancellationToken ct = default);

    Task<List<Recipe>> GetRecipesAsync(CancellationToken ct = default);
    Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken ct = default);
    Task<Recipe> AddRecipeAsync(Recipe recipe, CancellationToken ct = default);
    Task UpdateRecipeAsync(Recipe recipe, CancellationToken ct = default);
    Task DeleteRecipeAsync(Guid id, CancellationToken ct = default);

    Task<List<Ingredient>> GetIngredientsAsync(CancellationToken ct = default);
    Task<Ingredient?> GetIngredientAsync(Guid id, CancellationToken ct = default);
    Task<Ingredient> AddIngredientAsync(Ingredient ingredient, CancellationToken ct = default);
    Task UpdateIngredientAsync(Ingredient ingredient, CancellationToken ct = default);
    Task DeleteIngredientAsync(Guid id, CancellationToken ct = default);

    Task<List<Plan>> GetPlansAsync(CancellationToken ct = default);
    Task<Plan?> GetPlanAsync(Guid id, CancellationToken ct = default);
    Task<Plan> AddPlanAsync(Plan plan, CancellationToken ct = default);
    Task UpdatePlanAsync(Plan plan, CancellationToken ct = default);
    Task DeletePlanAsync(Guid id, CancellationToken ct = default);

    Task<List<PlannedMeal>> GetPlannedMealsAsync(CancellationToken ct = default);
    Task<List<PlannedMeal>> GetPlannedMealsByPlanAsync(Guid planId, CancellationToken ct = default);
    Task<PlannedMeal?> GetPlannedMealAsync(Guid id, CancellationToken ct = default);
    Task<PlannedMeal> AddPlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default);
    Task UpdatePlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default);
    Task DeletePlannedMealAsync(Guid id, CancellationToken ct = default);

    Task<List<ShoppingListItem>> GetShoppingListItemsAsync(Guid planId, CancellationToken ct = default);
    Task<ShoppingListItem?> GetShoppingListItemAsync(Guid id, CancellationToken ct = default);
    Task<ShoppingListItem> AddShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default);
    Task UpdateShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default);
    Task DeleteShoppingListItemAsync(Guid id, CancellationToken ct = default);

    Task<List<RecipeLabel>> GetRecipeLabelsAsync(CancellationToken ct = default);
    Task<RecipeLabel?> GetRecipeLabelAsync(Guid id, CancellationToken ct = default);
    Task<RecipeLabel> AddRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default);
    Task UpdateRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default);
    Task DeleteRecipeLabelAsync(Guid id, CancellationToken ct = default);
}
