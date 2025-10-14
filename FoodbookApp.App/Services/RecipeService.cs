using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;
using FoodbookApp.Interfaces;

namespace Foodbook.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly AppDbContext _context;

        public RecipeService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ? KRYTYCZNA OPTYMALIZACJA: AsNoTracking dla odczytu
        public async Task<List<Recipe>> GetRecipesAsync() => 
            await _context.Recipes
                .AsNoTracking() // Eliminuje tracking conflicts
                .Include(r => r.Ingredients)
                .Include(r => r.Labels)
                .ToListAsync();

        // ? KRYTYCZNA OPTYMALIZACJA: AsNoTracking dla pojedynczego odczytu
        public async Task<Recipe?> GetRecipeAsync(int id) => 
            await _context.Recipes
                .AsNoTracking() // Eliminuje tracking conflicts
                .Include(r => r.Ingredients)
                .Include(r => r.Labels)
                .FirstOrDefaultAsync(r => r.Id == id);

        // ? KRYTYCZNA OPTYMALIZACJA: Kontrolowane dodawanie
        public async Task AddRecipeAsync(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (recipe.Ingredients == null)
                recipe.Ingredients = new List<Ingredient>();
            if (recipe.Labels == null)
                recipe.Labels = new List<RecipeLabel>();

            try
            {
                // Detach wszystkich sk?adnik?w przed dodaniem
                foreach (var ingredient in recipe.Ingredients)
                {
                    var existingEntry = _context.Entry(ingredient);
                    if (existingEntry.State != EntityState.Detached)
                    {
                        existingEntry.State = EntityState.Detached;
                    }
                    
                    // Reset ID dla nowych sk?adnik?w przepisu
                    if (ingredient.RecipeId == null)
                    {
                        ingredient.Id = 0; // Nowy sk?adnik przepisu
                    }
                }

                // Attach existing labels (by Id) to avoid duplicates
                var attachedLabels = new List<RecipeLabel>();
                foreach (var label in recipe.Labels)
                {
                    if (label.Id > 0)
                    {
                        var tracked = await _context.RecipeLabels.FindAsync(label.Id);
                        if (tracked != null) attachedLabels.Add(tracked);
                    }
                    else
                    {
                        attachedLabels.Add(label);
                    }
                }
                recipe.Labels = attachedLabels;

                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();
                
                System.Diagnostics.Debug.WriteLine($"? Added recipe: {recipe.Name} with {recipe.Ingredients.Count} ingredients and {recipe.Labels.Count} labels");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error adding recipe: {ex.Message}");
                
                // Clear tracking na b??d
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        // ? KRYTYCZNA OPTYMALIZACJA: Transakcyjne updatey
        public async Task UpdateRecipeAsync(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                // Pobierz istniej?cy przepis z sk?adnikami
                var existingRecipe = await _context.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Labels)
                    .FirstOrDefaultAsync(r => r.Id == recipe.Id);

                if (existingRecipe == null)
                {
                    throw new InvalidOperationException($"Recipe with ID {recipe.Id} not found");
                }

                // Aktualizuj podstawowe w?a?ciwo?ci przepisu
                existingRecipe.Name = recipe.Name;
                existingRecipe.Description = recipe.Description;
                existingRecipe.Calories = recipe.Calories;
                existingRecipe.Protein = recipe.Protein;
                existingRecipe.Fat = recipe.Fat;
                existingRecipe.Carbs = recipe.Carbs;
                existingRecipe.IloscPorcji = recipe.IloscPorcji;
                existingRecipe.FolderId = recipe.FolderId; // FIX: persist folder assignment

                // Ingredients: reset and add new
                var existingIngredients = existingRecipe.Ingredients.ToList();
                _context.Ingredients.RemoveRange(existingIngredients);

                foreach (var ingredient in recipe.Ingredients)
                {
                    var newIngredient = new Ingredient
                    {
                        Name = ingredient.Name,
                        Quantity = ingredient.Quantity,
                        Unit = ingredient.Unit,
                        Calories = ingredient.Calories,
                        Protein = ingredient.Protein,
                        Fat = ingredient.Fat,
                        Carbs = ingredient.Carbs,
                        RecipeId = recipe.Id
                    };
                    
                    _context.Ingredients.Add(newIngredient);
                }

                // Labels: replace with provided set
                existingRecipe.Labels.Clear();
                foreach (var label in recipe.Labels)
                {
                    if (label.Id > 0)
                    {
                        var tracked = await _context.RecipeLabels.FindAsync(label.Id);
                        if (tracked != null) existingRecipe.Labels.Add(tracked);
                    }
                    else
                    {
                        existingRecipe.Labels.Add(label);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                System.Diagnostics.Debug.WriteLine($"? Updated recipe: {recipe.Name} with {recipe.Ingredients.Count} ingredients and {recipe.Labels.Count} labels");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"? Error updating recipe: {ex.Message}");
                
                // Clear tracking na b??d
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task DeleteRecipeAsync(int id)
        {
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error deleting recipe: {ex.Message}");
                throw;
            }
        }
    }
}