using Foodbook.Models;

namespace FoodbookApp.Interfaces;

public interface ISupabaseCrudService
{
    // Folders
    Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default);
    Task<Folder?> GetFolderAsync(Guid id, CancellationToken ct = default);
    Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default);
    Task UpdateFolderAsync(Folder folder, CancellationToken ct = default);
    Task DeleteFolderAsync(Guid id, CancellationToken ct = default);

    // Recipes
    Task<List<Recipe>> GetRecipesAsync(CancellationToken ct = default);
    Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken ct = default);
    Task<Recipe> AddRecipeAsync(Recipe recipe, CancellationToken ct = default);
    Task UpdateRecipeAsync(Recipe recipe, CancellationToken ct = default);
    Task DeleteRecipeAsync(Guid id, CancellationToken ct = default);

    // Ingredients
    Task<List<Ingredient>> GetIngredientsAsync(CancellationToken ct = default);
    Task<Ingredient?> GetIngredientAsync(Guid id, CancellationToken ct = default);
    Task<Ingredient> AddIngredientAsync(Ingredient ingredient, CancellationToken ct = default);
    Task UpdateIngredientAsync(Ingredient ingredient, CancellationToken ct = default);
    Task DeleteIngredientAsync(Guid id, CancellationToken ct = default);

    // Plans
    Task<List<Plan>> GetPlansAsync(CancellationToken ct = default);
    Task<Plan?> GetPlanAsync(Guid id, CancellationToken ct = default);
    Task<Plan> AddPlanAsync(Plan plan, CancellationToken ct = default);
    Task UpdatePlanAsync(Plan plan, CancellationToken ct = default);
    Task DeletePlanAsync(Guid id, CancellationToken ct = default);

    // PlannedMeals
    Task<List<PlannedMeal>> GetPlannedMealsAsync(CancellationToken ct = default);
    Task<List<PlannedMeal>> GetPlannedMealsByPlanAsync(Guid planId, CancellationToken ct = default);
    Task<PlannedMeal?> GetPlannedMealAsync(Guid id, CancellationToken ct = default);
    Task<PlannedMeal> AddPlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default);
    Task UpdatePlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default);
    Task DeletePlannedMealAsync(Guid id, CancellationToken ct = default);

    // ShoppingListItems
    Task<List<ShoppingListItem>> GetShoppingListItemsAsync(Guid planId, CancellationToken ct = default);
    Task<ShoppingListItem?> GetShoppingListItemAsync(Guid id, CancellationToken ct = default);
    Task<ShoppingListItem> AddShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default);
    Task UpdateShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default);
    Task DeleteShoppingListItemAsync(Guid id, CancellationToken ct = default);

    // RecipeLabels
    Task<List<RecipeLabel>> GetRecipeLabelsAsync(CancellationToken ct = default);
    Task<RecipeLabel?> GetRecipeLabelAsync(Guid id, CancellationToken ct = default);
    Task<RecipeLabel> AddRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default);
    Task UpdateRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default);
    Task DeleteRecipeLabelAsync(Guid id, CancellationToken ct = default);

    // === BATCH OPERATIONS (REST API) ===
    
    /// <summary>
    /// Insert multiple recipes in a single batch request
    /// </summary>
    Task<List<Recipe>> AddRecipesBatchAsync(IEnumerable<Recipe> recipes, CancellationToken ct = default);
    
    /// <summary>
    /// Insert multiple ingredients in a single batch request
    /// </summary>
    Task<List<Ingredient>> AddIngredientsBatchAsync(IEnumerable<Ingredient> ingredients, CancellationToken ct = default);
    
    /// <summary>
    /// Insert multiple planned meals in a single batch request
    /// </summary>
    Task<List<PlannedMeal>> AddPlannedMealsBatchAsync(IEnumerable<PlannedMeal> meals, CancellationToken ct = default);
    
    /// <summary>
    /// Insert multiple shopping list items in a single batch request
    /// </summary>
    Task<List<ShoppingListItem>> AddShoppingListItemsBatchAsync(IEnumerable<ShoppingListItem> items, CancellationToken ct = default);
    
    /// <summary>
    /// Upsert (insert or update) multiple recipes
    /// </summary>
    Task<List<Recipe>> UpsertRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken ct = default);
    
    /// <summary>
    /// Upsert (insert or update) multiple ingredients
    /// </summary>
    Task<List<Ingredient>> UpsertIngredientsAsync(IEnumerable<Ingredient> ingredients, CancellationToken ct = default);
}
