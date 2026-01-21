using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Models;
using Foodbook.Models.DTOs;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;

namespace FoodbookApp.Services.Supabase;

public sealed class SupabaseCrudService : ISupabaseCrudService
{
    private readonly global::Supabase.Client _client;
    private readonly SupabaseRestClient _restClient;
    private readonly IPreferencesService _preferencesService;
    private readonly IAuthTokenStore _tokenStore;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private const string SupabaseUrl = "https://gscbdvezastxpyndkauh.supabase.co";
    private const string SupabaseAnonKey = "sb_publishable_gwkJSRidW1DP28CCEeQUDA_ELLTHT92";

    public SupabaseCrudService(
        global::Supabase.Client client,
        SupabaseRestClient restClient,
        IAuthTokenStore tokenStore,
        IPreferencesService preferencesService)
    {
        _client = client;
        _tokenStore = tokenStore;
        _restClient = restClient ?? throw new ArgumentNullException(nameof(restClient));
        _preferencesService = preferencesService;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            try
            {
                // Initialize Supabase client (may attempt Realtime). Swallow Realtime errors and continue for PostgREST-only paths.
                await _client.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] InitializeAsync failed (proceeding HTTP-only): {ex.Message}");
            }
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Ensures the Supabase client has the current user's access token set for RLS.
    /// Call this before any CRUD operation.
    /// </summary>
    private async Task EnsureAuthenticatedAsync()
    {
        await EnsureInitializedAsync();

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return;

        var accessToken = await _tokenStore.GetAccessTokenAsync(accountId.Value);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                // Set the access token on Supabase client so RLS sees auth.uid()
                await _client.Auth.SetSession(accessToken, await _tokenStore.GetRefreshTokenAsync(accountId.Value) ?? string.Empty);
                System.Diagnostics.Debug.WriteLine("[SupabaseCrudService] Set auth session for CRUD operations");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] SetSession failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the current Supabase user ID (auth.uid()) for setting owner_id on entities.
    /// </summary>
    private async Task<string?> GetCurrentUserIdAsync()
    {
        var user = _client.Auth.CurrentUser;
        if (user != null) return user.Id;

        // Fallback: try to get from token store
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return null;

        var accessToken = await _tokenStore.GetAccessTokenAsync(accountId.Value);
        if (string.IsNullOrWhiteSpace(accessToken)) return null;

        // Parse user ID from JWT (sub claim)
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1];
                // Pad base64 if needed
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("sub", out var sub))
                    return sub.GetString();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] Failed to parse JWT: {ex.Message}");
        }

        return null;
    }

    #region Folders

    public async Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<FolderDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Folder?> GetFolderAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<FolderDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (folder.Id == Guid.Empty)
            folder.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(folder);
        var resp = await _client.From<FolderDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateFolderAsync(Folder folder, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(folder);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<FolderDto>().Where(x => x.Id == folder.Id).Update(dto);
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new FolderDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<FolderDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region Recipes

    public async Task<List<Recipe>> GetRecipesAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<RecipeDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<RecipeDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Recipe> AddRecipeAsync(Recipe recipe, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (recipe.Id == Guid.Empty)
            recipe.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(recipe);
        var resp = await _client.From<RecipeDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateRecipeAsync(Recipe recipe, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(recipe);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<RecipeDto>().Where(x => x.Id == recipe.Id).Update(dto);
    }

    public async Task DeleteRecipeAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new RecipeDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<RecipeDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region Ingredients

    public async Task<List<Ingredient>> GetIngredientsAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<IngredientDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Ingredient? > GetIngredientAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<IngredientDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Ingredient> AddIngredientAsync(Ingredient ingredient, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (ingredient.Id == Guid.Empty)
            ingredient.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(ingredient);
        System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] AddIngredient: id={dto.Id}, owner_id={dto.OwnerId}, name={dto.Name}");
        
        var resp = await _client.From<IngredientDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateIngredientAsync(Ingredient ingredient, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(ingredient);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<IngredientDto>().Where(x => x.Id == ingredient.Id).Update(dto);
    }

    public async Task DeleteIngredientAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new IngredientDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<IngredientDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region Plans

    public async Task<List<Plan>> GetPlansAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<PlanDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Plan? > GetPlanAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<PlanDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Plan> AddPlanAsync(Plan plan, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (plan.Id == Guid.Empty)
            plan.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(plan);
        var resp = await _client.From<PlanDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdatePlanAsync(Plan plan, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(plan);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<PlanDto>().Where(x => x.Id == plan.Id).Update(dto);
    }

    public async Task DeletePlanAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new PlanDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<PlanDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region PlannedMeals

    public async Task<List<PlannedMeal>> GetPlannedMealsAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<PlannedMealDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<List<PlannedMeal>> GetPlannedMealsByPlanAsync(Guid planId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<PlannedMealDto>().Where(x => x.PlanId == planId).Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<PlannedMeal? > GetPlannedMealAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<PlannedMealDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<PlannedMeal> AddPlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (plannedMeal.Id == Guid.Empty)
            plannedMeal.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(plannedMeal);
        var resp = await _client.From<PlannedMealDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdatePlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(plannedMeal);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<PlannedMealDto>().Where(x => x.Id == plannedMeal.Id).Update(dto);
    }

    public async Task DeletePlannedMealAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new PlannedMealDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<PlannedMealDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region ShoppingListItems

    public async Task<List<ShoppingListItem>> GetShoppingListItemsAsync(Guid planId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<ShoppingListItemDto>().Where(x => x.PlanId == planId).Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<ShoppingListItem? > GetShoppingListItemAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<ShoppingListItemDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<ShoppingListItem> AddShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (item.Id == Guid.Empty)
            item.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(item);
        var resp = await _client.From<ShoppingListItemDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(item);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<ShoppingListItemDto>().Where(x => x.Id == item.Id).Update(dto);
    }

    public async Task DeleteShoppingListItemAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new ShoppingListItemDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<ShoppingListItemDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region RecipeLabels

    public async Task<List<RecipeLabel>> GetRecipeLabelsAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<RecipeLabelDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<RecipeLabel? > GetRecipeLabelAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _client.From<RecipeLabelDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<RecipeLabel> AddRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        if (label.Id == Guid.Empty)
            label.Id = Guid.NewGuid();

        var dto = await ToDtoWithOwnerAsync(label);
        var resp = await _client.From<RecipeLabelDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        var dto = await ToDtoWithOwnerAsync(label);
        dto.UpdatedAt = DateTime.UtcNow;
        await _client.From<RecipeLabelDto>().Where(x => x.Id == label.Id).Update(dto);
    }

    public async Task DeleteRecipeLabelAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();

        var patch = new RecipeLabelDto
        {
            Id = id,
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _client.From<RecipeLabelDto>().Where(x => x.Id == id).Update(patch);
    }

    #endregion

    #region Batch Operations (REST API)

    public async Task<List<Recipe>> AddRecipesBatchAsync(IEnumerable<Recipe> recipes, CancellationToken ct = default)
    {
        var ownerId = await GetCurrentUserIdAsync();
        var now = DateTime.UtcNow;
        var dtos = recipes.Select(r =>
        {
            if (r.Id == Guid.Empty) r.Id = Guid.NewGuid();
            var dto = ToDto(r);
            dto.OwnerId = ownerId;
            dto.CreatedAt ??= now;
            dto.UpdatedAt ??= dto.CreatedAt;
            return dto;
        }).ToList();

        var results = await _restClient.InsertBatchAsync("recipes", dtos, ct);
        return results.Select(ToDomain).ToList();
    }

    public async Task<List<Ingredient>> AddIngredientsBatchAsync(IEnumerable<Ingredient> ingredients, CancellationToken ct = default)
    {
        var ownerId = await GetCurrentUserIdAsync();
        var now = DateTime.UtcNow;
        var dtos = ingredients.Select(i =>
        {
            if (i.Id == Guid.Empty) i.Id = Guid.NewGuid();
            var dto = ToDto(i);
            dto.OwnerId = ownerId;
            dto.CreatedAt ??= now;
            dto.UpdatedAt ??= dto.CreatedAt;
            return dto;
        }).ToList();

        var results = await _restClient.InsertBatchAsync("ingredients", dtos, ct);
        return results.Select(ToDomain).ToList();
    }

    public async Task<List<PlannedMeal>> AddPlannedMealsBatchAsync(IEnumerable<PlannedMeal> meals, CancellationToken ct = default)
    {
        var ownerId = await GetCurrentUserIdAsync();
        var now = DateTime.UtcNow;
        var dtos = meals.Select(pm =>
        {
            if (pm.Id == Guid.Empty) pm.Id = Guid.NewGuid();
            var dto = ToDto(pm);
            dto.OwnerId = ownerId;
            dto.CreatedAt ??= now;
            dto.UpdatedAt ??= dto.CreatedAt;
            return dto;
        }).ToList();

        var results = await _restClient.InsertBatchAsync("planned_meals", dtos, ct);
        return results.Select(ToDomain).ToList();
    }

    public async Task<List<ShoppingListItem>> AddShoppingListItemsBatchAsync(IEnumerable<ShoppingListItem> items, CancellationToken ct = default)
    {
        var ownerId = await GetCurrentUserIdAsync();
        var now = DateTime.UtcNow;
        var dtos = items.Select(s =>
        {
            if (s.Id == Guid.Empty) s.Id = Guid.NewGuid();
            var dto = ToDto(s);
            dto.OwnerId = ownerId;
            dto.CreatedAt ??= now;
            dto.UpdatedAt ??= dto.CreatedAt;
            return dto;
        }).ToList();

        var results = await _restClient.InsertBatchAsync("shopping_list_items", dtos, ct);
        return results.Select(ToDomain).ToList();
    }

    public async Task<List<Recipe>> UpsertRecipesAsync(IEnumerable<Recipe> recipes, CancellationToken ct = default)
    {
        var ownerId = await GetCurrentUserIdAsync();
        var now = DateTime.UtcNow;
        var dtos = recipes.Select(r =>
        {
            if (r.Id == Guid.Empty) r.Id = Guid.NewGuid();
            var dto = ToDto(r);
            dto.OwnerId = ownerId;
            // Upsert is used for inserts and updates; ensure timestamps are present.
            dto.CreatedAt ??= now;
            dto.UpdatedAt ??= now;
            return dto;
        }).ToList();

        var results = await _restClient.UpsertAsync("recipes", dtos, ct);
        return results.Select(ToDomain).ToList();
    }

    public async Task<List<Ingredient>> UpsertIngredientsAsync(IEnumerable<Ingredient> ingredients, CancellationToken ct = default)
    {
        var ownerId = await GetCurrentUserIdAsync();
        var now = DateTime.UtcNow;
        var dtos = ingredients.Select(i =>
        {
            if (i.Id == Guid.Empty) i.Id = Guid.NewGuid();
            var dto = ToDto(i);
            dto.OwnerId = ownerId;
            dto.CreatedAt ??= now;
            dto.UpdatedAt ??= now;
            return dto;
        }).ToList();

        var results = await _restClient.UpsertAsync("ingredients", dtos, ct);
        return results.Select(ToDomain).ToList();
    }

    #endregion

    #region UserPreferences (ORM)

    public async Task<UserPreferencesDto?> GetUserPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        try
        {
            var results = await _restClient.GetAsync<UserPreferencesDto>("user_preferences", $"id=eq.{userId}", ct);
            return results.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] GetUserPreferencesAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<UserPreferencesDto> UpsertUserPreferencesAsync(UserPreferencesDto preferences, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        try
        {
            // Prepare plain payload to avoid BaseModel internals being serialized
            var payload = new UserPreferencesPayload
            {
                id = preferences.Id ?? Guid.Empty,
                theme = preferences.Theme,
                color_theme = preferences.ColorTheme,
                is_colorful_background = preferences.IsColorfulBackground,
                is_wallpaper_enabled = preferences.IsWallpaperEnabled,
                font_family = preferences.FontFamily,
                font_size = preferences.FontSize,
                language = preferences.Language,
                created_at = preferences.CreatedAt ?? DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            // Use REST upsert with plain payload to avoid sending BaseModel metadata
            var results = await _restClient.UpsertAsync<UserPreferencesPayload>("user_preferences", new[] { payload }, ct);
            var created = results.FirstOrDefault();
            if (created == null)
            {
                preferences.UpdatedAt = DateTime.UtcNow;
                return preferences;
            }

            // Map back to UserPreferencesDto
            var dto = new UserPreferencesDto
            {
                Id = created.id,
                Theme = created.theme ?? "System",
                ColorTheme = created.color_theme ?? "Default",
                IsColorfulBackground = created.is_colorful_background ?? false,
                IsWallpaperEnabled = created.is_wallpaper_enabled ?? false,
                FontFamily = created.font_family ?? "Default",
                FontSize = created.font_size ?? "Default",
                Language = created.language ?? "en",
                CreatedAt = created.created_at,
                UpdatedAt = created.updated_at
            };

            return dto;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] UpsertUserPreferencesAsync failed: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Mapping

    private static Folder ToDomain(FolderDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Name = (r.Name ?? string.Empty).Trim(),
        Description = r.Description,
        ParentFolderId = r.ParentFolderId,
        Order = r.Order,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static FolderDto ToDto(Folder f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Description = f.Description,
        ParentFolderId = f.ParentFolderId,
        Order = f.Order,
        CreatedAt = f.CreatedAt,
        UpdatedAt = f.UpdatedAt
    };

    private async Task<FolderDto> ToDtoWithOwnerAsync(Folder f)
    {
        var dto = ToDto(f);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    private static Recipe ToDomain(RecipeDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Name = r.Name,
        Description = r.Description,
        Calories = r.Calories ?? 0,
        Protein = r.Protein ?? 0,
        Fat = r.Fat ?? 0,
        Carbs = r.Carbs ?? 0,
        IloscPorcji = r.Portions ?? 2,
        FolderId = r.FolderId,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static RecipeDto ToDto(Recipe r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Description = r.Description,
        Calories = r.Calories,
        Protein = r.Protein,
        Fat = r.Fat,
        Carbs = r.Carbs,
        Portions = r.IloscPorcji,
        FolderId = r.FolderId,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt
    };

    private async Task<RecipeDto> ToDtoWithOwnerAsync(Recipe r)
    {
        var dto = ToDto(r);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    private static Ingredient ToDomain(IngredientDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Name = (r.Name ?? string.Empty).Trim(),
        Quantity = r.Quantity ?? 0,
        Unit = (Unit)(r.Unit ?? 0),
        UnitWeight = r.UnitWeight ?? 1,
        Calories = r.Calories ?? 0,
        Protein = r.Protein ?? 0,
        Fat = r.Fat ?? 0,
        Carbs = r.Carbs ?? 0,
        RecipeId = r.RecipeId,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static IngredientDto ToDto(Ingredient i) => new()
    {
        Id = i.Id,
        Name = i.Name,
        Quantity = i.Quantity,
        Unit = (int)i.Unit,
        UnitWeight = i.UnitWeight,
        Calories = i.Calories,
        Protein = i.Protein,
        Fat = i.Fat,
        Carbs = i.Carbs,
        RecipeId = i.RecipeId,
        CreatedAt = i.CreatedAt,
        UpdatedAt = i.UpdatedAt
    };

    private async Task<IngredientDto> ToDtoWithOwnerAsync(Ingredient i)
    {
        var dto = ToDto(i);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    private static Plan ToDomain(PlanDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        StartDate = r.StartDate?.Date ?? DateTime.MinValue,
        EndDate = r.EndDate?.Date ?? DateTime.MinValue,
        IsArchived = r.IsArchived ?? false,
        Type = (PlanType)(r.Type ?? 0),
        LinkedShoppingListPlanId = r.LinkedShoppingListPlanId,
        Title = r.PlannerName,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static PlanDto ToDto(Plan p) => new()
    {
        Id = p.Id,
        StartDate = p.StartDate.Date,
        EndDate = p.EndDate.Date,
        IsArchived = p.IsArchived,
        Type = (int)p.Type,
        PlannerName = p.Title,
        LinkedShoppingListPlanId = p.LinkedShoppingListPlanId,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };

    private async Task<PlanDto> ToDtoWithOwnerAsync(Plan p)
    {
        var dto = ToDto(p);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    private static PlannedMeal ToDomain(PlannedMealDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        RecipeId = r.RecipeId ?? Guid.Empty,
        PlanId = r.PlanId,
        Date = r.Date ?? DateTime.MinValue,
        Portions = r.Portions ?? 1,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static PlannedMealDto ToDto(PlannedMeal pm) => new()
    {
        Id = pm.Id,
        RecipeId = pm.RecipeId,
        PlanId = pm.PlanId,
        Date = pm.Date,
        Portions = pm.Portions,
        CreatedAt = pm.CreatedAt,
        UpdatedAt = pm.UpdatedAt
    };

    private async Task<PlannedMealDto> ToDtoWithOwnerAsync(PlannedMeal pm)
    {
        var dto = ToDto(pm);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    private static ShoppingListItem ToDomain(ShoppingListItemDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        PlanId = r.PlanId ?? Guid.Empty,
        IngredientName = (r.Name ?? string.Empty).Trim(),
        Unit = (Unit)(r.Unit ?? 0),
        IsChecked = r.IsChecked ?? false,
        Quantity = r.Quantity ?? 0,
        Order = r.Order ?? 0,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static ShoppingListItemDto ToDto(ShoppingListItem s) => new()
    {
        Id = s.Id,
        PlanId = s.PlanId,
        Name = s.IngredientName,
        Unit = (int)s.Unit,
        IsChecked = s.IsChecked,
        Quantity = s.Quantity,
        Order = s.Order,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt
    };

    private async Task<ShoppingListItemDto> ToDtoWithOwnerAsync(ShoppingListItem s)
    {
        var dto = ToDto(s);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    private static RecipeLabel ToDomain(RecipeLabelDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Name = (r.Name ?? string.Empty).Trim(),
        ColorHex = r.ColorHex,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = r.UpdatedAt
    };

    private static RecipeLabelDto ToDto(RecipeLabel l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        ColorHex = l.ColorHex,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt
    };

    private async Task<RecipeLabelDto> ToDtoWithOwnerAsync(RecipeLabel l)
    {
        var dto = ToDto(l);
        dto.OwnerId = await GetCurrentUserIdAsync();
        dto.CreatedAt ??= DateTime.UtcNow;
        dto.UpdatedAt ??= dto.CreatedAt;
        return dto;
    }

    #endregion

    #region Bulk Operations for Sync
    
    public async Task<CloudDataSnapshot> FetchAllCloudDataAsync(CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync();
        
        try
        {
            // Fetch all data in parallel for efficiency
            var foldersTask = GetFoldersAsync(ct);
            var recipesTask = GetRecipesAsync(ct);
            var ingredientsTask = GetIngredientsAsync(ct);
            var labelsTask = GetRecipeLabelsAsync(ct);
            var plansTask = GetPlansAsync(ct);
            var plannedMealsTask = GetPlannedMealsAsync(ct);
            
            await Task.WhenAll(foldersTask, recipesTask, ingredientsTask, labelsTask, plansTask, plannedMealsTask);
            
            // Shopping list items require plan IDs, so fetch after plans
            var plans = await plansTask;
            var shoppingItems = new List<ShoppingListItem>();
            foreach (var plan in plans)
            {
                var items = await GetShoppingListItemsAsync(plan.Id, ct);
                shoppingItems.AddRange(items);
            }
            
            return new CloudDataSnapshot(
                await foldersTask,
                await recipesTask,
                await ingredientsTask,
                await labelsTask,
                plans,
                await plannedMealsTask,
                shoppingItems
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseCrudService] FetchAllCloudDataAsync failed: {ex.Message}");
            // Return empty snapshot on error to allow sync to continue
            return new CloudDataSnapshot(
                new List<Folder>(),
                new List<Recipe>(),
                new List<Ingredient>(),
                new List<RecipeLabel>(),
                new List<Plan>(),
                new List<PlannedMeal>(),
                new List<ShoppingListItem>()
            );
        }
    }
    
    #endregion
}

// Plain payload type for REST upsert to avoid BaseModel inheritance
public class UserPreferencesPayload
{
    public Guid id { get; set; }
    public string? theme { get; set; }
    public string? color_theme { get; set; }
    public bool? is_colorful_background { get; set; }
    public bool? is_wallpaper_enabled { get; set; }
    public string? font_family { get; set; }
    public string? font_size { get; set; }
    public string? language { get; set; }
    public DateTime? created_at { get; set; }
    public DateTime? updated_at { get; set; }
}
