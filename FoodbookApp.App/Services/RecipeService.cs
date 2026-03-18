using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;
using FoodbookApp.Interfaces;
using Foodbook.Services;
using Newtonsoft.Json;
using System.Data.Common;

namespace Foodbook.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly AppDbContext _context;
        private readonly ISupabaseSyncService? _syncService;

        public RecipeService(AppDbContext context, IServiceProvider serviceProvider)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            
            // Try to resolve sync service (may be null if not registered)
            try
            {
                _syncService = serviceProvider.GetService(typeof(ISupabaseSyncService)) as ISupabaseSyncService;
            }
            catch
            {
                _syncService = null;
            }
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
                // Diagnostic: log current Recipes table schema to help diagnose datatype mismatch errors
                try
                {
                    var conn = _context.Database.GetDbConnection();
                    try
                    {
                        if (conn.State != System.Data.ConnectionState.Open)
                            await conn.OpenAsync();

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "PRAGMA table_info('Recipes');";
                        using var reader = await cmd.ExecuteReaderAsync();
                        System.Diagnostics.Debug.WriteLine("? PRAGMA table_info('Recipes') ->");
                        while (await reader.ReadAsync())
                        {
                            try
                            {
                                var cid = reader.GetInt32(0);
                                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                                var type = reader.IsDBNull(2) ? "" : reader.GetString(2);
                                var notnull = reader.GetInt32(3);
                                var dflt = reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString();
                                var pk = reader.GetInt32(5);
                                System.Diagnostics.Debug.WriteLine($"? Column: cid={cid}, name={name}, type={type}, notnull={notnull}, dflt={dflt}, pk={pk}");
                            }
                            catch (Exception innerEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"? Error reading PRAGMA row: {innerEx.Message}");
                            }
                        }

                        // Also log Recipes indexes
                        cmd.CommandText = "PRAGMA index_list('Recipes');";
                        using var idxReader = await cmd.ExecuteReaderAsync();
                        System.Diagnostics.Debug.WriteLine("? PRAGMA index_list('Recipes') ->");
                        while (await idxReader.ReadAsync())
                        {
                            try
                            {
                                var name = idxReader.IsDBNull(1) ? "" : idxReader.GetString(1);
                                var unique = idxReader.IsDBNull(2) ? 0 : idxReader.GetInt32(2);
                                System.Diagnostics.Debug.WriteLine($"? Index: name={name}, unique={unique}");
                            }
                            catch { }
                        }
                    }
                    finally
                    {
                        try { await conn.CloseAsync(); } catch { }
                    }
                }
                catch (Exception exSchema)
                {
                    System.Diagnostics.Debug.WriteLine($"? Failed to read PRAGMA schema: {exSchema.Message}");
                }

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

                if (_syncService != null)
                {
                    try
                    {
                        await _syncService.QueueForSyncAsync(recipe, SyncOperationType.Insert);
                        System.Diagnostics.Debug.WriteLine($"[RecipeService] Queued recipe {recipe.Id} for Insert sync");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeService] Failed to queue insert sync: {syncEx.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"? Added recipe: {recipe.Name} (Id: {recipe.Id})");

                // Raise events after successful save
                AppEvents.RaiseRecipeSaved(recipe.Id);
                AppEvents.RaiseRecipesChanged();
            }
            catch (Exception ex)
            {
                // Try to gather as much diagnostic information as possible
                try
                {
                    var conn = string.Empty;
                    try { conn = _context.Database.GetDbConnection()?.ConnectionString ?? string.Empty; } catch { }

                    System.Diagnostics.Debug.WriteLine("? AddRecipeAsync error: " + ex.ToString());
                    if (!string.IsNullOrEmpty(conn))
                        System.Diagnostics.Debug.WriteLine($"? ConnectionString: {conn}");

                    try
                    {
                        var recipeJson = JsonConvert.SerializeObject(recipe, Formatting.None,
                            new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
                        System.Diagnostics.Debug.WriteLine($"? Recipe JSON: {recipeJson}");
                    }
                    catch (Exception sx)
                    {
                        System.Diagnostics.Debug.WriteLine($"? Failed to serialize recipe for diagnostics: {sx.Message}");
                    }

                    if (ex is DbUpdateException dbEx)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"? DbUpdateException detected: {dbEx.ToString()}");
                            foreach (var entry in dbEx.Entries)
                            {
                                System.Diagnostics.Debug.WriteLine($"? Entry EntityType: {entry.Entity?.GetType().FullName}, State: {entry.State}");
                            }
                        }
                        catch { }
                    }
                }
                catch { }

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

                // Queue for cloud sync (Update operation)
                if (_syncService != null)
                {
                    try
                    {
                        await _syncService.QueueForSyncAsync(recipe, SyncOperationType.Update);
                        System.Diagnostics.Debug.WriteLine($"[RecipeService] Queued recipe {recipe.Id} for Update sync");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RecipeService] Failed to queue sync: {syncEx.Message}");
                    }
                }

                // Raise events after successful save
                AppEvents.RaiseRecipeSaved(recipe.Id);
                AppEvents.RaiseRecipesChanged();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                System.Diagnostics.Debug.WriteLine("? UpdateRecipeAsync error: " + ex.ToString());

                try
                {
                    var conn = string.Empty;
                    try { conn = _context.Database.GetDbConnection()?.ConnectionString ?? string.Empty; } catch { }
                    if (!string.IsNullOrEmpty(conn))
                        System.Diagnostics.Debug.WriteLine($"? ConnectionString: {conn}");
                }
                catch { }

                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task DeleteRecipeAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                System.Diagnostics.Debug.WriteLine("? DeleteRecipeAsync called with Guid.Empty");
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
                    var ingredientIds = recipe.Ingredients.Select(i => i.Id).ToList();

                    _context.Recipes.Remove(recipe);
                    await _context.SaveChangesAsync();

                    System.Diagnostics.Debug.WriteLine($"? Deleted recipe: {recipe.Name}");

                    if (_syncService != null)
                    {
                        try
                        {
                            await _syncService.QueueForSyncAsync(new Recipe { Id = id, Name = recipe.Name }, SyncOperationType.Delete);
                            foreach (var ingredientId in ingredientIds)
                            {
                                await _syncService.QueueForSyncAsync(new Ingredient { Id = ingredientId }, SyncOperationType.Delete);
                            }
                            System.Diagnostics.Debug.WriteLine($"[RecipeService] Queued recipe {id} and {ingredientIds.Count} ingredients for Delete sync");
                        }
                        catch (Exception syncEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[RecipeService] Failed to queue sync: {syncEx.Message}");
                        }
                    }

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