using System.Diagnostics;
using System.Text.Json;
using Foodbook.Data;
using Foodbook.Models;
using Foodbook.Models.DTOs;
using FoodbookApp.Interfaces;
using FoodbookApp.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Services.Supabase;

/// <summary>
/// Service for synchronizing local data with Supabase cloud.
/// Uses a queue-based approach with interval-based processing to optimize bandwidth.
/// 
/// DESIGN PRINCIPLES:
/// - Deduplication is OPTIONAL optimization - sync MUST proceed even if deduplication fails
/// - Empty cloud (new user) is a valid state - sync should proceed normally
/// - All errors should be logged but not block the main sync flow
/// </summary>
public sealed class SupabaseSyncService : ISupabaseSyncService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAuthTokenStore _tokenStore;
    private readonly IPreferencesService _preferencesService;
    private readonly IThemeService _themeService;

    private CancellationTokenSource? _syncLoopCts;
    private Task? _syncLoopTask;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private bool _disposed;

    private const int MaxRetryCount = 3;
    private const int BatchSize = 3000;  // Zwiêkszone z 50 na 80
    private static readonly TimeSpan DefaultSyncInterval = TimeSpan.FromMinutes(5);
    
    // Random delay between sync attempts (30 seconds - 4 minutes) to avoid rate limiting
    private static readonly Random _random = new();
    private const int MinDelaySeconds = 5;
    private const int MaxDelaySeconds = 20; // 4 minutes

    // Error codes that should stop sync immediately (RLS violations, auth errors)
    private static readonly string[] FatalErrorCodes = new[] { "42501", "403", "401", "PGRST301" };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        // CRITICAL: Handle circular references (Recipe -> Ingredients -> Recipe)
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        MaxDepth = 64 // Default but explicit for clarity
    };

    public event EventHandler<SyncStatusChangedEventArgs>? SyncStatusChanged;
    public event EventHandler<SyncProgressEventArgs>? SyncProgressChanged;

    public SupabaseSyncService(
        IServiceProvider serviceProvider,
        IAuthTokenStore tokenStore)
    {
        _serviceProvider = serviceProvider;
        _tokenStore = tokenStore;
        
        // Resolve preferences and theme services (optional - may be null during startup)
        try
        {
            _preferencesService = serviceProvider.GetService(typeof(IPreferencesService)) as IPreferencesService 
                ?? throw new InvalidOperationException("IPreferencesService not registered");
            _themeService = serviceProvider.GetService(typeof(IThemeService)) as IThemeService 
                ?? throw new InvalidOperationException("IThemeService not registered");
        }
        catch (Exception ex)
        {
            Log($"WARNING: Failed to resolve preferences/theme services: {ex.Message}");
            throw;
        }
    }

    private async Task ExecuteWithCrudServiceAsync(Func<ISupabaseCrudService, Task> action)
    {
        using var scope = _serviceProvider.CreateScope();
        var crudService = scope.ServiceProvider.GetRequiredService<ISupabaseCrudService>();
        await action(crudService);
    }

    private async Task<T> ExecuteWithCrudServiceAsync<T>(Func<ISupabaseCrudService, Task<T>> action)
    {
        using var scope = _serviceProvider.CreateScope();
        var crudService = scope.ServiceProvider.GetRequiredService<ISupabaseCrudService>();
        return await action(crudService);
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

    public async Task EnableCloudSyncAsync(SyncPriority priority = SyncPriority.Local, CancellationToken ct = default)
    {
        var accountId = await _tokenStore.GetActiveAccountIdAsync();
        if (!accountId.HasValue)
        {
            Log("Cannot enable sync - no active account");
            return;
        }

        Log($"Enabling cloud sync for account {accountId.Value} with priority: {priority}...");

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
                Priority = priority,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            db.SyncStates.Add(state);
        }
        else
        {
            state.IsCloudSyncEnabled = true;
            state.Status = SyncStatus.Idle;
            state.Priority = priority;
            state.UpdatedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        Log($"Cloud sync enabled for account {accountId.Value} with priority: {priority}");

        // Deduplication is now integrated into StartInitialSyncAsync per-mode
        // (CloudFirst runs DeduplicateForCloudFirstAsync, LocalFirst runs DeduplicateForLocalFirstAsync)

        if (!state.InitialSyncCompleted)
        {
            Log($"Starting initial sync with {priority} priority...");
            _ = StartInitialSyncAsync(ct);
        }
        else
        {
            Log("Initial sync already completed, starting timer...");
        }

        StartSyncTimer();
        StartCloudPollingTimer();
    }

    /// <summary>
    /// Runs legacy deduplication on the sync queue. Returns result with success/failure info.
    /// NEVER throws - always returns a result.
    /// </summary>
    private async Task<DeduplicationResult> TryRunDeduplicationAsync(CancellationToken ct)
    {
        try
        {
            var deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
            if (deduplicationService == null)
            {
                return DeduplicationResult.Skipped("DeduplicationService not available");
            }

            Log("Running legacy queue deduplication...");
            
            try
            {
                if (!deduplicationService.IsCachePopulated)
                {
                    await deduplicationService.FetchCloudDataAsync(ct);
                }
            }
            catch (Exception ex)
            {
                return DeduplicationResult.Failed($"Failed to fetch cloud data: {ex.Message}");
            }
            
            try
            {
                var removed = await deduplicationService.DeduplicateSyncQueueAsync(ct);
                return DeduplicationResult.Success(removed);
            }
            catch (Exception ex)
            {
                return DeduplicationResult.Failed($"Deduplication error: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Log($"ERROR in TryRunDeduplicationAsync: {ex.Message}");
            return DeduplicationResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Result of deduplication attempt.
    /// </summary>
    private readonly record struct DeduplicationResult(bool IsSuccess, int RemovedCount, string Message)
    {
        public static DeduplicationResult Success(int removed) => 
            new(true, removed, $"Removed {removed} duplicates");
        public static DeduplicationResult Skipped(string reason) => 
            new(true, 0, $"Skipped: {reason}");
        public static DeduplicationResult Failed(string error) => 
            new(false, 0, $"Failed: {error}");
    }

    public async Task DisableCloudSyncAsync(CancellationToken ct = default)
    {
        StopSyncTimer();
        StopCloudPollingTimer();

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

        Log($"Cloud sync disabled for account {accountId.Value}");
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
        {
            Log("StartInitialSyncAsync: No active account");
            return false;
        }

        Log($"StartInitialSyncAsync: Beginning for account {accountId.Value}");

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
        if (state == null)
        {
            Log("StartInitialSyncAsync: Sync state not found, creating...");
            state = new SyncState
            {
                AccountId = accountId.Value,
                IsCloudSyncEnabled = true,
                Status = SyncStatus.InitialSync,
                Priority = SyncPriority.Local, // Default to local priority
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
                InitialSyncStartedUtc = DateTime.UtcNow
            };
            db.SyncStates.Add(state);
            await db.SaveChangesAsync(ct);
        }
        else if (state.InitialSyncCompleted)
        {
            Log("StartInitialSyncAsync: Initial sync already completed");
            return true;
        }
        else
        {
            state.Status = SyncStatus.InitialSync;
            state.InitialSyncStartedUtc = DateTime.UtcNow;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.InitialSync, "Starting initial sync");

        try
        {
            var batchId = Guid.NewGuid();
            int queuedCount = 0;
            
            // Determine sync priority
            var priority = state.Priority;
            Log($"Initial sync priority: {priority}");

            if (priority == SyncPriority.Cloud)
            {
                // CLOUD-FIRST: Download cloud data to local database first
                Log("Cloud-first sync: Fetching ALL data from cloud...");
                RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Syncing, "Downloading data from cloud...");
                
                try
                {
                    var cloudData = await ExecuteWithCrudServiceAsync(crud => crud.FetchAllCloudDataAsync(ct));
                    
                    Log($"Cloud data fetched: " +
                        $"Folders={cloudData.Folders.Count}, " +
                        $"Labels={cloudData.RecipeLabels.Count}, " +
                        $"Recipes={cloudData.Recipes.Count}, " +
                        $"Ingredients={cloudData.Ingredients.Count}, " +
                        $"Plans={cloudData.Plans.Count}, " +
                        $"Meals={cloudData.PlannedMeals.Count}, " +
                        $"ShoppingItems={cloudData.ShoppingListItems.Count}, " +
                        $"UserPreferences={cloudData.UserPreferences != null}");
                    
                    // DEDUPLICATION: Remove local duplicates BEFORE importing cloud data
                    // This prevents duplicate rows when cloud entity matches local entity with different ID
                    try
                    {
                        var deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
                        if (deduplicationService != null)
                        {
                            var mergedCount = await deduplicationService.DeduplicateForCloudFirstAsync(db, cloudData, ct);
                            Log($"Cloud-first deduplication: merged {mergedCount} local entities with cloud");
                        }
                    }
                    catch (Exception dedupEx)
                    {
                        Log($"WARNING: Cloud-first deduplication failed (continuing): {dedupEx.Message}");
                    }
                    
                    // Use FORCE import - cloud always wins
                    var importedCount = await ForceImportCloudDataToLocalAsync(db, cloudData, ct);
                    
                    Log($"Cloud-first sync: Imported {importedCount} entities from cloud");
                    
                    // Apply user preferences from snapshot (no extra API call!)
                    if (cloudData.UserPreferences != null)
                    {
                        try
                        {
                            var applied = ApplyUserPreferencesFromSnapshot(cloudData.UserPreferences);
                            if (applied)
                            {
                                Log("Cloud-first sync: User preferences applied from snapshot");
                            }
                            else
                            {
                                Log("Cloud-first sync: Failed to apply user preferences from snapshot");
                            }
                        }
                        catch (Exception prefEx)
                        {
                            Log($"Cloud-first sync: Failed to apply user preferences: {prefEx.Message}");
                        }
                    }
                    else
                    {
                        Log("Cloud-first sync: No user preferences in cloud snapshot");
                    }
                    
                    // Then queue any local-only entities for upload (entities that exist locally but not in cloud)
                    queuedCount = await QueueLocalOnlyEntitiesForSyncAsync(db, accountId.Value, batchId, cloudData, ct);
                    Log($"Cloud-first sync: Queued {queuedCount} local-only entities for upload");
                }
                catch (Exception cloudEx)
                {
                    Log($"ERROR fetching cloud data: {cloudEx.Message}");
                    throw;
                }
            }
            else
            {
                // LOCAL-FIRST: Queue all local entities for upload
                Log("Local-first sync: Queuing local entities for upload...");
                queuedCount = await QueueAllEntitiesForInitialSyncAsync(db, accountId.Value, batchId, ct);
                Log($"Local-first sync: Queued {queuedCount} entities for upload");
                
                // DEDUPLICATION: Before processing the queue, remove cloud duplicates
                // This deletes cloud entities that match local ones, so the upload creates clean entries
                try
                {
                    var deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
                    if (deduplicationService != null)
                    {
                        var dedupCount = await deduplicationService.DeduplicateForLocalFirstAsync(db, ct);
                        Log($"Local-first deduplication: deduplicated {dedupCount} cloud entities");
                    }
                }
                catch (Exception dedupEx)
                {
                    Log($"WARNING: Local-first deduplication failed (continuing): {dedupEx.Message}");
                }
            }

            state.InitialSyncCompleted = true;
            state.InitialSyncCompletedUtc = DateTime.UtcNow;
            state.Status = SyncStatus.Idle;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            RaiseSyncStatusChanged(SyncStatus.InitialSync, SyncStatus.Idle, $"Initial sync queued {queuedCount} items");

            // Start processing the queue
            Log("StartInitialSyncAsync: Starting queue processing...");
            _ = ProcessQueueAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            Log($"ERROR in StartInitialSyncAsync: {ex.Message}");
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

            Log($"Processing queue: {totalPending} items pending, taking batch of {pending.Count}");

            if (pending.Count == 0)
            {
                state.Status = SyncStatus.Idle;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                sw.Stop();
                RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, "Queue empty");
                
                // CRITICAL FIX: Restart timer when queue becomes empty
                // This ensures the timer doesn't hang after processing batches
                RestartSyncTimerAsync();
                
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

            // After processing the batch, check if there are more pending items
            var remainingPending = await GetPendingCountAsync(ct);
            if (processed > 0 && remainingPending > 0)
            {
                var delayMs = _random.Next(MinDelaySeconds * 1000, (MaxDelaySeconds + 1) * 1000);
                Log($"Batch processed ({processed}/{totalPending}) - scheduling next batch in {delayMs / 1000}s...");
                
                // CRITICAL FIX: Schedule next batch instead of waiting passively
                // This ensures continuous processing while respecting rate limits
                _ = ScheduleNextBatchAsync(delayMs, ct);
            }
            else if (remainingPending == 0)
            {
                Log($"All items synced - restarting timer for future items");
                RestartSyncTimerAsync();
            }

            state.LastSyncUtc = DateTime.UtcNow;
            state.TotalItemsSynced += processed;
            state.PendingItemsCount = remainingPending;
            state.Status = SyncStatus.Idle;
            state.LastSyncError = failed > 0 ? $"{failed} items failed" : null;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            sw.Stop();
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, $"Synced {processed} items" + (failed > 0 ? $", {failed} failed" : ""));
            return SyncResult.Ok(processed, remainingPending, sw.Elapsed);
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
    /// If Cloud priority is set, first downloads all cloud data.
    /// </summary>
    public async Task<SyncResult> ForceSyncAllAsync(CancellationToken ct = default)
    {
        // First, check if we should download from cloud (Cloud-First priority)
        var preAccountId = await _tokenStore.GetActiveAccountIdAsync();
        if (preAccountId.HasValue)
        {
            using var preScope = _serviceProvider.CreateScope();
            var preDb = preScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var preState = await preDb.SyncStates.FirstOrDefaultAsync(s => s.AccountId == preAccountId.Value, ct);
            
            if (preState?.Priority == SyncPriority.Cloud)
            {
                Log("ForceSyncAllAsync: Cloud priority detected - downloading from cloud first...");
                var downloadResult = await ForceDownloadFromCloudAsync(ct);
                if (!downloadResult.Success)
                {
                    Log($"ForceSyncAllAsync: Cloud download failed: {downloadResult.ErrorMessage}");
                    // Continue with queue processing anyway
                }
                else
                {
                    Log($"ForceSyncAllAsync: Downloaded {downloadResult.ItemsProcessed} items from cloud");
                }
            }
        }

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

    /// <summary>
    /// Forces immediate download of all cloud data to local database.
    /// Designed for Cloud-First priority - imports all cloud data, overwriting local if timestamps differ.
    /// </summary>
    public async Task<SyncResult> ForceDownloadFromCloudAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.FromSeconds(10), ct))
        {
            return SyncResult.Error("Could not acquire sync lock");
        }

        var sw = Stopwatch.StartNew();
        int imported = 0;

        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
                return SyncResult.Error("No active account");

            if (!await IsCloudSyncEnabledAsync(ct))
                return SyncResult.Error("Sync not enabled");

            Log("=== ForceDownloadFromCloudAsync START ===");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value, ct);
            if (state != null)
            {
                state.Status = SyncStatus.Syncing;
                state.LastSyncAttemptUtc = DateTime.UtcNow;
                state.UpdatedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            RaiseSyncStatusChanged(SyncStatus.Idle, SyncStatus.Syncing, "Downloading data from cloud...");

            // Fetch ALL cloud data
            Log("Fetching all data from cloud...");
            var cloudData = await ExecuteWithCrudServiceAsync(crud => crud.FetchAllCloudDataAsync(ct));
            
            Log($"Cloud data fetched: " +
                $"Folders={cloudData.Folders.Count}, " +
                $"Labels={cloudData.RecipeLabels.Count}, " +
                $"Recipes={cloudData.Recipes.Count}, " +
                $"Ingredients={cloudData.Ingredients.Count}, " +
                $"Plans={cloudData.Plans.Count}, " +
                $"Meals={cloudData.PlannedMeals.Count}, " +
                $"ShoppingItems={cloudData.ShoppingListItems.Count}, " +
                $"UserPreferences={cloudData.UserPreferences != null}");

            // DEDUPLICATION: Remove local duplicates BEFORE importing cloud data
            // This prevents duplicate rows when cloud entity matches local entity with different ID
            try
            {
                var deduplicationService = _serviceProvider.GetService(typeof(IDeduplicationService)) as IDeduplicationService;
                if (deduplicationService != null)
                {
                    var mergedCount = await deduplicationService.DeduplicateForCloudFirstAsync(db, cloudData, ct);
                    Log($"ForceDownload deduplication: merged {mergedCount} local entities with cloud");
                }
            }
            catch (Exception dedupEx)
            {
                Log($"WARNING: ForceDownload deduplication failed (continuing): {dedupEx.Message}");
            }

            // Import with FORCE mode - always import, don't compare timestamps
            imported = await ForceImportCloudDataToLocalAsync(db, cloudData, ct);

            Log($"Imported {imported} entities from cloud");
            
            // Apply user preferences from snapshot (no extra API call!)
            if (cloudData.UserPreferences != null)
            {
                try
                {
                    var applied = ApplyUserPreferencesFromSnapshot(cloudData.UserPreferences);
                    if (applied)
                    {
                        imported++;
                        Log("User preferences applied from snapshot");
                    }
                    else
                    {
                        Log("Failed to apply user preferences from snapshot");
                    }
                }
                catch (Exception prefEx)
                {
                    Log($"Failed to apply user preferences: {prefEx.Message}");
                }
            }
            else
            {
                Log("No user preferences in cloud snapshot");
            }

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
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Idle, $"Downloaded {imported} items from cloud");

            Log($"=== ForceDownloadFromCloudAsync COMPLETE: {imported} items in {sw.Elapsed.TotalSeconds:F1}s ===");
            return SyncResult.Ok(imported, 0, sw.Elapsed);
        }
        catch (Exception ex)
        {
            Log($"ERROR in ForceDownloadFromCloudAsync: {ex.Message}");
            RaiseSyncStatusChanged(SyncStatus.Syncing, SyncStatus.Error, ex.Message);
            return SyncResult.Error(ex.Message);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Force imports cloud data to local database - always adds/updates regardless of timestamps.
    /// For Cloud-First priority where cloud data should always win.
    /// NOTE: User preferences are NOT applied here - they are applied by the calling method 
    /// (StartInitialSyncAsync or ForceDownloadFromCloudAsync) after this method returns.
    /// </summary>
    private async Task<int> ForceImportCloudDataToLocalAsync(AppDbContext db, CloudDataSnapshot cloudData, CancellationToken ct)
    {
        int imported = 0;
        
        try
        {
            Log("=== ForceImportCloudDataToLocalAsync START ===");
            
            // Import in order respecting FK constraints: Folders › Labels › Recipes › Ingredients › Plans › PlannedMeals › ShoppingItems
            
            // Folders - FORCE import
            Log($"Importing {cloudData.Folders.Count} folders...");
            foreach (var folder in cloudData.Folders)
            {
                var existing = await db.Folders.FindAsync(new object[] { folder.Id }, ct);
                if (existing == null)
                {
                    db.Folders.Add(folder);
                    imported++;
                    Log($"  + Added folder: {folder.Name}");
                }
                else
                {
                    // Cloud wins - always update
                    existing.Name = folder.Name;
                    existing.Description = folder.Description;
                    existing.ParentFolderId = folder.ParentFolderId;
                    existing.Order = folder.Order;
                    existing.CreatedAt = folder.CreatedAt;
                    existing.UpdatedAt = folder.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                    Log($"  ~ Updated folder: {folder.Name}");
                }
            }
            await db.SaveChangesAsync(ct);
            
            // Recipe Labels - FORCE import
            Log($"Importing {cloudData.RecipeLabels.Count} labels...");
            foreach (var label in cloudData.RecipeLabels)
            {
                var existing = await db.RecipeLabels.FindAsync(new object[] { label.Id }, ct);
                if (existing == null)
                {
                    db.RecipeLabels.Add(label);
                    imported++;
                    Log($"  + Added label: {label.Name}");
                }
                else
                {
                    existing.Name = label.Name;
                    existing.ColorHex = label.ColorHex;
                    existing.CreatedAt = label.CreatedAt;
                    existing.UpdatedAt = label.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                    Log($"  ~ Updated label: {label.Name}");
                }
            }
            await db.SaveChangesAsync(ct);
            
            // Recipes - FORCE import (without ingredients - they come separately)
            Log($"Importing {cloudData.Recipes.Count} recipes...");
            foreach (var recipe in cloudData.Recipes)
            {
                var existing = await db.Recipes.FindAsync(new object[] { recipe.Id }, ct);
                if (existing == null)
                {
                    // Detach ingredients - they will be imported separately
                    var recipeToAdd = new Recipe
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
                    };
                    db.Recipes.Add(recipeToAdd);
                    imported++;
                    Log($"  + Added recipe: {recipe.Name}");
                }
                else
                {
                    existing.Name = recipe.Name;
                    existing.Description = recipe.Description;
                    existing.Calories = recipe.Calories;
                    existing.Protein = recipe.Protein;
                    existing.Fat = recipe.Fat;
                    existing.Carbs = recipe.Carbs;
                    existing.IloscPorcji = recipe.IloscPorcji;
                    existing.FolderId = recipe.FolderId;
                    existing.CreatedAt = recipe.CreatedAt;
                    existing.UpdatedAt = recipe.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                    Log($"  ~ Updated recipe: {recipe.Name}");
                }
            }
            await db.SaveChangesAsync(ct);
            
            // Ingredients - FORCE import
            Log($"Importing {cloudData.Ingredients.Count} ingredients...");
            foreach (var ingredient in cloudData.Ingredients)
            {
                var existing = await db.Ingredients.FindAsync(new object[] { ingredient.Id }, ct);
                if (existing == null)
                {
                    var ingredientToAdd = new Ingredient
                    {
                        Id = ingredient.Id,
                        Name = ingredient.Name,
                        Quantity = ingredient.Quantity,
                        Unit = ingredient.Unit,
                        UnitWeight = ingredient.UnitWeight,
                        Calories = ingredient.Calories,
                        Protein = ingredient.Protein,
                        Fat = ingredient.Fat,
                        Carbs = ingredient.Carbs,
                        RecipeId = ingredient.RecipeId,
                        CreatedAt = ingredient.CreatedAt,
                        UpdatedAt = ingredient.UpdatedAt
                    };
                    db.Ingredients.Add(ingredientToAdd);
                    imported++;
                }
                else
                {
                    existing.Name = ingredient.Name;
                    existing.Quantity = ingredient.Quantity;
                    existing.Unit = ingredient.Unit;
                    existing.UnitWeight = ingredient.UnitWeight;
                    existing.Calories = ingredient.Calories;
                    existing.Protein = ingredient.Protein;
                    existing.Fat = ingredient.Fat;
                    existing.Carbs = ingredient.Carbs;
                    existing.RecipeId = ingredient.RecipeId;
                    existing.CreatedAt = ingredient.CreatedAt;
                    existing.UpdatedAt = ingredient.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                }
            }
            await db.SaveChangesAsync(ct);
            Log($"  Imported {cloudData.Ingredients.Count} ingredients");
            
            // Plans - FORCE import
            Log($"Importing {cloudData.Plans.Count} plans...");
            foreach (var plan in cloudData.Plans)
            {
                var existing = await db.Plans.FindAsync(new object[] { plan.Id }, ct);
                if (existing == null)
                {
                    db.Plans.Add(plan);
                    imported++;
                    Log($"  + Added plan: {plan.Title ?? plan.Id.ToString()}");
                }
                else
                {
                    existing.StartDate = plan.StartDate;
                    existing.EndDate = plan.EndDate;
                    existing.IsArchived = plan.IsArchived;
                    existing.Type = plan.Type;
                    existing.Title = plan.Title;
                    existing.LinkedShoppingListPlanId = plan.LinkedShoppingListPlanId;
                    existing.CreatedAt = plan.CreatedAt;
                    existing.UpdatedAt = plan.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                    Log($"  ~ Updated plan: {plan.Title ?? plan.Id.ToString()}");
                }
            }
            await db.SaveChangesAsync(ct);
            
            // Planned Meals - FORCE import
            Log($"Importing {cloudData.PlannedMeals.Count} planned meals...");
            foreach (var meal in cloudData.PlannedMeals)
            {
                var existing = await db.PlannedMeals.FindAsync(new object[] { meal.Id }, ct);
                if (existing == null)
                {
                    db.PlannedMeals.Add(meal);
                    imported++;
                }
                else
                {
                    existing.RecipeId = meal.RecipeId;
                    existing.PlanId = meal.PlanId;
                    existing.Date = meal.Date;
                    existing.Portions = meal.Portions;
                    existing.CreatedAt = meal.CreatedAt;
                    existing.UpdatedAt = meal.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                }
            }
            await db.SaveChangesAsync(ct);
            
            // Shopping List Items - FORCE import
            Log($"Importing {cloudData.ShoppingListItems.Count} shopping items...");
            foreach (var item in cloudData.ShoppingListItems)
            {
                var existing = await db.ShoppingListItems.FindAsync(new object[] { item.Id }, ct);
                if (existing == null)
                {
                    db.ShoppingListItems.Add(item);
                    imported++;
                }
                else
                {
                    existing.PlanId = item.PlanId;
                    existing.IngredientName = item.IngredientName;
                    existing.Quantity = item.Quantity;
                    existing.Unit = item.Unit;
                    existing.IsChecked = item.IsChecked;
                    existing.Order = item.Order;
                    existing.CreatedAt = item.CreatedAt;
                    existing.UpdatedAt = item.UpdatedAt ?? DateTime.UtcNow;
                    imported++;
                }
            }
            await db.SaveChangesAsync(ct);
            
            // NOTE: User preferences are applied by calling method, not here
            // This avoids duplicate API calls since preferences are already in CloudDataSnapshot
            if (cloudData.UserPreferences != null)
            {
                Log($"  UserPreferences present in snapshot (will be applied by caller)");
            }
            
            Log($"=== ForceImportCloudDataToLocalAsync COMPLETE: {imported} entities imported ===");
        }
        catch (Exception ex)
        {
            Log($"ERROR in ForceImportCloudDataToLocalAsync: {ex.Message}");
            if (ex.InnerException != null)
                Log($"  Inner: {ex.InnerException.Message}");
            throw;
        }
        
        return imported;
    }
    
    /// <summary>
    /// Polls cloud for changes and imports them to local database.
    /// Creates sync queue entries to track imported changes.
    /// </summary>
    private async Task PollCloudForChangesAsync()
    {
        try
        {
            var accountId = await _tokenStore.GetActiveAccountIdAsync();
            if (!accountId.HasValue)
            {
                Log("PollCloudForChangesAsync: No active account");
                return;
            }

            if (!await IsCloudSyncEnabledAsync())
            {
                Log("PollCloudForChangesAsync: Sync not enabled");
                return;
            }

            Log("PollCloudForChangesAsync: Starting cloud poll...");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var state = await db.SyncStates.FirstOrDefaultAsync(s => s.AccountId == accountId.Value);
            if (state == null)
            {
                Log("PollCloudForChangesAsync: Sync state not found");
                return;
            }

            // Fetch current cloud data
            var cloudData = await ExecuteWithCrudServiceAsync(crud => crud.FetchAllCloudDataAsync());
            
            // Import changes to local DB (only newer/missing items)
            var importedCount = await ImportCloudDataToLocalAsync(db, cloudData, default);
            
            // Update last poll time
            state.LastCloudPollUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            Log($"PollCloudForChangesAsync: Completed. Imported {importedCount} changes from cloud");
        }
        catch (Exception ex)
        {
            Log($"ERROR in PollCloudForChangesAsync: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Imports cloud data to local database. Returns count of imported entities.
    /// </summary>
    private async Task<int> ImportCloudDataToLocalAsync(AppDbContext db, CloudDataSnapshot cloudData, CancellationToken ct)
    {
        int imported = 0;
        
        try
        {
            // Import in order respecting FK constraints: Folders ? Labels ? Recipes ? Ingredients ? Plans ? PlannedMeals ? ShoppingItems
            
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
            
            // Recipes
            foreach (var recipe in cloudData.Recipes)
            {
                var existing = await db.Recipes.FindAsync(new object[] { recipe.Id }, ct);
                if (existing == null)
                {
                    db.Recipes.Add(recipe);
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
            foreach (var ingredient in cloudData.Ingredients)
            {
                var existing = await db.Ingredients.FindAsync(new object[] { ingredient.Id }, ct);
                if (existing == null)
                {
                    db.Ingredients.Add(ingredient);
                    imported++;
                }
                else if (ingredient.UpdatedAt > existing.UpdatedAt)
                {
                    existing.Name = ingredient.Name;
                    existing.Quantity = ingredient.Quantity;
                    existing.Unit = ingredient.Unit;
                    existing.UnitWeight = ingredient.UnitWeight;
                    existing.Calories = ingredient.Calories;
                    existing.Protein = ingredient.Protein;
                    existing.Fat = ingredient.Fat;
                    existing.Carbs = ingredient.Carbs;
                    existing.RecipeId = ingredient.RecipeId;
                    existing.UpdatedAt = ingredient.UpdatedAt;
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
                    existing.UpdatedAt = plan.UpdatedAt;
                    imported++;
                }
            }
            await db.SaveChangesAsync(ct);
            
            // Planned Meals
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
            
            // Shopping List Items
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
            
            Log($"=== ForceImportCloudDataToLocalAsync COMPLETE: {imported} entities imported ===");
        }
        catch (Exception ex)
        {
            Log($"ERROR in ForceImportCloudDataToLocalAsync: {ex.Message}");
            if (ex.InnerException != null)
                Log($"  Inner: {ex.InnerException.Message}");
            throw;
        }
        
        return imported;
    }
    
    /// <summary>
    /// Queues only local entities that don't exist in cloud for upload (used in cloud-first sync).
    /// </summary>
    private async Task<int> QueueLocalOnlyEntitiesForSyncAsync(AppDbContext db, Guid accountId, Guid batchId, CloudDataSnapshot cloudData, CancellationToken ct)
    {
        int totalQueued = 0;
        
        try
        {
            // Build sets of cloud IDs for quick lookup
            var cloudFolderIds = new HashSet<Guid>(cloudData.Folders.Select(f => f.Id));
            var cloudLabelIds = new HashSet<Guid>(cloudData.RecipeLabels.Select(l => l.Id));
            var cloudRecipeIds = new HashSet<Guid>(cloudData.Recipes.Select(r => r.Id));
            var cloudIngredientIds = new HashSet<Guid>(cloudData.Ingredients.Select(i => i.Id));
            var cloudPlanIds = new HashSet<Guid>(cloudData.Plans.Select(p => p.Id));
            var cloudMealIds = new HashSet<Guid>(cloudData.PlannedMeals.Select(m => m.Id));
            var cloudShoppingIds = new HashSet<Guid>(cloudData.ShoppingListItems.Select(s => s.Id));
            
            // Queue local-only folders
            var localFolders = await db.Folders.ToListAsync(ct);
            foreach (var folder in localFolders.Where(f => !cloudFolderIds.Contains(f.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, folder, batchId, priority: 1));
                totalQueued++;
            }
            
            // Queue local-only labels
            var localLabels = await db.RecipeLabels.ToListAsync(ct);
            foreach (var label in localLabels.Where(l => !cloudLabelIds.Contains(l.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, label, batchId, priority: 2));
                totalQueued++;
            }
            
            // Queue local-only recipes
            var localRecipes = await db.Recipes.ToListAsync(ct);
            foreach (var recipe in localRecipes.Where(r => !cloudRecipeIds.Contains(r.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, recipe, batchId, priority: 3));
                totalQueued++;
            }
            
            // Queue local-only ingredients
            var localIngredients = await db.Ingredients.ToListAsync(ct);
            foreach (var ingredient in localIngredients.Where(i => !cloudIngredientIds.Contains(i.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, ingredient, batchId, priority: 4));
                totalQueued++;
            }
            
            // Queue local-only plans
            var localPlans = await db.Plans.ToListAsync(ct);
            foreach (var plan in localPlans.Where(p => !cloudPlanIds.Contains(p.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, plan, batchId, priority: 5));
                totalQueued++;
            }
            
            // Queue local-only planned meals
            var localMeals = await db.PlannedMeals.ToListAsync(ct);
            foreach (var meal in localMeals.Where(m => !cloudMealIds.Contains(m.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, meal, batchId, priority: 6));
                totalQueued++;
            }
            
            // Queue local-only shopping items
            var localShoppingItems = await db.ShoppingListItems.ToListAsync(ct);
            foreach (var item in localShoppingItems.Where(s => !cloudShoppingIds.Contains(s.Id)))
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, item, batchId, priority: 7));
                totalQueued++;
            }
            
            await db.SaveChangesAsync(ct);
            Log($"QueueLocalOnlyEntitiesForSyncAsync: Queued {totalQueued} local-only entities");
        }
        catch (Exception ex)
        {
            Log($"ERROR in QueueLocalOnlyEntitiesForSyncAsync: {ex.Message}");
            throw;
        }
        
        return totalQueued;
    }
    
    private async Task<int> QueueAllEntitiesForInitialSyncAsync(AppDbContext db, Guid accountId, Guid batchId, CancellationToken ct)
    {
        int totalQueued = 0;

        try
        {
            var folders = await db.Folders.ToListAsync(ct);
            foreach (var folder in folders)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, folder, batchId, priority: 1));
                totalQueued++;
            }
            Log($"Queued {folders.Count} folders");

            var labels = await db.RecipeLabels.ToListAsync(ct);
            foreach (var label in labels)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, label, batchId, priority: 2));
                totalQueued++;
            }
            Log($"Queued {labels.Count} recipe labels");

            var recipes = await db.Recipes.ToListAsync(ct);
            foreach (var recipe in recipes)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, recipe, batchId, priority: 3));
                totalQueued++;
            }
            Log($"Queued {recipes.Count} recipes");

            var ingredients = await db.Ingredients.ToListAsync(ct);
            foreach (var ingredient in ingredients)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, ingredient, batchId, priority: 4));
                totalQueued++;
            }
            Log($"Queued {ingredients.Count} ingredients");

            var plans = await db.Plans.ToListAsync(ct);
            foreach (var plan in plans)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, plan, batchId, priority: 5));
                totalQueued++;
            }
            Log($"Queued {plans.Count} plans");

            var plannedMeals = await db.PlannedMeals.ToListAsync(ct);
            foreach (var meal in plannedMeals)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, meal, batchId, priority: 6));
                totalQueued++;
            }
            Log($"Queued {plannedMeals.Count} planned meals");

            var shoppingItems = await db.ShoppingListItems.ToListAsync(ct);
            foreach (var item in shoppingItems)
            {
                db.SyncQueue.Add(CreateInitialSyncEntry(accountId, item, batchId, priority: 7));
                totalQueued++;
            }
            Log($"Queued {shoppingItems.Count} shopping list items");

            await db.SaveChangesAsync(ct);
            Log($"Initial sync queue saved: {totalQueued} total entities");
        }
        catch (Exception ex)
        {
            Log($"ERROR in QueueAllEntitiesForInitialSyncAsync: {ex.Message}");
            throw;
        }

        return totalQueued;
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
                case "UserPreferencesDto":
                    await ProcessUserPreferencesAsync(entry, ct);
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddFolderAsync(folder, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdateFolderAsync(folder, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeleteFolderAsync(entry.EntityId, ct));
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddRecipeAsync(recipe, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdateRecipeAsync(recipe, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeleteRecipeAsync(entry.EntityId, ct));
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddIngredientAsync(ingredient, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdateIngredientAsync(ingredient, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeleteIngredientAsync(entry.EntityId, ct));
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddRecipeLabelAsync(label, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdateRecipeLabelAsync(label, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeleteRecipeLabelAsync(entry.EntityId, ct));
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddPlanAsync(plan, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdatePlanAsync(plan, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeletePlanAsync(entry.EntityId, ct));
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddPlannedMealAsync(meal, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdatePlannedMealAsync(meal, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeletePlannedMealAsync(entry.EntityId, ct));
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
                    await ExecuteWithCrudServiceAsync(crud => crud.AddShoppingListItemAsync(item, ct));
                else
                    await ExecuteWithCrudServiceAsync(crud => crud.UpdateShoppingListItemAsync(item, ct));
                break;
            case SyncOperationType.Delete:
                await ExecuteWithCrudServiceAsync(crud => crud.DeleteShoppingListItemAsync(entry.EntityId, ct));
                break;
        }
    }

    private async Task ProcessUserPreferencesAsync(SyncQueueEntry entry, CancellationToken ct)
    {
        switch (entry.OperationType)
        {
            case SyncOperationType.Insert:
            case SyncOperationType.Update:
                var prefs = DeserializeEntity<UserPreferencesDto>(entry.Payload!);
                await ExecuteWithCrudServiceAsync(crud => crud.UpsertUserPreferencesAsync(prefs, ct));
                break;
            case SyncOperationType.Delete:
                // Preferences are user-specific; soft delete by clearing data
                var emptyPrefs = new UserPreferencesDto { Id = entry.EntityId };
                await ExecuteWithCrudServiceAsync(crud => crud.UpsertUserPreferencesAsync(emptyPrefs, ct));
                break;
        }
    }

    private static Guid GetEntityId<T>(T entity) where T : class
    {
        var idProp = typeof(T).GetProperty("Id") ?? throw new InvalidOperationException($"{typeof(T).Name} has no Id");
        var value = idProp.GetValue(entity);
        return value is Guid g ? g : throw new InvalidOperationException($"{typeof(T).Name}.Id is not Guid");
    }

    private static string SerializeEntity<T>(T entity)
    {
        // Special-case UserPreferencesDto to avoid BaseModel internals (PrimaryKey dictionary) being serialized
        if (entity is Foodbook.Models.DTOs.UserPreferencesDto prefs)
        {
            var payload = new
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
            };

            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        return JsonSerializer.Serialize(entity, JsonOptions);
    }

    private static string StripMetadataFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return json;

            using var ms = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var name = prop.Name;
                    if (string.Equals(name, "PrimaryKey", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "TableName", StringComparison.OrdinalIgnoreCase))
                    {
                        // skip metadata fields entirely
                        continue;
                    }

                    prop.WriteTo(writer);
                }
                writer.WriteEndObject();
                writer.Flush();
            }

            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            // If anything fails while cleaning, fall back to original JSON to avoid data loss
            return json;
        }
    }

    private static T DeserializeEntity<T>(string json)
    {
        // Clean known problematic metadata that may have been serialized from Supabase BaseModel
        if (!string.IsNullOrEmpty(json) && (json.IndexOf("\"PrimaryKey\"", StringComparison.OrdinalIgnoreCase) >= 0 || json.IndexOf("\"TableName\"", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            json = StripMetadataFromJson(json);
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");
    }

    private void RaiseSyncStatusChanged(SyncStatus oldStatus, SyncStatus newStatus, string? message = null)
    {
        SyncStatusChanged?.Invoke(this, new SyncStatusChangedEventArgs { OldStatus = oldStatus, NewStatus = newStatus, Message = message });
    }

    private void RaiseSyncProgress(int total, int processed, int failed, string? entityType = null)
    {
        SyncProgressChanged?.Invoke(this, new SyncProgressEventArgs { TotalItems = total, ProcessedItems = processed, FailedItems = failed, CurrentEntityType = entityType });
    }

    private static void Log(string message) => System.Diagnostics.Debug.WriteLine($"[SupabaseSyncService] {message}");

    #region User Preferences Sync

    /// <summary>
    /// Applies user preferences from a UserPreferencesDto directly (no API call).
    /// Use this when preferences are already fetched as part of CloudDataSnapshot.
    /// </summary>
    public bool ApplyUserPreferencesFromSnapshot(UserPreferencesDto cloudPrefs)
    {
        try
        {
            Log($"Applying preferences from snapshot...");

            if (cloudPrefs == null)
            {
                Log("No preferences in snapshot");
                return false;
            }

            // CRITICAL: Suppress cloud sync during load to prevent circular calls
            _preferencesService.SuppressCloudSync();

            try
            {
                // Apply cloud preferences to local storage
                
                if (!string.IsNullOrWhiteSpace(cloudPrefs.Language))
                {
                    _preferencesService.SaveLanguage(cloudPrefs.Language);
                    Log($"Applied language: {cloudPrefs.Language}");
                }

                if (Enum.TryParse<Foodbook.Models.AppTheme>(cloudPrefs.Theme, out var theme))
                {
                    _preferencesService.SaveTheme(theme);
                    _themeService.SetTheme(theme);
                    Log($"Applied theme: {theme}");
                }

                if (Enum.TryParse<AppColorTheme>(cloudPrefs.ColorTheme, out var colorTheme))
                {
                    _preferencesService.SaveColorTheme(colorTheme);
                    _themeService.SetColorTheme(colorTheme);
                    Log($"Applied color theme: {colorTheme}");
                }

                _preferencesService.SaveColorfulBackground(cloudPrefs.IsColorfulBackground);
                _preferencesService.SaveWallpaperEnabled(cloudPrefs.IsWallpaperEnabled);

                // Apply font settings using IFontService
                if (Enum.TryParse<AppFontFamily>(cloudPrefs.FontFamily, out var fontFamily))
                {
                    _preferencesService.SaveFontFamily(fontFamily);
                    
                    var fontService = _serviceProvider.GetService(typeof(IFontService)) as IFontService;
                    if (fontService != null)
                    {
                        fontService.SetFontFamily(fontFamily);
                        Log($"Applied font family: {fontFamily}");
                    }
                }

                if (Enum.TryParse<AppFontSize>(cloudPrefs.FontSize, out var fontSize))
                {
                    _preferencesService.SaveFontSize(fontSize);
                    
                    var fontService = _serviceProvider.GetService(typeof(IFontService)) as IFontService;
                    if (fontService != null)
                    {
                        fontService.SetFontSize(fontSize);
                        Log($"Applied font size: {fontSize}");
                    }
                }

                Log("? Successfully applied preferences from snapshot");
                return true;
            }
            finally
            {
                // Re-enable cloud sync
                _preferencesService.ResumeCloudSync();
            }
        }
        catch (Exception ex)
        {
            Log($"? Error applying preferences from snapshot: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LoadUserPreferencesFromCloudAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            Log($"Loading preferences from cloud for user {userId}");

            var cloudPrefs = await ExecuteWithCrudServiceAsync(crud => crud.GetUserPreferencesAsync(userId, ct));
            
            if (cloudPrefs == null)
            {
                Log("No cloud preferences found - first login");
                return false;
            }

            // CRITICAL: Suppress cloud sync during load to prevent circular calls
            _preferencesService.SuppressCloudSync();

            try
            {
                // Apply cloud preferences to local storage
                
                if (!string.IsNullOrWhiteSpace(cloudPrefs.Language))
                {
                    _preferencesService.SaveLanguage(cloudPrefs.Language);
                    Log($"Applied language: {cloudPrefs.Language}");
                }

                if (Enum.TryParse<Foodbook.Models.AppTheme>(cloudPrefs.Theme, out var theme))
                {
                    _preferencesService.SaveTheme(theme);
                    _themeService.SetTheme(theme);
                    Log($"Applied theme: {theme}");
                }

                if (Enum.TryParse<AppColorTheme>(cloudPrefs.ColorTheme, out var colorTheme))
                {
                    _preferencesService.SaveColorTheme(colorTheme);
                    _themeService.SetColorTheme(colorTheme);
                    Log($"Applied color theme: {colorTheme}");
                }

                _preferencesService.SaveColorfulBackground(cloudPrefs.IsColorfulBackground);
                _preferencesService.SaveWallpaperEnabled(cloudPrefs.IsWallpaperEnabled);

                // FIX: Apply font settings using IFontService (not just save to preferences)
                if (Enum.TryParse<AppFontFamily>(cloudPrefs.FontFamily, out var fontFamily))
                {
                    _preferencesService.SaveFontFamily(fontFamily);
                    
                    // Get FontService from service provider and apply font
                    var fontService = _serviceProvider.GetService(typeof(IFontService)) as IFontService;
                    if (fontService != null)
                    {
                        fontService.SetFontFamily(fontFamily);
                        Log($"Applied font family: {fontFamily}");
                    }
                    else
                    {
                        Log($"WARNING: FontService not available - font family saved but not applied");
                    }
                }

                if (Enum.TryParse<AppFontSize>(cloudPrefs.FontSize, out var fontSize))
                {
                    _preferencesService.SaveFontSize(fontSize);
                    
                    // Get FontService from service provider and apply font size
                    var fontService = _serviceProvider.GetService(typeof(IFontService)) as IFontService;
                    if (fontService != null)
                    {
                        fontService.SetFontSize(fontSize);
                        Log($"Applied font size: {fontSize}");
                    }
                    else
                    {
                        Log($"WARNING: FontService not available - font size saved but not applied");
                    }
                }

                Log("? Successfully loaded and applied preferences from cloud");
                return true;
            }
            finally
            {
                // Re-enable cloud sync
                _preferencesService.ResumeCloudSync();
            }
        }
        catch (Exception ex)
        {
            Log($"? Error loading preferences from cloud: {ex.Message}");
            return false;
        }
    }

    public async Task SaveUserPreferencesToCloudAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            Log($"Saving preferences to cloud for user {userId}");

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

            // Use the same queue-based flow as other entities (Folder/Recipe/Ingredient/etc.)
            // so UserPreferences follows identical sync lifecycle and status handling.
            await QueueForSyncAsync(dto, SyncOperationType.Update, ct);

            Log("? Successfully queued preferences for cloud sync");
        }
        catch (Exception ex)
        {
            Log($"? Error saving preferences to cloud: {ex.Message}");
        }
    }

    public async Task CreateInitialUserPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            Log($"Creating initial preferences for user {userId}");

            var existing = await ExecuteWithCrudServiceAsync(crud => crud.GetUserPreferencesAsync(userId, ct));
            if (existing != null)
            {
                Log("Preferences already exist - skipping creation");
                return;
            }

            await SaveUserPreferencesToCloudAsync(userId, ct);
            Log("? Successfully created initial preferences");
        }
        catch (Exception ex)
        {
            Log($"? Error creating initial preferences: {ex.Message}");
        }
    }

    public async Task<bool> HasCloudPreferencesAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var prefs = await ExecuteWithCrudServiceAsync(crud => crud.GetUserPreferencesAsync(userId, ct));
            return prefs != null;
        }
        catch (Exception ex)
        {
            Log($"? Error checking cloud preferences: {ex.Message}");
            return false;
        }
    }

    #endregion

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
        _syncLoopCts = new CancellationTokenSource();
        _syncLoopTask = SyncLoopAsync(_syncLoopCts.Token);
    }

    public void StopSyncTimer()
    {
        _syncLoopCts?.Cancel();
        _syncLoopCts?.Dispose();
        _syncLoopCts = null;
        // Don't await _syncLoopTask here — it will complete on its own after cancellation.
        _syncLoopTask = null;
    }

    /// <summary>
    /// Single async loop that replaces Timer + async void callback.
    /// PeriodicTimer guarantees no overlapping ticks — the next tick only fires
    /// after the previous await completes, eliminating data races from K2.
    /// </summary>
    private async Task SyncLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(DefaultSyncInterval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await ProcessQueueAsync(ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log($"Error in sync loop iteration: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log("Sync loop cancelled");
        }
    }

    /// <summary>
    /// CRITICAL FIX: Restart sync timer when queue becomes empty or to continue processing batches
    /// This prevents the timer from hanging after 3-4 batches
    /// </summary>
    private void RestartSyncTimerAsync()
    {
        try
        {
            StartSyncTimer();
            Log("Sync timer restarted");
        }
        catch (Exception ex)
        {
            Log($"Error restarting sync timer: {ex.Message}");
        }
    }

    /// <summary>
    /// Schedule next batch instead of waiting passively for timer.
    /// Ensures continuous processing while respecting rate limits.
    /// </summary>
    private async Task ScheduleNextBatchAsync(int delayMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct);
            Log($"Scheduled batch timer firing after {delayMs}ms delay");
            await ProcessQueueAsync(ct);
        }
        catch (OperationCanceledException)
        {
            Log("Scheduled batch cancelled");
        }
        catch (Exception ex)
        {
            Log($"Error scheduling next batch: {ex.Message}");
        }
    }

    private CancellationTokenSource? _cloudPollingCts;
    private Task? _cloudPollingTask;
    private static readonly TimeSpan CloudPollingInterval = TimeSpan.FromMinutes(10);
    
    private void StartCloudPollingTimer()
    {
        StopCloudPollingTimer();
        _cloudPollingCts = new CancellationTokenSource();
        _cloudPollingTask = CloudPollingLoopAsync(_cloudPollingCts.Token);
        Log("Cloud polling timer started");
    }
    
    private void StopCloudPollingTimer()
    {
        _cloudPollingCts?.Cancel();
        _cloudPollingCts?.Dispose();
        _cloudPollingCts = null;
        _cloudPollingTask = null;
    }

    /// <summary>
    /// Single async loop for cloud polling that replaces Timer + async void callback.
    /// Prevents overlapping poll executions (K2 fix).
    /// </summary>
    private async Task CloudPollingLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(CloudPollingInterval);
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await PollCloudForChangesAsync();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Log($"Error in cloud polling loop iteration: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log("Cloud polling loop cancelled");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _syncLoopCts?.Cancel();
        _syncLoopCts?.Dispose();
        _cloudPollingCts?.Cancel();
        _cloudPollingCts?.Dispose();
        _syncLock.Dispose();
        _queueLock.Dispose();
        _disposed = true;
    }
}
