using System.Diagnostics;
using System.Text.Json;
using Foodbook.Data;
using Foodbook.Models;
using Foodbook.Models.DTOs;
using Foodbook.Services;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Services.Supabase;

public sealed class SupabaseSyncService : ISupabaseSyncService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthTokenStore _tokenStore;
    private readonly IPreferencesService _preferencesService;
    private readonly IThemeService _themeService;

    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private bool _disposed;

    private const int MaxRetryCount = 3;
    private const int QueueProcessingBatchSize = 100;
    private static readonly TimeSpan InProgressRecoveryThreshold = TimeSpan.FromMinutes(5);
    private static readonly string[] FatalErrorCodes = { "42501", "403", "401", "PGRST301" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        MaxDepth = 64
    };

    public event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;
    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    public SupabaseSyncService(IServiceProvider serviceProvider, IAuthTokenStore tokenStore)
    {
        _serviceProvider = serviceProvider;
        _tokenStore = tokenStore;
        _preferencesService = serviceProvider.GetRequiredService<IPreferencesService>();
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
    }

    #region State

    public async Task<SyncState?> GetSyncStateAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return null;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
    }

    public async Task<bool> IsCloudSyncEnabledAsync(CancellationToken ct = default)
    {
        var state = await GetSyncStateAsync(ct);
        return state?.IsCloudSyncEnabled ?? false;
    }

    #endregion

    #region Enable / Disable

    /// <summary>
    /// Enables cloud sync using smart merge (timestamp wins — no user dialog needed).
    /// First call: fetches cloud, merges with local, queues local-only for upload.
    /// Subsequent calls: resumes queue processing.
    /// </summary>
    public async Task EnableCloudSyncAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
        {
            Log("Cannot enable sync - no active account");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
        if (state == null)
        {
            state = new SyncState
            {
                AccountId = accountId.Value,
                IsCloudSyncEnabled = true,
                Status = SyncStatus.Idle,
                Priority = SyncPriority.Local,
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

        if (!state.InitialSyncCompleted)
        {
            Log("Initial sync not done — starting smart merge...");
            await StartSmartMergeInitialSyncAsync(ct);
        }
        else
        {
            await RetryFailedEntriesAsync(ct);
            _ = Task.Run(() => TryProcessQueueImmediatelyAsync(ct), ct);
        }
    }

    public async Task DisableCloudSyncAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
        if (state != null)
        {
            state.IsCloudSyncEnabled = false;
            state.Status = SyncStatus.Disabled;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Disabled, "Sync disabled");
    }

    #endregion

    #region Smart Merge Initial Sync

    /// <summary>
    /// Smart merge: cloud data + local data, newer UpdatedAt wins.
    /// No user choice required. Idempotent — safe to call multiple times.
    /// </summary>
    public async Task<bool> StartSmartMergeInitialSyncAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return false;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
        if (state?.InitialSyncCompleted == true) return true;

        if (state != null)
        {
            state.Status = SyncStatus.InitialSync;
            state.InitialSyncStartedUtc = DateTime.UtcNow;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.InitialSync, "Rozpoczynanie smart merge...");

        try
        {
            Log("=== SmartMergeInitialSync START ===");

            // 1. Fetch cloud snapshot
            RaiseSyncProgress(100, 5, 0, "Pobieranie danych z chmury");
            var cloudData = await ExecuteWithCrudServiceAsync(crud => crud.FetchAllCloudDataAsync(ct));
            Log($"Cloud snapshot: Recipes={cloudData.Recipes.Count}, Ingredients={cloudData.Ingredients.Count}, Plans={cloudData.Plans.Count}");

            // 2. Deduplicate local before merge (remove local duplicates that match cloud entities)
            try
            {
                var dedup = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
                if (dedup != null)
                {
                    var merged = await dedup.DeduplicateForCloudFirstAsync(db, cloudData, ct);
                    Log($"Dedup: merged {merged} local duplicates");
                }
            }
            catch (Exception ex) { Log($"Dedup warning (non-fatal): {ex.Message}"); }

            // 3. Import cloud entities — newer UpdatedAt wins
            RaiseSyncProgress(100, 30, 0, "Importowanie danych z chmury");
            var imported = await ImportCloudDataSmartAsync(db, cloudData, ct);
            Log($"Imported {imported} entities from cloud");

            // 4. Apply cloud user preferences if present
            if (cloudData.UserPreferences != null)
                ApplyUserPreferencesFromSnapshot(cloudData.UserPreferences);

            // 5. Queue local-only entities for upload to cloud
            RaiseSyncProgress(100, 60, 0, "Kolejkowanie danych lokalnych");
            var batchId = Guid.NewGuid();
            var queued = await QueueLocalOnlyEntitiesForSyncAsync(db, accountId.Value, batchId, cloudData, ct);
            Log($"Queued {queued} local-only entities for upload");

            // 6. Mark initial sync complete
            if (state != null)
            {
                state.InitialSyncCompleted = true;
                state.InitialSyncCompletedUtc = DateTime.UtcNow;
                state.Status = SyncStatus.Idle;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            // 7. Invalidate caches so UI refreshes
            await RefreshImportedDataAsync(imported, ct);

            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Idle,
                $"Merge zakończony: {imported} zaimportowanych, {queued} w kolejce");
            RaiseSyncProgress(100, 80, 0, "Przetwarzanie kolejki uploadu");

            // 8. Process upload queue immediately
            await TryProcessQueueImmediatelyAsync(ct);

            RaiseSyncProgress(100, 100, 0, "Gotowe");
            Log("=== SmartMergeInitialSync COMPLETE ===");
            return true;
        }
        catch (Exception ex)
        {
            Log($"ERROR in SmartMergeInitialSync: {ex.Message}");

            // Update state to Error using a fresh scope (original scope may be disposed)
            try
            {
                using var errScope = _serviceProvider.CreateScope();
                var errDb = errScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var errState = await errDb.SyncStates.FirstOrDefaultAsync(
                    s => s.AccountId == accountId.Value, ct);
                if (errState != null)
                {
                    errState.Status = SyncStatus.Error;
                    errState.LastSyncError = ex.Message;
                    errState.UpdatedUtc = DateTime.UtcNow;
                    await errDb.SaveChangesAsync(ct);
                }
            }
            catch { /* best effort */ }

            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Error, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Smart import: adds missing entities, updates if cloud version is newer (UpdatedAt).
    /// Does NOT overwrite local data that is newer than cloud.
    /// FK order: Folders → Labels → Recipes → Ingredients → Plans → PlannedMeals → ShoppingItems
    /// </summary>
    private async Task<int> ImportCloudDataSmartAsync(AppDbContext db, CloudDataSnapshot cloudData, CancellationToken ct)
    {
        int imported = 0;

        // Folders
        foreach (var folder in cloudData.Folders)
        {
            var existing = await db.Folders.FindAsync(new object[] { folder.Id }, ct);
            if (existing == null)
            {
                db.Folders.Add(folder);
                imported++;
            }
            else if (folder.UpdatedAt > existing.UpdatedAt)
            {
                existing.Name = folder.Name;
                existing.Description = folder.Description;
                existing.ParentFolderId = folder.ParentFolderId;
                existing.Order = folder.Order;
                existing.UpdatedAt = folder.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        // Recipe Labels
        foreach (var label in cloudData.RecipeLabels)
        {
            var existing = await db.RecipeLabels.FindAsync(new object[] { label.Id }, ct);
            if (existing == null)
            {
                db.RecipeLabels.Add(label);
                imported++;
            }
            else if (label.UpdatedAt > existing.UpdatedAt)
            {
                existing.Name = label.Name;
                existing.ColorHex = label.ColorHex;
                existing.UpdatedAt = label.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        // Recipes — import without navigation properties to avoid EF tracking issues
        foreach (var recipe in cloudData.Recipes)
        {
            var existing = await db.Recipes.FindAsync(new object[] { recipe.Id }, ct);
            if (existing == null)
            {
                db.Recipes.Add(new Recipe
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Description = recipe.Description,
                    Calories = recipe.Calories,
                    Protein = recipe.Protein,
                    Fat = recipe.Fat,
                    Carbs = recipe.Carbs,
                    IloscPorcji = recipe.IloscPorcji,
                    FolderId = recipe.FolderId,
                    CreatedAt = recipe.CreatedAt,
                    UpdatedAt = recipe.UpdatedAt
                });
                imported++;
            }
            else if (recipe.UpdatedAt > existing.UpdatedAt)
            {
                existing.Name = recipe.Name;
                existing.Description = recipe.Description;
                existing.Calories = recipe.Calories;
                existing.Protein = recipe.Protein;
                existing.Fat = recipe.Fat;
                existing.Carbs = recipe.Carbs;
                existing.IloscPorcji = recipe.IloscPorcji;
                existing.FolderId = recipe.FolderId;
                existing.UpdatedAt = recipe.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        // Ingredients
        foreach (var ing in cloudData.Ingredients)
        {
            var existing = await db.Ingredients.FindAsync(new object[] { ing.Id }, ct);
            if (existing == null)
            {
                db.Ingredients.Add(new Ingredient
                {
                    Id = ing.Id,
                    Name = ing.Name,
                    Quantity = ing.Quantity,
                    Unit = ing.Unit,
                    UnitWeight = ing.UnitWeight,
                    Calories = ing.Calories,
                    Protein = ing.Protein,
                    Fat = ing.Fat,
                    Carbs = ing.Carbs,
                    RecipeId = ing.RecipeId,
                    CreatedAt = ing.CreatedAt,
                    UpdatedAt = ing.UpdatedAt
                });
                imported++;
            }
            else if (ing.UpdatedAt > existing.UpdatedAt)
            {
                existing.Name = ing.Name;
                existing.Quantity = ing.Quantity;
                existing.Unit = ing.Unit;
                existing.UnitWeight = ing.UnitWeight;
                existing.Calories = ing.Calories;
                existing.Protein = ing.Protein;
                existing.Fat = ing.Fat;
                existing.Carbs = ing.Carbs;
                existing.RecipeId = ing.RecipeId;
                existing.UpdatedAt = ing.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        // Plans
        foreach (var plan in cloudData.Plans)
        {
            var existing = await db.Plans.FindAsync(new object[] { plan.Id }, ct);
            if (existing == null)
            {
                db.Plans.Add(plan);
                imported++;
            }
            else if (plan.UpdatedAt > existing.UpdatedAt)
            {
                existing.StartDate = plan.StartDate;
                existing.EndDate = plan.EndDate;
                existing.IsArchived = plan.IsArchived;
                existing.Type = plan.Type;
                existing.Title = plan.Title;
                existing.LinkedShoppingListPlanId = plan.LinkedShoppingListPlanId;
                existing.AccentColor = plan.AccentColor;
                existing.Emoji = plan.Emoji;
                existing.DurationDays = plan.DurationDays;
                existing.UpdatedAt = plan.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        // PlannedMeals
        foreach (var meal in cloudData.PlannedMeals)
        {
            var existing = await db.PlannedMeals.FindAsync(new object[] { meal.Id }, ct);
            if (existing == null)
            {
                db.PlannedMeals.Add(meal);
                imported++;
            }
            else if (meal.UpdatedAt > existing.UpdatedAt)
            {
                existing.RecipeId = meal.RecipeId;
                existing.PlanId = meal.PlanId;
                existing.Date = meal.Date;
                existing.Portions = meal.Portions;
                existing.UpdatedAt = meal.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        // ShoppingListItems
        foreach (var item in cloudData.ShoppingListItems)
        {
            var existing = await db.ShoppingListItems.FindAsync(new object[] { item.Id }, ct);
            if (existing == null)
            {
                db.ShoppingListItems.Add(item);
                imported++;
            }
            else if (item.UpdatedAt > existing.UpdatedAt)
            {
                existing.PlanId = item.PlanId;
                existing.IngredientName = item.IngredientName;
                existing.Quantity = item.Quantity;
                existing.Unit = item.Unit;
                existing.IsChecked = item.IsChecked;
                existing.Order = item.Order;
                existing.UpdatedAt = item.UpdatedAt;
                imported++;
            }
        }
        await db.SaveChangesAsync(ct);

        return imported;
    }

    #endregion

    #region Queue Management

    public async Task QueueForSyncAsync<T>(T entity, SyncOperationType operation, CancellationToken ct = default) where T : class
    {
        if (!await IsCloudSyncEnabledAsync(ct)) return;

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return;

        await _queueLock.WaitAsync(ct);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var entityId = GetEntityId(entity);
            var entityType = typeof(T).Name;

            // Coalesce: update existing pending entry for same entity (Delete supersedes everything)
            var existing = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value
                    && e.EntityType == entityType
                    && e.EntityId == entityId
                    && e.Status == SyncEntryStatus.Pending)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                if (operation == SyncOperationType.Delete)
                {
                    existing.OperationType = SyncOperationType.Delete;
                    existing.Payload = null;
                }
                else
                {
                    existing.OperationType = operation;
                    existing.Payload = SerializeEntity(entity);
                }
                existing.CreatedUtc = DateTime.UtcNow;
            }
            else
            {
                db.SyncQueue.Add(new SyncQueueEntry
                {
                    AccountId = accountId.Value,
                    EntityType = entityType,
                    EntityId = entityId,
                    OperationType = operation,
                    Payload = operation != SyncOperationType.Delete ? SerializeEntity(entity) : null,
                    Priority = 10
                });
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _queueLock.Release();
        }

        _ = Task.Run(() => TryProcessQueueImmediatelyAsync(ct), ct);
    }

    public async Task QueueBatchForSyncAsync<T>(IEnumerable<T> entities, SyncOperationType operation, CancellationToken ct = default) where T : class
    {
        if (!await IsCloudSyncEnabledAsync(ct)) return;

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return;

        var list = entities.ToList();
        if (list.Count == 0) return;

        await _queueLock.WaitAsync(ct);
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var batchId = Guid.NewGuid();
            var entityType = typeof(T).Name;

            foreach (var entity in list)
            {
                db.SyncQueue.Add(new SyncQueueEntry
                {
                    AccountId = accountId.Value,
                    EntityType = entityType,
                    EntityId = GetEntityId(entity),
                    OperationType = operation,
                    Payload = operation != SyncOperationType.Delete ? SerializeEntity(entity) : null,
                    Priority = 10,
                    BatchId = batchId
                });
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _queueLock.Release();
        }

        _ = Task.Run(() => TryProcessQueueImmediatelyAsync(ct), ct);
    }

    private async Task TryProcessQueueImmediatelyAsync(CancellationToken ct)
    {
        try { await ProcessQueueAsync(ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Log($"Queue processing background error: {ex.Message}"); }
    }

    #endregion

    #region Queue Processor — Batched Upsert + Delete

    public async Task<SyncResult> ProcessQueueAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
            return SyncResult.Ok(0, await GetPendingCountAsync(ct), TimeSpan.Zero);

        var sw = Stopwatch.StartNew();
        int processed = 0, failed = 0;

        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue) return SyncResult.Error("No active account");
            if (!await IsCloudSyncEnabledAsync(ct)) return SyncResult.Error("Sync not enabled");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var crud = scope.ServiceProvider.GetRequiredService<ISupabaseCrudService>();

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
            if (state == null) return SyncResult.Error("Sync state not found");

            await RecoverStaleInProgressEntriesAsync(db, accountId.Value, ct);

            var totalPending = await GetPendingCountAsync(db, accountId.Value, ct);
            if (totalPending == 0)
            {
                state.Status = SyncStatus.Idle;
                state.PendingItemsCount = 0;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return SyncResult.Ok(0, 0, sw.Elapsed);
            }

            state.Status = SyncStatus.Syncing;
            state.LastSyncAttemptUtc = DateTime.UtcNow;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Syncing, $"Synchronizacja {totalPending} elementów");

            string? fatalError = null;

            while (fatalError == null)
            {
                var batch = await db.SyncQueue
                    .Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending)
                    .OrderBy(e => e.Priority).ThenBy(e => e.CreatedUtc)
                    .Take(QueueProcessingBatchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                var now = DateTime.UtcNow;
                foreach (var e in batch) { e.Status = SyncEntryStatus.InProgress; e.LastAttemptUtc = now; }
                await db.SaveChangesAsync(ct);

                var result = await ProcessBatchGroupedAsync(crud, db, batch, totalPending, processed, ct);
                processed += result.Processed;
                failed += result.Failed;
                fatalError = result.FatalError;

                await db.SaveChangesAsync(ct);
                RaiseSyncProgress(totalPending, processed, failed, null);
            }

            var remaining = await GetPendingCountAsync(db, accountId.Value, ct);
            state.LastSyncUtc = DateTime.UtcNow;
            state.TotalItemsSynced += processed;
            state.PendingItemsCount = remaining;
            state.Status = fatalError != null ? SyncStatus.Error : SyncStatus.Idle;
            state.LastSyncError = fatalError ?? (failed > 0 ? $"{failed} elementów nie udało się zsynchronizować" : null);
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            RaiseSyncStatusChanged(SyncStatus.Syncing,
                fatalError != null ? SyncStatus.Error : SyncStatus.Idle,
                fatalError ?? $"Zsynchronizowano {processed} elementów");

            return fatalError != null
                ? SyncResult.Error(fatalError, failed)
                : SyncResult.Ok(processed, remaining, sw.Elapsed);
        }
        catch (Exception ex)
        {
            Log($"ProcessQueueAsync error: {ex.Message}");
            return SyncResult.Error(ex.Message, failed);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Groups batch entries by EntityType, sends batch upserts and individual deletes.
    /// Falls back to single-entry processing if batch upsert fails (non-fatal).
    /// </summary>
    private async Task<(int Processed, int Failed, string? FatalError)> ProcessBatchGroupedAsync(
        ISupabaseCrudService crud,
        AppDbContext db,
        List<SyncQueueEntry> batch,
        int totalPending,
        int processedOffset,
        CancellationToken ct)
    {
        int processed = 0, failed = 0;

        var upsertGroups = batch
            .Where(e => e.OperationType != SyncOperationType.Delete && !string.IsNullOrEmpty(e.Payload))
            .GroupBy(e => e.EntityType)
            .ToList();

        var deleteGroups = batch
            .Where(e => e.OperationType == SyncOperationType.Delete)
            .GroupBy(e => e.EntityType)
            .ToList();

        // ── UPSERTS ──────────────────────────────────────────────────────────────
        foreach (var group in upsertGroups)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var (p, fatalError) = await UpsertGroupAsync(crud, group.Key, group.ToList(), ct);
                foreach (var entry in group)
                {
                    if (fatalError != null)
                    {
                        entry.Status = SyncEntryStatus.Abandoned;
                        entry.LastError = fatalError;
                        failed++;
                    }
                    else
                    {
                        entry.Status = SyncEntryStatus.Completed;
                        entry.SyncedUtc = DateTime.UtcNow;
                        entry.LastError = null;
                    }
                }
                processed += p;

                if (fatalError != null)
                    return (processed, failed, fatalError);
            }
            catch (Exception ex)
            {
                Log($"Batch upsert {group.Key} failed ({ex.Message}) — falling back to single-entry");

                if (IsFatalError(ex.Message))
                {
                    foreach (var e in group) { e.Status = SyncEntryStatus.Abandoned; e.LastError = ex.Message; }
                    return (processed, failed + group.Count(), ex.Message);
                }

                // Fallback: process each entry individually
                foreach (var entry in group)
                {
                    try
                    {
                        await ProcessSingleEntryAsync(crud, entry, ct);
                        entry.Status = SyncEntryStatus.Completed;
                        entry.SyncedUtc = DateTime.UtcNow;
                        entry.LastError = null;
                        processed++;
                    }
                    catch (Exception entryEx)
                    {
                        entry.RetryCount++;
                        entry.LastError = entryEx.Message;
                        entry.Status = entry.RetryCount >= MaxRetryCount
                            ? SyncEntryStatus.Abandoned
                            : SyncEntryStatus.Failed;
                        failed++;
                    }
                }
            }
        }

        // ── DELETES ──────────────────────────────────────────────────────────────
        foreach (var group in deleteGroups)
        {
            foreach (var entry in group)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await ProcessSingleDeleteAsync(crud, entry, ct);
                    entry.Status = SyncEntryStatus.Completed;
                    entry.SyncedUtc = DateTime.UtcNow;
                    entry.LastError = null;
                    processed++;
                }
                catch (Exception ex)
                {
                    entry.RetryCount++;
                    entry.LastError = ex.Message;
                    entry.Status = entry.RetryCount >= MaxRetryCount
                        ? SyncEntryStatus.Abandoned
                        : SyncEntryStatus.Failed;
                    failed++;
                    Log($"Delete {entry.EntityType} {entry.EntityId} failed: {ex.Message}");

                    if (IsFatalError(ex.Message))
                        return (processed, failed, ex.Message);
                }
            }
        }

        return (processed, failed, null);
    }

    private async Task<(int Processed, string? FatalError)> UpsertGroupAsync(
        ISupabaseCrudService crud, string entityType, List<SyncQueueEntry> entries, CancellationToken ct)
    {
        switch (entityType)
        {
            case "Folder":
                await crud.UpsertFoldersBatchAsync(
                    entries.Select(e => DeserializeEntity<Folder>(e.Payload!)), ct);
                return (entries.Count, null);

            case "RecipeLabel":
                await crud.UpsertRecipeLabelsBatchAsync(
                    entries.Select(e => DeserializeEntity<RecipeLabel>(e.Payload!)), ct);
                return (entries.Count, null);

            case "Recipe":
                await crud.UpsertRecipesAsync(
                    entries.Select(e => DeserializeEntity<Recipe>(e.Payload!)), ct);
                return (entries.Count, null);

            case "Ingredient":
                await crud.UpsertIngredientsAsync(
                    entries.Select(e => DeserializeEntity<Ingredient>(e.Payload!)), ct);
                return (entries.Count, null);

            case "Plan":
                await crud.UpsertPlansBatchAsync(
                    entries.Select(e => DeserializeEntity<Plan>(e.Payload!)), ct);
                return (entries.Count, null);

            case "PlannedMeal":
                await crud.UpsertPlannedMealsBatchAsync(
                    entries.Select(e => DeserializeEntity<PlannedMeal>(e.Payload!)), ct);
                return (entries.Count, null);

            case "ShoppingListItem":
                await crud.UpsertShoppingListItemsBatchAsync(
                    entries.Select(e => DeserializeEntity<ShoppingListItem>(e.Payload!)), ct);
                return (entries.Count, null);

            case "UserPreferencesDto":
                foreach (var e in entries)
                    await crud.UpsertUserPreferencesAsync(DeserializeEntity<UserPreferencesDto>(e.Payload!), ct);
                return (entries.Count, null);

            default:
                throw new NotSupportedException($"Upsert not supported for entity type: {entityType}");
        }
    }

    /// <summary>
    /// Handles delete operations. Always uses SoftDeleteAsync to avoid RLS violations.
    /// </summary>
    private static async Task ProcessSingleDeleteAsync(
        ISupabaseCrudService crud, SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.EntityType)
        {
            case "Folder":           await crud.DeleteFolderAsync(entry.EntityId, ct);           break;
            case "Recipe":           await crud.DeleteRecipeAsync(entry.EntityId, ct);           break;
            case "Ingredient":       await crud.DeleteIngredientAsync(entry.EntityId, ct);       break;
            case "RecipeLabel":      await crud.DeleteRecipeLabelAsync(entry.EntityId, ct);      break;
            case "Plan":             await crud.DeletePlanAsync(entry.EntityId, ct);             break;
            case "PlannedMeal":      await crud.DeletePlannedMealAsync(entry.EntityId, ct);      break;
            case "ShoppingListItem": await crud.DeleteShoppingListItemAsync(entry.EntityId, ct); break;
            default: throw new NotSupportedException($"Delete not supported for: {entry.EntityType}");
        }
    }

    /// <summary>
    /// Fallback single-entry upsert (used when batch fails).
    /// </summary>
    private static async Task ProcessSingleEntryAsync(
        ISupabaseCrudService crud, SyncQueueEntry entry, CancellationToken ct)
    {
        if (entry.OperationType == SyncOperationType.Delete)
        {
            await ProcessSingleDeleteAsync(crud, entry, ct);
            return;
        }

        switch (entry.EntityType)
        {
            case "Folder":
                var folder = DeserializeEntity<Folder>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddFolderAsync(folder, ct);
                else await crud.UpdateFolderAsync(folder, ct);
                break;
            case "Recipe":
                var recipe = DeserializeEntity<Recipe>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddRecipeAsync(recipe, ct);
                else await crud.UpdateRecipeAsync(recipe, ct);
                break;
            case "Ingredient":
                var ingredient = DeserializeEntity<Ingredient>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddIngredientAsync(ingredient, ct);
                else await crud.UpdateIngredientAsync(ingredient, ct);
                break;
            case "RecipeLabel":
                var label = DeserializeEntity<RecipeLabel>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddRecipeLabelAsync(label, ct);
                else await crud.UpdateRecipeLabelAsync(label, ct);
                break;
            case "Plan":
                var plan = DeserializeEntity<Plan>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddPlanAsync(plan, ct);
                else await crud.UpdatePlanAsync(plan, ct);
                break;
            case "PlannedMeal":
                var meal = DeserializeEntity<PlannedMeal>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddPlannedMealAsync(meal, ct);
                else await crud.UpdatePlannedMealAsync(meal, ct);
                break;
            case "ShoppingListItem":
                var item = DeserializeEntity<ShoppingListItem>(entry.Payload!);
                if (entry.OperationType == SyncOperationType.Insert) await crud.AddShoppingListItemAsync(item, ct);
                else await crud.UpdateShoppingListItemAsync(item, ct);
                break;
            case "UserPreferencesDto":
                var prefs = DeserializeEntity<UserPreferencesDto>(entry.Payload!);
                await crud.UpsertUserPreferencesAsync(prefs, ct);
                break;
            default:
                throw new NotSupportedException($"Entity type {entry.EntityType} not supported");
        }
    }

    #endregion

    #region Force Sync

    public Task<SyncResult> ForceSyncAsync(CancellationToken ct = default) => ProcessQueueAsync(ct);

    public async Task<SyncResult> ForceSyncAllAsync(CancellationToken ct = default)
    {
        var downloadResult = await ForceDownloadFromCloudAsync(ct);
        if (!downloadResult.Success)
            Log($"Cloud download warning: {downloadResult.ErrorMessage}");

        return await ProcessQueueAsync(ct);
    }

    public async Task<SyncResult> ForceDownloadFromCloudAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(10), ct))
            return SyncResult.Error("Could not acquire sync lock");

        var sw = Stopwatch.StartNew();
        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue) return SyncResult.Error("No active account");
            if (!await IsCloudSyncEnabledAsync(ct)) return SyncResult.Error("Sync not enabled");

            RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Syncing, "Pobieranie danych z chmury...");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cloudData = await ExecuteWithCrudServiceAsync(crud => crud.FetchAllCloudDataAsync(ct));

            try
            {
                var dedup = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
                if (dedup != null)
                    await dedup.DeduplicateForCloudFirstAsync(db, cloudData, ct);
            }
            catch (Exception ex) { Log($"Dedup warning: {ex.Message}"); }

            var imported = await ImportCloudDataSmartAsync(db, cloudData, ct);
            await RefreshImportedDataAsync(imported, ct);

            if (cloudData.UserPreferences != null)
                ApplyUserPreferencesFromSnapshot(cloudData.UserPreferences);

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
            if (state != null)
            {
                state.LastSyncUtc = DateTime.UtcNow;
                state.LastCloudPollUtc = DateTime.UtcNow;
                state.Status = SyncStatus.Idle;
                state.LastSyncError = null;
                state.TotalItemsSynced += imported;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            sw.Stop();
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, $"Pobrano {imported} elementów");
            return SyncResult.Ok(imported, 0, sw.Elapsed);
        }
        catch (Exception ex)
        {
            Log($"ForceDownload error: {ex.Message}");
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Error, ex.Message);
            return SyncResult.Error(ex.Message);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    #endregion

    #region Queue Local-Only (for initial sync upload)

    private async Task<int> QueueLocalOnlyEntitiesForSyncAsync(
        AppDbContext db, Guid accountId, Guid batchId, CloudDataSnapshot cloudData, CancellationToken ct)
    {
        int queued = 0;

        var cloudFolderIds = new HashSet<Guid>(cloudData.Folders.Select(f => f.Id));
        var cloudLabelIds = new HashSet<Guid>(cloudData.RecipeLabels.Select(l => l.Id));
        var cloudRecipeIds = new HashSet<Guid>(cloudData.Recipes.Select(r => r.Id));
        var cloudIngIds = new HashSet<Guid>(cloudData.Ingredients.Select(i => i.Id));
        var cloudPlanIds = new HashSet<Guid>(cloudData.Plans.Select(p => p.Id));
        var cloudMealIds = new HashSet<Guid>(cloudData.PlannedMeals.Select(m => m.Id));
        var cloudShopIds = new HashSet<Guid>(cloudData.ShoppingListItems.Select(s => s.Id));

        foreach (var f in (await db.Folders.ToListAsync(ct)).Where(x => !cloudFolderIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, f, batchId, 1)); queued++; }

        foreach (var l in (await db.RecipeLabels.ToListAsync(ct)).Where(x => !cloudLabelIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, l, batchId, 2)); queued++; }

        foreach (var r in (await db.Recipes.ToListAsync(ct)).Where(x => !cloudRecipeIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, r, batchId, 3)); queued++; }

        foreach (var i in (await db.Ingredients.ToListAsync(ct)).Where(x => !cloudIngIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, i, batchId, 4)); queued++; }

        foreach (var p in (await db.Plans.ToListAsync(ct)).Where(x => !cloudPlanIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, p, batchId, 5)); queued++; }

        foreach (var m in (await db.PlannedMeals.ToListAsync(ct)).Where(x => !cloudMealIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, m, batchId, 6)); queued++; }

        foreach (var s in (await db.ShoppingListItems.ToListAsync(ct)).Where(x => !cloudShopIds.Contains(x.Id)))
        { db.SyncQueue.Add(MakeInitialEntry(accountId, s, batchId, 7)); queued++; }

        await db.SaveChangesAsync(ct);
        return queued;
    }

    private SyncQueueEntry MakeInitialEntry<T>(Guid accountId, T entity, Guid batchId, int priority) where T : class
    {
        return new SyncQueueEntry
        {
            AccountId = accountId,
            EntityType = typeof(T).Name,
            EntityId = GetEntityId(entity),
            OperationType = SyncOperationType.Insert,
            Payload = SerializeEntity(entity),
            Priority = priority,
            BatchId = batchId
        };
    }

    #endregion

    #region Helpers

    private async Task<int> RecoverStaleInProgressEntriesAsync(AppDbContext db, Guid accountId, CancellationToken ct)
    {
        var threshold = DateTime.UtcNow - InProgressRecoveryThreshold;
        var stale = await db.SyncQueue
            .Where(e => e.AccountId == accountId
                && e.Status == SyncEntryStatus.InProgress
                && (!e.LastAttemptUtc.HasValue || e.LastAttemptUtc.Value <= threshold))
            .ToListAsync(ct);

        foreach (var e in stale) e.Status = SyncEntryStatus.Pending;
        if (stale.Count > 0) await db.SaveChangesAsync(ct);
        return stale.Count;
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return 0;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await GetPendingCountAsync(db, accountId.Value, ct);
    }

    private static Task<int> GetPendingCountAsync(AppDbContext db, Guid accountId, CancellationToken ct)
        => db.SyncQueue.CountAsync(e => e.AccountId == accountId && e.Status == SyncEntryStatus.Pending, ct);

    public async Task RetryFailedEntriesAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failed = await db.SyncQueue
            .Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Failed)
            .ToListAsync(ct);

        foreach (var e in failed) { e.Status = SyncEntryStatus.Pending; e.RetryCount = 0; e.LastError = null; }
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearFailedEntriesAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entries = await db.SyncQueue
            .Where(e => e.AccountId == accountId.Value
                && (e.Status == SyncEntryStatus.Failed || e.Status == SyncEntryStatus.Abandoned))
            .ToListAsync(ct);

        db.SyncQueue.RemoveRange(entries);
        await db.SaveChangesAsync(ct);
    }

    private async Task RefreshImportedDataAsync(int count, CancellationToken ct)
    {
        if (count <= 0) return;
        try { (_serviceProvider.GetService(typeof(IIngredientService)) as IIngredientService)?.InvalidateCache(); } catch { }
        try { await AppEvents.RaiseIngredientsChangedAsync(); } catch { }
        try { await AppEvents.RaiseRecipesChangedAsync(); } catch { }
    }

    private static bool IsFatalError(string message)
        => FatalErrorCodes.Any(c => message.Contains(c, StringComparison.OrdinalIgnoreCase));

    private async Task<T> ExecuteWithCrudServiceAsync<T>(Func<ISupabaseCrudService, Task<T>> action)
    {
        using var scope = _serviceProvider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ISupabaseCrudService>());
    }

    private void RaiseSyncStatusChanged(SyncStatus old, SyncStatus @new, string? msg = null)
        => SyncStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs { OldStatus = old, NewStatus = @new, Message = msg });

    private void RaiseSyncProgress(int total, int done, int failed, string? entityType)
        => SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs
        { TotalItems = total, ProcessedItems = done, FailedItems = failed, CurrentEntityType = entityType });

    private static Guid GetEntityId<T>(T entity) where T : class
    {
        var v = typeof(T).GetProperty("Id")?.GetValue(entity)
            ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id property");
        return v is Guid g ? g : throw new InvalidOperationException($"{typeof(T).Name}.Id is not Guid");
    }

    private static string SerializeEntity<T>(T entity)
    {
        if (entity is UserPreferencesDto prefs)
        {
            return JsonSerializer.Serialize(new
            {
                id = prefs.Id,
                theme = prefs.Theme,
                color_theme = prefs.ColorTheme,
                is_colorful_background = prefs.IsColorfulBackground,
                is_wallpaper_enabled = prefs.IsWallpaperEnabled,
                font_family = prefs.FontFamily,
                font_size = prefs.FontSize,
                language = prefs.Language,
                created_at = prefs.CreatedAt,
                updated_at = prefs.UpdatedAt
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(entity, JsonOptions);
    }

    private static T DeserializeEntity<T>(string json)
    {
        if (!string.IsNullOrEmpty(json) &&
            (json.Contains("\"PrimaryKey\"", StringComparison.OrdinalIgnoreCase) ||
             json.Contains("\"TableName\"", StringComparison.OrdinalIgnoreCase)))
        {
            json = StripMetadataKeys(json);
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");
    }

    private static string StripMetadataKeys(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return json;
            using var ms = new System.IO.MemoryStream();
            using (var w = new Utf8JsonWriter(ms))
            {
                w.WriteStartObject();
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    var norm = p.Name.Replace("_", "").ToLowerInvariant();
                    if (norm is "primarykey" or "tablename") continue;
                    p.WriteTo(w);
                }

                w.WriteEndObject();
            }

            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return json; }
    }

    private static void Log(string msg) => Debug.WriteLine($"[SyncService] {msg}");

    #endregion

    #region User Preferences

    public bool ApplyUserPreferencesFromSnapshot(UserPreferencesDto cloudPrefs)
    {
        try
        {
            if (cloudPrefs == null) return false;
            _preferencesService.SuppressCloudSync();
            try
            {
                if (!string.IsNullOrWhiteSpace(cloudPrefs.Language))
                    _preferencesService.SaveLanguage(cloudPrefs.Language);

                if (Enum.TryParse<Foodbook.Models.AppTheme>(cloudPrefs.Theme, out var theme))
                {
                    _preferencesService.SaveTheme(theme);
                    _themeService.SetTheme(theme);
                }

                if (Enum.TryParse<AppColorTheme>(cloudPrefs.ColorTheme, out var colorTheme))
                {
                    _preferencesService.SaveColorTheme(colorTheme);
                    _themeService.SetColorTheme(colorTheme);
                }

                _preferencesService.SaveColorfulBackground(cloudPrefs.IsColorfulBackground);
                _preferencesService.SaveWallpaperEnabled(cloudPrefs.IsWallpaperEnabled);

                var fontService = _serviceProvider.GetService(typeof(IFontService)) as IFontService;

                if (Enum.TryParse<AppFontFamily>(cloudPrefs.FontFamily, out var fontFamily))
                {
                    _preferencesService.SaveFontFamily(fontFamily);
                    fontService?.SetFontFamily(fontFamily);
                }

                if (Enum.TryParse<AppFontSize>(cloudPrefs.FontSize, out var fontSize))
                {
                    _preferencesService.SaveFontSize(fontSize);
                    fontService?.SetFontSize(fontSize);
                }

                Log("User preferences applied from snapshot");
                return true;
            }
            finally
            {
                _preferencesService.ResumeCloudSync();
            }
        }
        catch (Exception ex)
        {
            Log($"ApplyPreferences error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LoadUserPreferencesFromCloudAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var prefs = await ExecuteWithCrudServiceAsync(crud => crud.GetUserPreferencesAsync(userId, ct));
            return prefs != null && ApplyUserPreferencesFromSnapshot(prefs);
        }
        catch (Exception ex)
        {
            Log($"LoadPreferences error: {ex.Message}");
            return false;
        }
    }

    public async Task SaveUserPreferencesToCloudAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var dto = new UserPreferencesDto
            {
                Id = userId,
                Theme = _preferencesService.GetSavedTheme().ToString(),
                ColorTheme = _preferencesService.GetSavedColorTheme().ToString(),
                IsColorfulBackground = _preferencesService.GetIsColorfulBackgroundEnabled(),
                IsWallpaperEnabled = _preferencesService.GetIsWallpaperEnabled(),
                FontFamily = _preferencesService.GetSavedFontFamily().ToString(),
                FontSize = _preferencesService.GetSavedFontSize().ToString(),
                Language = _preferencesService.GetSavedLanguage(),
                UpdatedAt = DateTime.UtcNow
            };
            await QueueForSyncAsync(dto, SyncOperationType.Update, ct);
        }
        catch (Exception ex) { Log($"SavePreferences error: {ex.Message}"); }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _syncLock.Dispose();
        _queueLock.Dispose();
        _disposed = true;
    }
}
