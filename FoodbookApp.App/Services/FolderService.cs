using Foodbook.Data;
using Foodbook.Models;
using Microsoft.EntityFrameworkCore;
using FoodbookApp.Interfaces;

namespace Foodbook.Services
{
    public interface IFolderService
    {
        Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default);
        Task<List<Folder>> GetFolderHierarchyAsync(CancellationToken ct = default);
        Task<Folder?> GetFolderByIdAsync(Guid id, CancellationToken ct = default);
        Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default);
        Task UpdateFolderAsync(Folder folder, CancellationToken ct = default);
        Task DeleteFolderAsync(Guid id, CancellationToken ct = default);
        Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentFolderId, CancellationToken ct = default);
        Task<bool> MoveRecipeToFolderAsync(Guid recipeId, Guid? targetFolderId, CancellationToken ct = default);
        Task<bool> IsValidFolderMoveAsync(Guid folderId, Guid? newParentFolderId, CancellationToken ct = default);
        Task<bool> ReorderFolderAsync(Guid folderId, Guid? parentFolderId, int newIndex, CancellationToken ct = default);
    }

    public class FolderService : IFolderService
    {
        private readonly AppDbContext _db;
        private readonly ISupabaseSyncService? _syncService;

        public FolderService(AppDbContext db, IServiceProvider serviceProvider)
        {
            _db = db;
            
            try
            {
                _syncService = serviceProvider.GetService(typeof(ISupabaseSyncService)) as ISupabaseSyncService;
            }
            catch
            {
                _syncService = null;
            }
        }

        public async Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default)
        {
            try
            {
                return await _db.Folders
                    .AsNoTracking()
                    .Include(f => f.SubFolders)
                    .Include(f => f.Recipes)
                    .OrderBy(f => f.ParentFolderId)
                    .ThenBy(f => f.Order)
                    .ThenBy(f => f.Name)
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] GetFoldersAsync failed: {ex.Message}");
                return new List<Folder>(); // Return empty list on error
            }
        }

        public async Task<List<Folder>> GetFolderHierarchyAsync(CancellationToken ct = default)
        {
            try
            {
                var folders = await _db.Folders
                    .AsNoTracking()
                    .Include(f => f.Recipes)
                    .OrderBy(f => f.ParentFolderId)
                    .ThenBy(f => f.Order)
                    .ThenBy(f => f.Name)
                    .ToListAsync(ct);

                return BuildHierarchy(folders);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] GetFolderHierarchyAsync failed: {ex.Message}");
                return new List<Folder>(); // Return empty list on error
            }
        }

        public async Task<Folder?> GetFolderByIdAsync(Guid id, CancellationToken ct = default)
        {
            try
            {
                return await _db.Folders
                    .AsNoTracking()
                    .Include(f => f.SubFolders)
                    .Include(f => f.Recipes)
                    .FirstOrDefaultAsync(f => f.Id == id, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] GetFolderByIdAsync failed: {ex.Message}");
                return null;
            }
        }

        public async Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default)
        {
            // sanitize required fields
            folder.Name = folder.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder.Name))
                throw new ArgumentException("Folder name is required", nameof(folder));

            try
            {
                // Set Order to be last among siblings
                var siblingsMax = await _db.Folders
                    .Where(f => f.ParentFolderId == folder.ParentFolderId)
                    .Select(f => (int?)f.Order)
                    .MaxAsync(ct) ?? -1;
                folder.Order = siblingsMax + 1;

                _db.Folders.Add(folder);
                await _db.SaveChangesAsync(ct);

                if (_syncService != null)
                {
                    try
                    {
                        await _syncService.QueueForSyncAsync(folder, SyncOperationType.Insert, ct);
                        System.Diagnostics.Debug.WriteLine($"[FolderService] Queued folder {folder.Id} for Insert sync");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FolderService] Failed to queue insert sync: {syncEx.Message}");
                    }
                }

                return folder;
            }
            catch (DbUpdateException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] AddFolder failed: {ex.InnerException?.Message ?? ex.Message}");
                throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message, ex);
            }
        }

        public async Task UpdateFolderAsync(Folder folder, CancellationToken ct = default)
        {
            try
            {
                folder.Name = folder.Name?.Trim() ?? string.Empty;
                _db.Folders.Update(folder);
                await _db.SaveChangesAsync(ct);
                
                // Queue for sync (Update)
                if (_syncService != null)
                {
                    try
                    {
                        await _syncService.QueueForSyncAsync(folder, SyncOperationType.Update, ct);
                        System.Diagnostics.Debug.WriteLine($"[FolderService] Queued folder {folder.Id} for Update sync");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FolderService] Failed to queue sync: {syncEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] UpdateFolderAsync failed: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteFolderAsync(Guid id, CancellationToken ct = default)
        {
            try
            {
                var folder = await _db.Folders
                    .Include(f => f.SubFolders)
                    .Include(f => f.Recipes)
                    .FirstOrDefaultAsync(f => f.Id == id, ct);
                if (folder == null) return;

                var parentFolderId = folder.ParentFolderId;
                var recipesToMove = folder.Recipes.ToList();
                var subFoldersToMove = folder.SubFolders.ToList();

                // Move recipes to parent folder (or root)
                foreach (var recipe in recipesToMove)
                {
                    recipe.FolderId = parentFolderId;
                }

                await _db.SaveChangesAsync(ct);
                await QueueRecipesForUpdateAsync(recipesToMove, ct);

                // Ensure no child folders remain attached to this parent to satisfy Restrict behavior
                foreach (var sub in subFoldersToMove)
                {
                    sub.ParentFolderId = parentFolderId;
                }

                await _db.SaveChangesAsync(ct);
                await QueueFolderUpdatesAsync(subFoldersToMove, ct);

                _db.Folders.Remove(folder);
                await _db.SaveChangesAsync(ct);
                await NormalizeSiblingOrderAsync(parentFolderId, ct);
                
                // Queue for sync (Delete)
                if (_syncService != null)
                {
                    try
                    {
                        var deleteEntity = new Folder { Id = id, Name = folder.Name };
                        await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete, ct);
                        System.Diagnostics.Debug.WriteLine($"[FolderService] Queued folder {id} for Delete sync");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FolderService] Failed to queue sync: {syncEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] DeleteFolderAsync failed: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> MoveFolderAsync(Guid folderId, Guid? newParentFolderId, CancellationToken ct = default)
        {
            try
            {
                if (!await IsValidFolderMoveAsync(folderId, newParentFolderId, ct))
                    return false;

                var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == folderId, ct);
                if (folder == null) return false;

                var previousParentFolderId = folder.ParentFolderId;
                folder.ParentFolderId = newParentFolderId;

                // When moving to a new parent, assign order to the end of that parent's list
                var maxOrder = await _db.Folders
                    .Where(f => f.ParentFolderId == newParentFolderId && f.Id != folderId)
                    .Select(f => (int?)f.Order)
                    .MaxAsync(ct) ?? -1;
                folder.Order = maxOrder + 1;

                await _db.SaveChangesAsync(ct);
                await QueueFolderUpdatesAsync(new[] { folder }, ct);
                await NormalizeSiblingOrderAsync(previousParentFolderId, ct);
                await NormalizeSiblingOrderAsync(newParentFolderId, ct);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] MoveFolderAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> MoveRecipeToFolderAsync(Guid recipeId, Guid? targetFolderId, CancellationToken ct = default)
        {
            try
            {
                var recipe = await _db.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId, ct);
                if (recipe == null) return false;

                if (targetFolderId.HasValue)
                {
                    var target = await _db.Folders.AnyAsync(f => f.Id == targetFolderId.Value, ct);
                    if (!target) return false;
                }

                recipe.FolderId = targetFolderId; // null => root
                await _db.SaveChangesAsync(ct);
                await QueueRecipesForUpdateAsync(new[] { recipe }, ct);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] MoveRecipeToFolderAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsValidFolderMoveAsync(Guid folderId, Guid? newParentFolderId, CancellationToken ct = default)
        {
            try
            {
                if (newParentFolderId.HasValue && folderId == newParentFolderId.Value) return false;
                if (!newParentFolderId.HasValue) return true; // move to root allowed

                // prevent cycles: new parent cannot be a descendant of folderId
                var folders = await _db.Folders.AsNoTracking().ToListAsync(ct);
                var map = folders.ToDictionary(f => f.Id, f => f.ParentFolderId);

                Guid? current = newParentFolderId;
                while (current.HasValue)
                {
                    if (current.Value == folderId) return false; // cycle detected
                    map.TryGetValue(current.Value, out current);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] IsValidFolderMoveAsync failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ReorderFolderAsync(Guid folderId, Guid? parentFolderId, int newIndex, CancellationToken ct = default)
        {
            try
            {
                // get siblings ordered by current Order
                var siblings = await _db.Folders
                    .Where(f => f.ParentFolderId == parentFolderId)
                    .OrderBy(f => f.Order)
                    .ToListAsync(ct);

                var target = siblings.FirstOrDefault(f => f.Id == folderId);
                if (target == null) return false;

                siblings.Remove(target);
                newIndex = Math.Clamp(newIndex, 0, siblings.Count);
                siblings.Insert(newIndex, target);

                // reassign sequential Order values
                for (int i = 0; i < siblings.Count; i++)
                {
                    siblings[i].Order = i;
                }

                await _db.SaveChangesAsync(ct);
                await QueueFolderUpdatesAsync(siblings, ct);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] ReorderFolderAsync failed: {ex.Message}");
                return false;
            }
        }

        private async Task NormalizeSiblingOrderAsync(Guid? parentFolderId, CancellationToken ct)
        {
            var siblings = await _db.Folders
                .Where(f => f.ParentFolderId == parentFolderId)
                .OrderBy(f => f.Order)
                .ThenBy(f => f.Name)
                .ToListAsync(ct);

            var changed = false;
            for (int i = 0; i < siblings.Count; i++)
            {
                if (siblings[i].Order != i)
                {
                    siblings[i].Order = i;
                    changed = true;
                }
            }

            if (!changed)
                return;

            await _db.SaveChangesAsync(ct);
            await QueueFolderUpdatesAsync(siblings, ct);
        }

        private async Task QueueFolderUpdatesAsync(IEnumerable<Folder> folders, CancellationToken ct)
        {
            if (_syncService == null)
                return;

            var items = folders
                .Where(f => f.Id != Guid.Empty)
                .GroupBy(f => f.Id)
                .Select(g => g.First())
                .ToList();

            if (items.Count == 0)
                return;

            try
            {
                await _syncService.QueueBatchForSyncAsync(items, SyncOperationType.Update, ct);
                System.Diagnostics.Debug.WriteLine($"[FolderService] Queued {items.Count} folders for Update sync");
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] Failed to queue folder batch sync: {syncEx.Message}");
            }
        }

        private async Task QueueRecipesForUpdateAsync(IEnumerable<Recipe> recipes, CancellationToken ct)
        {
            if (_syncService == null)
                return;

            var items = recipes
                .Where(r => r.Id != Guid.Empty)
                .GroupBy(r => r.Id)
                .Select(g => g.First())
                .ToList();

            if (items.Count == 0)
                return;

            try
            {
                await _syncService.QueueBatchForSyncAsync(items, SyncOperationType.Update, ct);
                System.Diagnostics.Debug.WriteLine($"[FolderService] Queued {items.Count} recipes for Update sync");
            }
            catch (Exception syncEx)
            {
                System.Diagnostics.Debug.WriteLine($"[FolderService] Failed to queue recipe batch sync: {syncEx.Message}");
            }
        }

        // Build a hierarchical tree with Level/DisplayName populated
        private static List<Folder> BuildHierarchy(List<Folder> flat)
        {
            var byId = flat.ToDictionary(f => f.Id);
            foreach (var f in flat)
            {
                f.SubFolders = new List<Folder>();
            }

            var roots = new List<Folder>();
            foreach (var f in flat)
            {
                if (f.ParentFolderId.HasValue && byId.TryGetValue(f.ParentFolderId.Value, out var parent))
                {
                    parent.SubFolders.Add(f);
                }
                else
                {
                    roots.Add(f);
                }
            }

            void AssignMeta(Folder folder, int level)
            {
                folder.Level = level;
                folder.DisplayName = new string('?', Math.Max(0, level)) + (level > 0 ? " " : string.Empty) + folder.Name;
                foreach (var child in folder.SubFolders.OrderBy(x => x.Order).ThenBy(x => x.Name))
                    AssignMeta(child, level + 1);
            }

            foreach (var root in roots.OrderBy(x => x.Order).ThenBy(x => x.Name))
                AssignMeta(root, 0);

            return roots;
        }
    }
}
