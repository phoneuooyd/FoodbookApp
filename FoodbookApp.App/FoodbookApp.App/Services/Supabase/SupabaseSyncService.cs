using System.Diagnostics;
using System.Text.Json;
using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Services.Supabase;

/// <summary>
/// Service for synchronizing local data with Supabase cloud.
/// Uses a queue-based approach with interval-based processing to optimize bandwidth.
/// </summary>
public sealed class SupabaseSyncService : ISupabaseSyncService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthTokenStore _tokenStore;
    private readonly ISupabaseCrudService _crudService;
    
    private Timer? _syncTimer;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private bool _disposed;

    private const int MaxRetryCount = 3;
    private const int BatchSize = 50;
    private static readonly TimeSpan DefaultSyncInterval = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;
    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    public SupabaseSyncService(
        IServiceProvider serviceProvider,
        IAuthTokenStore tokenStore,
        ISupabaseCrudService crudService)
    {
        _serviceProvider = serviceProvider;
        _tokenStore = tokenStore;
        _crudService = crudService;
    }

    #region Public API

    public async Task<SyncState?> GetSyncStateAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return null;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        return await db.SyncStates
            .FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
    }

    public async Task<bool> IsCloudSyncEnabledAsync(CancellationToken ct = default)
    {
        var state = await GetSyncStateAsync(ct);
        return state?.IsCloudSyncEnabled ?? false;
    }

    public async Task EnableCloudSyncAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
        {
            Debug.WriteLine("[SyncService] Cannot enable sync - no active account");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates
            .FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);

        if (state == null)
        {
            state = new SyncState
            {
                AccountId = accountId.Value,
                IsCloudSyncEnabled = true,
                Status = SyncStatus.Idle,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            db.SyncStates.Add(state);
        }
        else
        {
            state.IsCloudSyncEnabled = true;
            state.Status = SyncStatus.Idle;
            state.UpdatedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        Debug.WriteLine($"[SyncService] Cloud sync enabled for account {accountId.Value}");

        // Start initial sync if not completed
        if (!state.InitialSyncCompleted)
        {
            _ = StartInitialSyncAsync(ct);
        }

        StartSyncTimer();
    }

    public async Task DisableCloudSyncAsync(CancellationToken ct = default)
    {
        StopSyncTimer();

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates
            .FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);

        if (state != null)
        {
            state.IsCloudSyncEnabled = false;
            state.Status = SyncStatus.Disabled;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        Debug.WriteLine($"[SyncService] Cloud sync disabled for account {accountId.Value}");
        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Disabled, "Sync disabled by user");
    }

    public async Task QueueForSyncAsync<T>(T entity, SyncOperationType operation, CancellationToken ct = default) 
        where T : class
    {
        if (!await IsCloudSyncEnabledAsync(ct))
            return;

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        await _queueLock.WaitAsync(ct);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entityId = GetEntityId(entity);
            var entityType = GetEntityTypeName<T>();

            // Check for existing pending entry for same entity - consolidate updates
            var existingEntry = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value 
                         && e.EntityType == entityType 
                         && e.EntityId == entityId
                         && e.Status == SyncEntryStatus.Pending)
                .FirstOrDefaultAsync(ct);

            if (existingEntry != null)
            {
                // Consolidate: if new is Delete, change to Delete; otherwise update payload
                if (operation == SyncOperationType.Delete)
                {
                    existingEntry.OperationType = SyncOperationType.Delete;
                    existingEntry.Payload = null;
                }
                else
                {
                    existingEntry.Payload = SerializeEntity(entity);
                }
                existingEntry.CreatedUtc = DateTime.UtcNow;
                Debug.WriteLine($"[SyncService] Consolidated {entityType} {entityId} in queue");
            }
            else
            {
                var entry = new SyncQueueEntry
                {
                    AccountId = accountId.Value,
                    EntityType = entityType,
                    EntityId = entityId,
                    OperationType = operation,
                    Payload = operation != SyncOperationType.Delete ? SerializeEntity(entity) : null,
                    Priority = 10
                };
                db.SyncQueue.Add(entry);
                Debug.WriteLine($"[SyncService] Queued {operation} for {entityType} {entityId}");
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task QueueBatchForSyncAsync<T>(IEnumerable<T> entities, SyncOperationType operation, CancellationToken ct = default) 
        where T : class
    {
        if (!await IsCloudSyncEnabledAsync(ct))
            return;

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        var entityList = entities.ToList();
        if (entityList.Count == 0)
            return;

        await _queueLock.WaitAsync(ct);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var batchId = Guid.NewGuid();
            var entityType = GetEntityTypeName<T>();

            foreach (var entity in entityList)
            {
                var entry = new SyncQueueEntry
                {
                    AccountId = accountId.Value,
                    EntityType = entityType,
                    EntityId = GetEntityId(entity),
                    OperationType = operation,
                    Payload = operation != SyncOperationType.Delete ? SerializeEntity(entity) : null,
                    Priority = 10,
                    BatchId = batchId
                };
                db.SyncQueue.Add(entry);
            }

            await db.SaveChangesAsync(ct);
            Debug.WriteLine($"[SyncService] Queued batch of {entityList.Count} {entityType} items");
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task<bool> StartInitialSyncAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return false;

        Debug.WriteLine("[SyncService] Starting initial synchronization...");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates
            .FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);

        if (state == null || state.InitialSyncCompleted)
        {
            Debug.WriteLine("[SyncService] Initial sync already completed or state not found");
            return true;
        }

        state.Status = SyncStatus.InitialSync;
        state.InitialSyncStartedUtc = DateTime.UtcNow;
        state.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.InitialSync, "Starting initial sync");

        try
        {
            var batchId = Guid.NewGuid();

            // Queue all entities for initial sync
            await QueueAllEntitiesForInitialSyncAsync(db, accountId.Value, batchId, ct);

            state.InitialSyncCompleted = true;
            state.InitialSyncCompletedUtc = DateTime.UtcNow;
            state.Status = SyncStatus.Idle;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            Debug.WriteLine("[SyncService] Initial sync queued successfully");
            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Idle, "Initial sync completed");

            // Process queue immediately after initial sync
            _ = ProcessQueueAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SyncService] Initial sync failed: {ex.Message}");
            state.Status = SyncStatus.Error;
            state.LastSyncError = ex.Message;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Error, ex.Message);
            return false;
        }
    }

    public async Task<SyncResult> ProcessQueueAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            Debug.WriteLine("[SyncService] Sync already in progress, skipping");
            return SyncResult.Ok(0, await GetPendingCountAsync(ct), TimeSpan.Zero);
        }

        var sw = Stopwatch.StartNew();
        var processed = 0;
        var failed = 0;

        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
                return SyncResult.Error("No active account");

            if (!await IsCloudSyncEnabledAsync(ct))
                return SyncResult.Error("Sync not enabled");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state = await db.SyncStates
                .FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);

            if (state == null)
                return SyncResult.Error("Sync state not found");

            state.Status = SyncStatus.Syncing;
            state.LastSyncAttemptUtc = DateTime.UtcNow;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Syncing, "Processing queue");

            // Get pending entries ordered by priority and creation time
            var pendingEntries = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending)
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.CreatedUtc)
                .Take(BatchSize)
                .ToListAsync(ct);

            var totalPending = await db.SyncQueue
                .CountAsync(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending, ct);

            if (pendingEntries.Count == 0)
            {
                state.Status = SyncStatus.Idle;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                
                sw.Stop();
                RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, "Queue empty");
                return SyncResult.Ok(0, 0, sw.Elapsed);
            }

            Debug.WriteLine($"[SyncService] Processing {pendingEntries.Count} entries...");

            foreach (var entry in pendingEntries)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    entry.Status = SyncEntryStatus.InProgress;
                    entry.LastAttemptUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    await ProcessSingleEntryAsync(entry, ct);

                    entry.Status = SyncEntryStatus.Completed;
                    entry.SyncedUtc = DateTime.UtcNow;
                    processed++;

                    RaiseSyncProgress(totalPending, processed, failed, entry.EntityType);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SyncService] Failed to sync {entry.EntityType} {entry.EntityId}: {ex.Message}");
                    
                    entry.RetryCount++;
                    entry.LastError = ex.Message;
                    
                    if (entry.RetryCount >= MaxRetryCount)
                    {
                        entry.Status = SyncEntryStatus.Abandoned;
                        Debug.WriteLine($"[SyncService] Entry {entry.Id} abandoned after {MaxRetryCount} retries");
                    }
                    else
                    {
                        entry.Status = SyncEntryStatus.Failed;
                    }
                    
                    failed++;
                }

                await db.SaveChangesAsync(ct);
            }

            // Update sync state
            state.LastSyncUtc = DateTime.UtcNow;
            state.TotalItemsSynced += processed;
            state.PendingItemsCount = totalPending - processed;
            state.Status = SyncStatus.Idle;
            state.LastSyncError = failed > 0 ? $"{failed} items failed" : null;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            Debug.WriteLine($"[SyncService] Processed {processed} items, {failed} failed, {state.PendingItemsCount} remaining in {sw.Elapsed.TotalSeconds:F2}s");
            
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, 
                $"Synced {processed} items" + (failed > 0 ? $", {failed} failed" : ""));

            return SyncResult.Ok(processed, state.PendingItemsCount, sw.Elapsed);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SyncService] Queue processing error: {ex.Message}");
            return SyncResult.Error(ex.Message, failed);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SyncResult> ForceSyncAsync(CancellationToken ct = default)
    {
        Debug.WriteLine("[SyncService] Force sync requested");
        return await ProcessQueueAsync(ct);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return 0;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.SyncQueue
            .CountAsync(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending, ct);
    }

    public async Task ClearFailedEntriesAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failedEntries = await db.SyncQueue
            .Where(e => e.AccountId == accountId.Value 
                     && (e.Status == SyncEntryStatus.Failed || e.Status == SyncEntryStatus.Abandoned))
            .ToListAsync(ct);

        db.SyncQueue.RemoveRange(failedEntries);
        await db.SaveChangesAsync(ct);

        Debug.WriteLine($"[SyncService] Cleared {failedEntries.Count} failed entries");
    }

    public async Task RetryFailedEntriesAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failedEntries = await db.SyncQueue
            .Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Failed)
            .ToListAsync(ct);

        foreach (var entry in failedEntries)
        {
            entry.Status = SyncEntryStatus.Pending;
            entry.RetryCount = 0;
            entry.LastError = null;
        }

        await db.SaveChangesAsync(ct);
        Debug.WriteLine($"[SyncService] Reset {failedEntries.Count} entries for retry");
    }

    public void StartSyncTimer()
    {
        StopSyncTimer();
        _syncTimer = new Timer(
            async _ => await ProcessQueueAsync(),
            null,
            DefaultSyncInterval,
            DefaultSyncInterval);
        Debug.WriteLine($"[SyncService] Sync timer started with interval {DefaultSyncInterval.TotalMinutes} minutes");
    }

    public void StopSyncTimer()
    {
        _syncTimer?.Dispose();
        _syncTimer = null;
        Debug.WriteLine("[SyncService] Sync timer stopped");
    }

    #endregion

    #region Private Methods

    private async Task QueueAllEntitiesForInitialSyncAsync(
        AppDbContext db, 
        Guid accountId, 
        Guid batchId, 
        CancellationToken ct)
    {
        // Queue Folders first (needed for recipe references)
        var folders = await db.Folders.ToListAsync(ct);
        foreach (var folder in folders)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, folder, batchId, priority: 1));
        }
        Debug.WriteLine($"[SyncService] Queued {folders.Count} folders for initial sync");

        // Queue RecipeLabels
        var labels = await db.RecipeLabels.ToListAsync(ct);
        foreach (var label in labels)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, label, batchId, priority: 2));
        }
        Debug.WriteLine($"[SyncService] Queued {labels.Count} recipe labels for initial sync");

        // Queue Recipes
        var recipes = await db.Recipes.ToListAsync(ct);
        foreach (var recipe in recipes)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, recipe, batchId, priority: 3));
        }
        Debug.WriteLine($"[SyncService] Queued {recipes.Count} recipes for initial sync");

        // Queue Ingredients
        var ingredients = await db.Ingredients.ToListAsync(ct);
        foreach (var ingredient in ingredients)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, ingredient, batchId, priority: 4));
        }
        Debug.WriteLine($"[SyncService] Queued {ingredients.Count} ingredients for initial sync");

        // Queue Plans
        var plans = await db.Plans.ToListAsync(ct);
        foreach (var plan in plans)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, plan, batchId, priority: 5));
        }
        Debug.WriteLine($"[SyncService] Queued {plans.Count} plans for initial sync");

        // Queue PlannedMeals
        var plannedMeals = await db.PlannedMeals.ToListAsync(ct);
        foreach (var meal in plannedMeals)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, meal, batchId, priority: 6));
        }
        Debug.WriteLine($"[SyncService] Queued {plannedMeals.Count} planned meals for initial sync");

        // Queue ShoppingListItems
        var shoppingItems = await db.ShoppingListItems.ToListAsync(ct);
        foreach (var item in shoppingItems)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, item, batchId, priority: 7));
        }
        Debug.WriteLine($"[SyncService] Queued {shoppingItems.Count} shopping list items for initial sync");

        await db.SaveChangesAsync(ct);
    }

    private SyncQueueEntry CreateInitialSyncEntry<T>(Guid accountId, T entity, Guid batchId, int priority) where T : class
    {
        return new SyncQueueEntry
        {
            AccountId = accountId,
            EntityType = GetEntityTypeName<T>(),
            EntityId = GetEntityId(entity),
            OperationType = SyncOperationType.Insert,
            Payload = SerializeEntity(entity),
            Priority = priority,
            BatchId = batchId
        };
    }

    private async Task ProcessSingleEntryAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        Debug.WriteLine($"[SyncService] Processing {entry.OperationType} for {entry.EntityType} {entry.EntityId}");

        switch (entry.EntityType)
        {
            case "Folder":
                await ProcessFolderAsync(entry, ct);
                break;
            case "Recipe":
                await ProcessRecipeAsync(entry, ct);
                break;
            case "Ingredient":
                await ProcessIngredientAsync(entry, ct);
                break;
            case "RecipeLabel":
                await ProcessRecipeLabelAsync(entry, ct);
                break;
            case "Plan":
                await ProcessPlanAsync(entry, ct);
                break;
            case "PlannedMeal":
                await ProcessPlannedMealAsync(entry, ct);
                break;
            case "ShoppingListItem":
                await ProcessShoppingListItemAsync(entry, ct);
                break;
            default:
                throw new NotSupportedException($"Entity type {entry.EntityType} not supported for sync");
        }
    }

    private async Task ProcessFolderAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var folder = DeserializeEntity<Folder>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddFolderAsync(folder, ct);
                else
                    await _crudService.UpdateFolderAsync(folder, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeleteFolderAsync(entry.EntityId, ct);
                break;
        }
    }

    private async Task ProcessRecipeAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var recipe = DeserializeEntity<Recipe>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddRecipeAsync(recipe, ct);
                else
                    await _crudService.UpdateRecipeAsync(recipe, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeleteRecipeAsync(entry.EntityId, ct);
                break;
        }
    }

    private async Task ProcessIngredientAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var ingredient = DeserializeEntity<Ingredient>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddIngredientAsync(ingredient, ct);
                else
                    await _crudService.UpdateIngredientAsync(ingredient, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeleteIngredientAsync(entry.EntityId, ct);
                break;
        }
    }

    private async Task ProcessRecipeLabelAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var label = DeserializeEntity<RecipeLabel>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddRecipeLabelAsync(label, ct);
                else
                    await _crudService.UpdateRecipeLabelAsync(label, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeleteRecipeLabelAsync(entry.EntityId, ct);
                break;
        }
    }

    private async Task ProcessPlanAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var plan = DeserializeEntity<Plan>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddPlanAsync(plan, ct);
                else
                    await _crudService.UpdatePlanAsync(plan, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeletePlanAsync(entry.EntityId, ct);
                break;
        }
    }

    private async Task ProcessPlannedMealAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var meal = DeserializeEntity<PlannedMeal>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddPlannedMealAsync(meal, ct);
                else
                    await _crudService.UpdatePlannedMealAsync(meal, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeletePlannedMealAsync(entry.EntityId, ct);
                break;
        }
    }

    private async Task ProcessShoppingListItemAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var item = DeserializeEntity<ShoppingListItem>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert)
                    await _crudService.AddShoppingListItemAsync(item, ct);
                else
                    await _crudService.UpdateShoppingListItemAsync(item, ct);
                break;
            case SyncOperationType.Delete:
                await _crudService.DeleteShoppingListItemAsync(entry.EntityId, ct);
                break;
        }
    }

    private static string GetEntityTypeName<T>() => typeof(T).Name;

    private static Guid GetEntityId<T>(T entity) where T : class
    {
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty == null)
            throw new InvalidOperationException($"Entity {typeof(T).Name} does not have an Id property");

        var value = idProperty.GetValue(entity);
        return value switch
        {
            Guid guid => guid,
            _ => throw new InvalidOperationException($"Entity {typeof(T).Name} Id is not a Guid")
        };
    }

    private static string SerializeEntity<T>(T entity) => JsonSerializer.Serialize(entity, JsonOptions);

    private static T DeserializeEntity<T>(string json) => 
        JsonSerializer.Deserialize<T>(json, JsonOptions) 
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");

    private void RaiseSyncStatusChanged(SyncStatus oldStatus, SyncStatus newStatus, string? message = null)
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs
        {
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Message = message
        });
    }

    private void RaiseSyncProgress(int total, int processed, int failed, string? entityType = null)
    {
        SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs
        {
            TotalItems = total,
            ProcessedItems = processed,
            FailedItems = failed,
            CurrentEntityType = entityType
        });
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _syncTimer?.Dispose();
        _syncLock.Dispose();
        _queueLock.Dispose();
        _disposed = true;
    }

    #endregion
}
