using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;
using FoodbookApp.Interfaces;
using Foodbook.Services;

namespace Foodbook.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly AppDbContext _context;

        public RecipeService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Recipe>> GetRecipesAsync()
        {
            try
            {
                // Clear any stale tracking state before query
                _context.ChangeTracker.Clear();
                
                return await _context.Recipes
                    .AsNoTracking()
                    .Include(r => r.Ingredients)
                    .Include(r => r.Labels)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? GetRecipesAsync error: {ex.Message}");
                return new List<Recipe>();
            }
        }

        public async Task<Recipe?> GetRecipeAsync(Guid id)
        {
            try
            {
                if (id == Guid.Empty)
                {
                    System.Diagnostics.Debug.WriteLine("?? GetRecipeAsync called with Guid.Empty");
                    return null;
                }

                // Clear any stale tracking state before query
                _context.ChangeTracker.Clear();
                
                return await _context.Recipes
                    .AsNoTracking()
                    .Include(r => r.Ingredients)
                    .Include(r => r.Labels)
                    .FirstOrDefaultAsync(r => r.Id == id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? GetRecipeAsync error for id {id}: {ex.Message}");
                return null;
            }
        }

        public async Task AddRecipeAsync(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            // Clear tracking to avoid conflicts
            _context.ChangeTracker.Clear();

            try
            {
                // Ensure recipe has valid ID
                if (recipe.Id == Guid.Empty)
                    recipe.Id = Guid.NewGuid();

                // Normalize FolderId: Guid.Empty ? null
                if (recipe.FolderId.HasValue && recipe.FolderId.Value == Guid.Empty)
                    recipe.FolderId = null;

                // Initialize collections if null
                recipe.Ingredients ??= new List<Ingredient>();
                recipe.Labels ??= new List<RecipeLabel>();

                // Process ingredients - create fresh instances to avoid tracking issues
                var ingredientsToAdd = new List<Ingredient>();
                foreach (var ingredient in recipe.Ingredients)
                {
                    var newIngredient = new Ingredient
                    {
                        Id = ingredient.Id == Guid.Empty ? Guid.NewGuid() : ingredient.Id,
                        Name = ingredient.Name ?? string.Empty,
                        Quantity = ingredient.Quantity,
                        Unit = ingredient.Unit,
                        UnitWeight = ingredient.UnitWeight,
                        Calories = ingredient.Calories,
                        Protein = ingredient.Protein,
                        Fat = ingredient.Fat,
                        Carbs = ingredient.Carbs,
                        RecipeId = recipe.Id
                    };
                    ingredientsToAdd.Add(newIngredient);
                }
                recipe.Ingredients = ingredientsToAdd;

                // Process labels - attach existing or create new
                var labelsToAttach = new List<RecipeLabel>();
                foreach (var label in recipe.Labels)
                {
                    if (label.Id != Guid.Empty)
                    {
                        var existingLabel = await _context.RecipeLabels.FindAsync(label.Id);
                        if (existingLabel != null)
                            labelsToAttach.Add(existingLabel);
                    }
                }
                recipe.Labels = labelsToAttach;

                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();

                System.Diagnostics.Debug.WriteLine($"? Added recipe: {recipe.Name} (Id: {recipe.Id})");

                // Raise events after successful save
                AppEvents.RaiseRecipeSaved(recipe.Id);
                AppEvents.RaiseRecipesChanged();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? AddRecipeAsync error: {ex.Message}");
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task UpdateRecipeAsync(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            if (recipe.Id == Guid.Empty)
                throw new ArgumentException("Recipe ID cannot be empty", nameof(recipe));

            // Clear tracking to avoid conflicts
            _context.ChangeTracker.Clear();

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var existingRecipe = await _context.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Labels)
                    .FirstOrDefaultAsync(r => r.Id == recipe.Id);

                if (existingRecipe == null)
                    throw new InvalidOperationException($"Recipe with ID {recipe.Id} not found");

                // Update basic properties
                existingRecipe.Name = recipe.Name ?? string.Empty;
                existingRecipe.Description = recipe.Description;
                existingRecipe.Calories = recipe.Calories;
                existingRecipe.Protein = recipe.Protein;
                existingRecipe.Fat = recipe.Fat;
                existingRecipe.Carbs = recipe.Carbs;
                existingRecipe.IloscPorcji = recipe.IloscPorcji;
                
                // Normalize FolderId: Guid.Empty ? null
                existingRecipe.FolderId = (recipe.FolderId.HasValue && recipe.FolderId.Value != Guid.Empty) 
                    ? recipe.FolderId 
                    : null;

                // Remove old ingredients
                if (existingRecipe.Ingredients.Any())
                {
                    _context.Ingredients.RemoveRange(existingRecipe.Ingredients);
                }

                // Add new ingredients
                recipe.Ingredients ??= new List<Ingredient>();
                foreach (var ingredient in recipe.Ingredients)
                {
                    var newIngredient = new Ingredient
                    {
                        Id = Guid.NewGuid(), // Always new ID for updated ingredients
                        Name = ingredient.Name ?? string.Empty,
                        Quantity = ingredient.Quantity,
                        Unit = ingredient.Unit,
                        UnitWeight = ingredient.UnitWeight,
                        Calories = ingredient.Calories,
                        Protein = ingredient.Protein,
                        Fat = ingredient.Fat,
                        Carbs = ingredient.Carbs,
                        RecipeId = recipe.Id
                    };
                    _context.Ingredients.Add(newIngredient);
                }

                // Update labels
                existingRecipe.Labels.Clear();
                recipe.Labels ??= new List<RecipeLabel>();
                foreach (var label in recipe.Labels)
                {
                    if (label.Id != Guid.Empty)
                    {
                        var existingLabel = await _context.RecipeLabels.FindAsync(label.Id);
                        if (existingLabel != null)
                            existingRecipe.Labels.Add(existingLabel);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                System.Diagnostics.Debug.WriteLine($"? Updated recipe: {recipe.Name} (Id: {recipe.Id})");

                // Raise events after successful save
                AppEvents.RaiseRecipeSaved(recipe.Id);
                AppEvents.RaiseRecipesChanged();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"? UpdateRecipeAsync error: {ex.Message}");
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task DeleteRecipeAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine("?? DeleteRecipeAsync called with Guid.Empty");
                return;
            }

            // Clear tracking to avoid conflicts
            _context.ChangeTracker.Clear();

            try
            {
                var recipe = await _context.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Labels)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (recipe != null)
                {
                    _context.Recipes.Remove(recipe);
                    await _context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"? Deleted recipe: {recipe.Name}");

                    AppEvents.RaiseRecipesChanged();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? DeleteRecipeAsync error: {ex.Message}");
                _context.ChangeTracker.Clear();
                throw;
            }
        }
    }
}