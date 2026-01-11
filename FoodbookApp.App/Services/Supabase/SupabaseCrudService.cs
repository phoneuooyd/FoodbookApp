using System.Text.Json;
using System.Text.Json.Serialization;
using Foodbook.Models;
using Foodbook.Models.DTOs;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services.Supabase;

public sealed class SupabaseCrudService : ISupabaseCrudService
{
    private readonly global::Supabase.Client _client;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SupabaseCrudService(global::Supabase.Client client)
    {
        _client = client;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _client.InitializeAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    #region Folders

    public async Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<FolderDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Folder?> GetFolderAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<FolderDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (folder.Id == Guid.Empty)
            folder.Id = Guid.NewGuid();

        var dto = ToDto(folder);
        var resp = await _client.From<FolderDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateFolderAsync(Folder folder, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(folder);
        await _client.From<FolderDto>().Where(x => x.Id == folder.Id).Update(dto);
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        var resp = await _client.From<RecipeDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Recipe?> GetRecipeAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<RecipeDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Recipe> AddRecipeAsync(Recipe recipe, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (recipe.Id == Guid.Empty)
            recipe.Id = Guid.NewGuid();

        var dto = ToDto(recipe);
        var resp = await _client.From<RecipeDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateRecipeAsync(Recipe recipe, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(recipe);
        await _client.From<RecipeDto>().Where(x => x.Id == recipe.Id).Update(dto);
    }

    public async Task DeleteRecipeAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        var resp = await _client.From<IngredientDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Ingredient?> GetIngredientAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<IngredientDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Ingredient> AddIngredientAsync(Ingredient ingredient, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (ingredient.Id == Guid.Empty)
            ingredient.Id = Guid.NewGuid();

        var dto = ToDto(ingredient);
        var resp = await _client.From<IngredientDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateIngredientAsync(Ingredient ingredient, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(ingredient);
        await _client.From<IngredientDto>().Where(x => x.Id == ingredient.Id).Update(dto);
    }

    public async Task DeleteIngredientAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        var resp = await _client.From<PlanDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Plan?> GetPlanAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<PlanDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<Plan> AddPlanAsync(Plan plan, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (plan.Id == Guid.Empty)
            plan.Id = Guid.NewGuid();

        var dto = ToDto(plan);
        var resp = await _client.From<PlanDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdatePlanAsync(Plan plan, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(plan);
        await _client.From<PlanDto>().Where(x => x.Id == plan.Id).Update(dto);
    }

    public async Task DeletePlanAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        var resp = await _client.From<PlannedMealDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<List<PlannedMeal>> GetPlannedMealsByPlanAsync(Guid planId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<PlannedMealDto>().Where(x => x.PlanId == planId).Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<PlannedMeal?> GetPlannedMealAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<PlannedMealDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<PlannedMeal> AddPlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (plannedMeal.Id == Guid.Empty)
            plannedMeal.Id = Guid.NewGuid();

        var dto = ToDto(plannedMeal);
        var resp = await _client.From<PlannedMealDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdatePlannedMealAsync(PlannedMeal plannedMeal, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(plannedMeal);
        await _client.From<PlannedMealDto>().Where(x => x.Id == plannedMeal.Id).Update(dto);
    }

    public async Task DeletePlannedMealAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        var resp = await _client.From<ShoppingListItemDto>().Where(x => x.PlanId == planId).Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<ShoppingListItem?> GetShoppingListItemAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<ShoppingListItemDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<ShoppingListItem> AddShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (item.Id == Guid.Empty)
            item.Id = Guid.NewGuid();

        var dto = ToDto(item);
        var resp = await _client.From<ShoppingListItemDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateShoppingListItemAsync(ShoppingListItem item, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(item);
        await _client.From<ShoppingListItemDto>().Where(x => x.Id == item.Id).Update(dto);
    }

    public async Task DeleteShoppingListItemAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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
        await EnsureInitializedAsync();
        var resp = await _client.From<RecipeLabelDto>().Get();
        return resp.Models
            .Where(r => r.IsDeleted != true)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<RecipeLabel?> GetRecipeLabelAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var resp = await _client.From<RecipeLabelDto>().Where(x => x.Id == id).Get();
        var row = resp.Models.FirstOrDefault(r => r.IsDeleted != true);
        return row == null ? null : ToDomain(row);
    }

    public async Task<RecipeLabel> AddRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

        if (label.Id == Guid.Empty)
            label.Id = Guid.NewGuid();

        var dto = ToDto(label);
        var resp = await _client.From<RecipeLabelDto>().Insert(new[] { dto });
        var created = resp.Models.FirstOrDefault() ?? dto;
        return ToDomain(created);
    }

    public async Task UpdateRecipeLabelAsync(RecipeLabel label, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var dto = ToDto(label);
        await _client.From<RecipeLabelDto>().Where(x => x.Id == label.Id).Update(dto);
    }

    public async Task DeleteRecipeLabelAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();

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

    #region Mapping

    private static Folder ToDomain(FolderDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Name = (r.Name ?? string.Empty).Trim(),
        Description = r.Description,
        ParentFolderId = r.ParentFolderId,
        Order = r.Order,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow
    };

    private static FolderDto ToDto(Folder f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Description = f.Description,
        ParentFolderId = f.ParentFolderId,
        Order = f.Order
    };

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
        FolderId = r.FolderId
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
        FolderId = r.FolderId
    };

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
        RecipeId = r.RecipeId
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
        RecipeId = i.RecipeId
    };

    private static Plan ToDomain(PlanDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        StartDate = r.StartDate?.Date ?? DateTime.MinValue,
        EndDate = r.EndDate?.Date ?? DateTime.MinValue,
        IsArchived = r.IsArchived ?? false,
        Type = (PlanType)(r.Type ?? 0),
        LinkedShoppingListPlanId = r.LinkedShoppingListPlanId,
        Title = r.PlannerName
    };

    private static PlanDto ToDto(Plan p) => new()
    {
        Id = p.Id,
        StartDate = p.StartDate.Date,
        EndDate = p.EndDate.Date,
        IsArchived = p.IsArchived,
        Type = (int)p.Type,
        PlannerName = p.Title,
        LinkedShoppingListPlanId = p.LinkedShoppingListPlanId
    };

    private static PlannedMeal ToDomain(PlannedMealDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        RecipeId = r.RecipeId ?? Guid.Empty,
        PlanId = r.PlanId,
        Date = r.Date ?? DateTime.MinValue,
        Portions = r.Portions ?? 1
    };

    private static PlannedMealDto ToDto(PlannedMeal pm) => new()
    {
        Id = pm.Id,
        RecipeId = pm.RecipeId,
        PlanId = pm.PlanId,
        Date = pm.Date,
        Portions = pm.Portions
    };

    private static ShoppingListItem ToDomain(ShoppingListItemDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        PlanId = r.PlanId ?? Guid.Empty,
        IngredientName = (r.Name ?? string.Empty).Trim(),
        Unit = (Unit)(r.Unit ?? 0),
        IsChecked = r.IsChecked ?? false,
        Quantity = r.Quantity ?? 0,
        Order = r.Order ?? 0
    };

    private static ShoppingListItemDto ToDto(ShoppingListItem s) => new()
    {
        Id = s.Id,
        PlanId = s.PlanId,
        Name = s.IngredientName,
        Unit = (int)s.Unit,
        IsChecked = s.IsChecked,
        Quantity = s.Quantity,
        Order = s.Order
    };

    private static RecipeLabel ToDomain(RecipeLabelDto r) => new()
    {
        Id = r.Id ?? Guid.Empty,
        Name = (r.Name ?? string.Empty).Trim(),
        ColorHex = r.ColorHex,
        CreatedAt = r.CreatedAt ?? DateTime.UtcNow
    };

    private static RecipeLabelDto ToDto(RecipeLabel l) => new()
    {
        Id = l.Id,
        Name = l.Name,
        ColorHex = l.ColorHex
    };

    #endregion
}
