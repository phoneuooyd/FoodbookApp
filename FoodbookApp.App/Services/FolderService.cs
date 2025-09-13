using Foodbook.Data;
using Foodbook.Models;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services
{
    public interface IFolderService
    {
        Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default);
        Task<List<Folder>> GetFolderHierarchyAsync(CancellationToken ct = default);
        Task<Folder?> GetFolderByIdAsync(int id, CancellationToken ct = default);
        Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default);
        Task UpdateFolderAsync(Folder folder, CancellationToken ct = default);
        Task DeleteFolderAsync(int id, CancellationToken ct = default);
        Task<bool> MoveFolderAsync(int folderId, int? newParentFolderId, CancellationToken ct = default);
        Task<bool> MoveRecipeToFolderAsync(int recipeId, int? targetFolderId, CancellationToken ct = default);
        Task<bool> IsValidFolderMoveAsync(int folderId, int? newParentFolderId, CancellationToken ct = default);
    }

    public class FolderService : IFolderService
    {
        private readonly AppDbContext _db;

        public FolderService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<Folder>> GetFoldersAsync(CancellationToken ct = default)
        {
            return await _db.Folders
                .AsNoTracking()
                .Include(f => f.SubFolders)
                .Include(f => f.Recipes)
                .OrderBy(f => f.Name)
                .ToListAsync(ct);
        }

        public async Task<List<Folder>> GetFolderHierarchyAsync(CancellationToken ct = default)
        {
            var folders = await _db.Folders
                .AsNoTracking()
                .Include(f => f.Recipes)
                .ToListAsync(ct);

            return BuildHierarchy(folders);
        }

        public async Task<Folder?> GetFolderByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.Folders
                .AsNoTracking()
                .Include(f => f.SubFolders)
                .Include(f => f.Recipes)
                .FirstOrDefaultAsync(f => f.Id == id, ct);
        }

        public async Task<Folder> AddFolderAsync(Folder folder, CancellationToken ct = default)
        {
            // Ensure database/tables exist (device may still be initializing DB)
            await _db.Database.EnsureCreatedAsync(ct);

            // sanitize required fields to avoid SaveChanges failure on devices with older DB
            folder.Name = folder.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(folder.Name))
                throw new ArgumentException("Folder name is required", nameof(folder));

            try
            {
                _db.Folders.Add(folder);
                await _db.SaveChangesAsync(ct);
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
            folder.Name = folder.Name?.Trim() ?? string.Empty;
            _db.Folders.Update(folder);
            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteFolderAsync(int id, CancellationToken ct = default)
        {
            var folder = await _db.Folders
                .Include(f => f.SubFolders)
                .Include(f => f.Recipes)
                .FirstOrDefaultAsync(f => f.Id == id, ct);
            if (folder == null) return;

            // Move recipes to parent folder (or root)
            foreach (var recipe in folder.Recipes)
            {
                recipe.FolderId = folder.ParentFolderId; // may be null => move to root
            }

            await _db.SaveChangesAsync(ct);

            // Ensure no child folders remain attached to this parent to satisfy Restrict behavior
            foreach (var sub in folder.SubFolders)
            {
                sub.ParentFolderId = folder.ParentFolderId;
            }

            await _db.SaveChangesAsync(ct);

            _db.Folders.Remove(folder);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<bool> MoveFolderAsync(int folderId, int? newParentFolderId, CancellationToken ct = default)
        {
            if (!await IsValidFolderMoveAsync(folderId, newParentFolderId, ct))
                return false;

            var folder = await _db.Folders.FirstOrDefaultAsync(f => f.Id == folderId, ct);
            if (folder == null) return false;

            folder.ParentFolderId = newParentFolderId;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> MoveRecipeToFolderAsync(int recipeId, int? targetFolderId, CancellationToken ct = default)
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
            return true;
        }

        public async Task<bool> IsValidFolderMoveAsync(int folderId, int? newParentFolderId, CancellationToken ct = default)
        {
            if (folderId == newParentFolderId) return false; // cannot move under itself
            if (!newParentFolderId.HasValue) return true; // move to root allowed

            // prevent cycles: new parent cannot be a descendant of folderId
            var folders = await _db.Folders.AsNoTracking().ToListAsync(ct);
            var map = folders.ToDictionary(f => f.Id, f => f.ParentFolderId);

            int? current = newParentFolderId;
            while (current.HasValue)
            {
                if (current.Value == folderId) return false; // cycle detected
                map.TryGetValue(current.Value, out current);
            }
            return true;
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
                folder.DisplayName = new string('·', Math.Max(0, level)) + (level > 0 ? " " : string.Empty) + folder.Name;
                foreach (var child in folder.SubFolders.OrderBy(x => x.Name))
                    AssignMeta(child, level + 1);
            }

            foreach (var root in roots.OrderBy(x => x.Name))
                AssignMeta(root, 0);

            return roots;
        }
    }
}
