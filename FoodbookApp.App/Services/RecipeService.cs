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
                .ToListAsync();

        // ? KRYTYCZNA OPTYMALIZACJA: AsNoTracking dla pojedynczego odczytu
        public async Task<Recipe?> GetRecipeAsync(int id) => 
            await _context.Recipes
                .AsNoTracking() // Eliminuje tracking conflicts
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == id);

        // ? KRYTYCZNA OPTYMALIZACJA: Kontrolowane dodawanie
        public async Task AddRecipeAsync(Recipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            if (recipe.Ingredients == null)
                recipe.Ingredients = new List<Ingredient>();

            try
            {
                // Detach wszystkich sk³adników przed dodaniem
                foreach (var ingredient in recipe.Ingredients)
                {
                    var existingEntry = _context.Entry(ingredient);
                    if (existingEntry.State != EntityState.Detached)
                    {
                        existingEntry.State = EntityState.Detached;
                    }
                    
                    // Reset ID dla nowych sk³adników przepisu
                    if (ingredient.RecipeId == null)
                    {
                        ingredient.Id = 0; // Nowy sk³adnik przepisu
                    }
                }

                _context.Recipes.Add(recipe);
                await _context.SaveChangesAsync();
                
                System.Diagnostics.Debug.WriteLine($"? Added recipe: {recipe.Name} with {recipe.Ingredients.Count} ingredients");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error adding recipe: {ex.Message}");
                
                // Clear tracking na b³¹d
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
                // Pobierz istniej¹cy przepis z sk³adnikami
                var existingRecipe = await _context.Recipes
                    .Include(r => r.Ingredients)
                    .FirstOrDefaultAsync(r => r.Id == recipe.Id);

                if (existingRecipe == null)
                {
                    throw new InvalidOperationException($"Recipe with ID {recipe.Id} not found");
                }

                // Aktualizuj podstawowe w³aœciwoœci przepisu
                existingRecipe.Name = recipe.Name;
                existingRecipe.Description = recipe.Description;
                existingRecipe.Calories = recipe.Calories;
                existingRecipe.Protein = recipe.Protein;
                existingRecipe.Fat = recipe.Fat;
                existingRecipe.Carbs = recipe.Carbs;
                existingRecipe.IloscPorcji = recipe.IloscPorcji;
                existingRecipe.FolderId = recipe.FolderId; // FIX: persist folder assignment

                // ? OPTYMALIZACJA: Zarz¹dzanie sk³adnikami bez tracking conflicts
                
                // Usuñ wszystkie istniej¹ce sk³adniki przepisu
                var existingIngredients = existingRecipe.Ingredients.ToList();
                _context.Ingredients.RemoveRange(existingIngredients);

                // Dodaj nowe sk³adniki
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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                System.Diagnostics.Debug.WriteLine($"? Updated recipe: {recipe.Name} with {recipe.Ingredients.Count} ingredients");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine($"? Error updating recipe: {ex.Message}");
                
                // Clear tracking na b³¹d
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
                    .FirstOrDefaultAsync(r => r.Id == id);
                    
                if (recipe != null)
                {
                    // EF Core automatycznie usunie sk³adniki dziêki cascade delete
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