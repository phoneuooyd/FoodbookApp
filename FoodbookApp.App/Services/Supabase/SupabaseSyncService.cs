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
    
    // Random delay between sync attempts (30 seconds - 4 minutes) to avoid rate limiting
    private static readonly Random _random = new();
    private const int MinDelaySeconds = 30;
    private const int MaxDelaySeconds = 240; // 4 minutes

    // Error codes that should stop sync immediately (RLS violations, auth errors)
    private static readonly string[] FatalErrorCodes = ["42501", "403", "401", "PGRST301"];

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

    public async Task<SyncState?> GetSyncStateAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return null;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
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

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);

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

        // Run deduplication before initial sync to avoid sending duplicates
        await RunDeduplicationBeforeSyncAsync(ct);

        if (!state.InitialSyncCompleted)
        {
            _ = StartInitialSyncAsync(ct);
        }

        StartSyncTimer();
    }

    /// <summary>
    /// Runs deduplication service to remove duplicates from sync queue before processing.
    /// </summary>
    private async Task RunDeduplicationBeforeSyncAsync(CancellationToken ct)
    {
        try
        {
            var deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
            if (deduplicationService == null)
            {
                Debug.WriteLine("[SyncService] DeduplicationService not available");
                return;
            }

            Debug.WriteLine("[SyncService] Running deduplication before sync...");
            
            // Fetch cloud data if not already cached
            if (!deduplicationService.IsCachePopulated)
            {
                await deduplicationService.FetchCloudDataAsync(ct);
            }
            
            // Deduplicate sync queue
            var removed = await deduplicationService.DeduplicateSyncQueueAsync(ct);
            Debug.WriteLine($"[SyncService] Deduplication complete: {removed} duplicates removed from queue");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SyncService] Deduplication error (non-fatal): {ex.Message}");
            // Don't throw - deduplication is optional optimization
        }
    }

    public async Task DisableCloudSyncAsync(CancellationToken ct = default)
    {
        StopSyncTimer();

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

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

        Debug.WriteLine($"[SyncService] Cloud sync disabled for account {accountId.Value}");
        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Disabled, "Sync disabled by user");
    }

    public async Task QueueForSyncAsync<T>(T entity, SyncOperationType operation, CancellationToken ct = default) where T : class
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
            var entityType = typeof(T).Name;

            var existingEntry = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value && e.EntityType == entityType && e.EntityId == entityId && e.Status == SyncEntryStatus.Pending)
                .FirstOrDefaultAsync(ct);

            if (existingEntry != null)
            {
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
            }

            await db.SaveChangesAsync(ct);
        }
        finally
        {
            _queueLock.Release();
        }
    }

    public async Task QueueBatchForSyncAsync<T>(IEnumerable<T> entities, SyncOperationType operation, CancellationToken ct = default) where T : class
    {
        if (!await IsCloudSyncEnabledAsync(ct))
            return;

        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

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

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
        if (state == null || state.InitialSyncCompleted)
            return true;

        state.Status = SyncStatus.InitialSync;
        state.InitialSyncStartedUtc = DateTime.UtcNow;
        state.UpdatedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.InitialSync, "Starting initial sync");

        try
        {
            var batchId = Guid.NewGuid();

            await QueueAllEntitiesForInitialSyncAsync(db, accountId.Value, batchId, ct);

            state.InitialSyncCompleted = true;
            state.InitialSyncCompletedUtc = DateTime.UtcNow;
            state.Status = SyncStatus.Idle;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Idle, "Initial sync completed");

            _ = ProcessQueueAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            state.Status = SyncStatus.Error;
            state.LastSyncError = ex.Message;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Error, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if an error message contains a fatal error code that should stop sync immediately.
    /// </summary>
    private static bool IsFatalError(string errorMessage)
    {
        return FatalErrorCodes.Any(code => errorMessage.Contains(code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SyncResult> ProcessQueueAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            return SyncResult.Ok(0, await GetPendingCountAsync(ct), TimeSpan.Zero);
        }

        var sw = Stopwatch.StartNew();
        var processed = 0;
        var failed = 0;
        string? fatalError = null;

        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
                return SyncResult.Error("No active account");

            if (!await IsCloudSyncEnabledAsync(ct))
                return SyncResult.Error("Sync not enabled");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
            if (state == null)
                return SyncResult.Error("Sync state not found");

            state.Status = SyncStatus.Syncing;
            state.LastSyncAttemptUtc = DateTime.UtcNow;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var pending = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending)
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.CreatedUtc)
                .Take(BatchSize)
                .ToListAsync(ct);

            var totalPending = await db.SyncQueue.CountAsync(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending, ct);

            Log($"Processing queue: {totalPending} items pending");

            if (pending.Count == 0)
            {
                state.Status = SyncStatus.Idle;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                sw.Stop();
                RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, "Queue empty");
                return SyncResult.Ok(0, 0, sw.Elapsed);
            }

            foreach (var entry in pending)
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
                    entry.RetryCount++;
                    entry.LastError = ex.Message;
                    entry.Status = entry.RetryCount >= MaxRetryCount ? SyncEntryStatus.Abandoned : SyncEntryStatus.Failed;
                    failed++;
                    Log($"Error processing entry {entry.EntityId}: {ex.Message}");

                    // FAIL-FAST: Check if this is a fatal error (RLS violation, auth error)
                    if (IsFatalError(ex.Message))
                    {
                        fatalError = ex.Message;
                        Log($"FATAL ERROR detected - stopping sync immediately: {ex.Message}");
                        
                        // Mark entry as abandoned so we don't retry it
                        entry.Status = SyncEntryStatus.Abandoned;
                        await db.SaveChangesAsync(ct);
                        
                        // Break out of the loop - don't process more entries
                        break;
                    }
                }

                await db.SaveChangesAsync(ct);
            }

            // Update state based on whether we hit a fatal error
            if (fatalError != null)
            {
                state.Status = SyncStatus.Error;
                state.LastSyncError = $"Sync stopped: {fatalError}";
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                
                sw.Stop();
                RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Error, $"Sync stopped due to fatal error: {fatalError}");
                
                // Stop the sync timer to prevent continuous retries
                StopSyncTimer();
                
                return SyncResult.Error($"Sync stopped: {fatalError}", failed);
            }

            // After processing the batch, wait a randomized interval before next batch
            if (processed > 0 && (await GetPendingCountAsync(ct)) > 0)
            {
                var delayMs = _random.Next(MinDelaySeconds * 1000, (MaxDelaySeconds + 1) * 1000);
                Log($"Batch processed ({processed}) - waiting {delayMs / 1000}s before next batch...");
                await Task.Delay(delayMs, ct);
            }

            state.LastSyncUtc = DateTime.UtcNow;
            state.TotalItemsSynced += processed;
            state.PendingItemsCount = totalPending - processed;
            state.Status = SyncStatus.Idle;
            state.LastSyncError = failed > 0 ? $"{failed} items failed" : null;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, $"Synced {processed} items" + (failed > 0 ? $", {failed} failed" : ""));
            return SyncResult.Ok(processed, state.PendingItemsCount, sw.Elapsed);
        }
        catch (Exception ex)
        {
            return SyncResult.Error(ex.Message, failed);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public Task<SyncResult> ForceSyncAsync(CancellationToken ct = default)
    {
        return ProcessQueueAsync(ct);
    }

    /// <summary>
    /// Forces synchronization of all pending items without delays.
    /// Used for manual "Sync Now" button clicks.
    /// </summary>
    public async Task<SyncResult> ForceSyncAllAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(5), ct))
        {
            return SyncResult.Ok(0, await GetPendingCountAsync(ct), TimeSpan.Zero);
        }

        var sw = Stopwatch.StartNew();
        var processed = 0;
        var failed = 0;
        string? fatalError = null;

        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
                return SyncResult.Error("No active account");

            if (!await IsCloudSyncEnabledAsync(ct))
                return SyncResult.Error("Sync not enabled");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
            if (state == null)
                return SyncResult.Error("Sync state not found");

            state.Status = SyncStatus.Syncing;
            state.LastSyncAttemptUtc = DateTime.UtcNow;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Force sync: get ALL pending items (no BatchSize limit)
            var allPending = await db.SyncQueue
                .Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending)
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.CreatedUtc)
                .ToListAsync(ct);

            Log($"Force sync: processing {allPending.Count} items (no limit)");

            if (allPending.Count == 0)
            {
                state.Status = SyncStatus.Idle;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                sw.Stop();
                RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, "Queue empty");
                return SyncResult.Ok(0, 0, sw.Elapsed);
            }

            foreach (var entry in allPending)
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

                    RaiseSyncProgress(allPending.Count, processed, failed, entry.EntityType);
                    
                    // No delay for force sync - send all items as fast as possible
                }
                catch (Exception ex)
                {
                    entry.RetryCount++;
                    entry.LastError = ex.Message;
                    entry.Status = entry.RetryCount >= MaxRetryCount ? SyncEntryStatus.Abandoned : SyncEntryStatus.Failed;
                    failed++;
                    Log($"Error processing entry {entry.EntityId}: {ex.Message}");

                    // FAIL-FAST: Check if this is a fatal error
                    if (IsFatalError(ex.Message))
                    {
                        fatalError = ex.Message;
                        Log($"FATAL ERROR detected - stopping force sync: {ex.Message}");
                        
                        entry.Status = SyncEntryStatus.Abandoned;
                        await db.SaveChangesAsync(ct);
                        break;
                    }
                }

                await db.SaveChangesAsync(ct);
            }

            if (fatalError != null)
            {
                state.Status = SyncStatus.Error;
                state.LastSyncError = $"Force sync stopped: {fatalError}";
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                
                sw.Stop();
                RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Error, $"Force sync stopped: {fatalError}");
                StopSyncTimer();
                
                return SyncResult.Error($"Force sync stopped: {fatalError}", failed);
            }

            state.LastSyncUtc = DateTime.UtcNow;
            state.TotalItemsSynced += processed;
            state.PendingItemsCount = 0;
            state.Status = SyncStatus.Idle;
            state.LastSyncError = failed > 0 ? $"{failed} items failed" : null;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, $"Force synced {processed} items" + (failed > 0 ? $", {failed} failed" : ""));
            return SyncResult.Ok(processed, 0, sw.Elapsed);
        }
        catch (Exception ex)
        {
            return SyncResult.Error(ex.Message, failed);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return 0;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.SyncQueue.CountAsync(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Pending, ct);
    }

    public async Task ClearFailedEntriesAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failedEntries = await db.SyncQueue
            .Where(e => e.AccountId == accountId.Value && (e.Status == SyncEntryStatus.Failed || e.Status == SyncEntryStatus.Abandoned))
            .ToListAsync(ct);

        db.SyncQueue.RemoveRange(failedEntries);
        await db.SaveChangesAsync(ct);
    }

    public async Task RetryFailedEntriesAsync(CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var failedEntries = await db.SyncQueue.Where(e => e.AccountId == accountId.Value && e.Status == SyncEntryStatus.Failed).ToListAsync(ct);
        foreach (var entry in failedEntries)
        {
            entry.Status = SyncEntryStatus.Pending;
            entry.RetryCount = 0;
            entry.LastError = null;
        }

        await db.SaveChangesAsync(ct);
    }

    public void StartSyncTimer()
    {
        StopSyncTimer();
        _syncTimer = new Timer(async _ => await ProcessQueueAsync(), null, DefaultSyncInterval, DefaultSyncInterval);
    }

    public void StopSyncTimer()
    {
        _syncTimer?.Dispose();
        _syncTimer = null;
    }

    private async Task QueueAllEntitiesForInitialSyncAsync(AppDbContext db, Guid accountId, Guid batchId, CancellationToken ct)
    {
        var folders = await db.Folders.ToListAsync(ct);
        foreach (var folder in folders)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, folder, batchId, priority: 1));
        }

        var labels = await db.RecipeLabels.ToListAsync(ct);
        foreach (var label in labels)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, label, batchId, priority: 2));
        }

        var recipes = await db.Recipes.ToListAsync(ct);
        foreach (var recipe in recipes)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, recipe, batchId, priority: 3));
        }

        var ingredients = await db.Ingredients.ToListAsync(ct);
        foreach (var ingredient in ingredients)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, ingredient, batchId, priority: 4));
        }

        var plans = await db.Plans.ToListAsync(ct);
        foreach (var plan in plans)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, plan, batchId, priority: 5));
        }

        var plannedMeals = await db.PlannedMeals.ToListAsync(ct);
        foreach (var meal in plannedMeals)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, meal, batchId, priority: 6));
        }

        var shoppingItems = await db.ShoppingListItems.ToListAsync(ct);
        foreach (var item in shoppingItems)
        {
            db.SyncQueue.Add(CreateInitialSyncEntry(accountId, item, batchId, priority: 7));
        }

        await db.SaveChangesAsync(ct);
    }

    private SyncQueueEntry CreateInitialSyncEntry<T>(Guid accountId, T entity, Guid batchId, int priority) where T : class
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

    private async Task ProcessSingleEntryAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        try
        {
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
        catch (Exception ex)
        {
            Log($"Error processing entry {entry.EntityId}: {ex.Message}\n{ex.StackTrace}");
            throw;
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

    private static Guid GetEntityId<T>(T entity) where T : class
    {
        var idProp = typeof(T).GetProperty("Id") ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id");
        var value = idProp.GetValue(entity);
        return value is Guid g ? g : throw new InvalidOperationException($"{typeof(T).Name}.Id is not Guid");
    }

    private static string SerializeEntity<T>(T entity) => JsonSerializer.Serialize(entity, JsonOptions);
    private static T DeserializeEntity<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");

    private void RaiseSyncStatusChanged(SyncStatus oldStatus, SyncStatus newStatus, string? message = null)
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs { OldStatus = oldStatus, NewStatus = newStatus, Message = message });
    }

    private void RaiseSyncProgress(int total, int processed, int failed, string? entityType = null)
    {
        SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs { TotalItems = total, ProcessedItems = processed, FailedItems = failed, CurrentEntityType = entityType });
    }

    private static void Log(string message) => System.Diagnostics.Debug.WriteLine($"[SupabaseSyncService] {message}");

    public void Dispose()
    {
        if (_disposed) return;
        _syncTimer?.Dispose();
        _syncLock.Dispose();
        _queueLock.Dispose();
        _disposed = true;
    }
}
