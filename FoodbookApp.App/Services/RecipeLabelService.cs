using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services
{
    public class RecipeLabelService : IRecipeLabelService
    {
        private readonly AppDbContext _db;
        private readonly ISupabaseSyncService? _syncService;
        
        public RecipeLabelService(AppDbContext db, IServiceProvider serviceProvider)
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

        public async Task<List<RecipeLabel>> GetAllAsync(CancellationToken ct = default)
        {
            return await _db.RecipeLabels.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);
        }

        public async Task<RecipeLabel?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await _db.RecipeLabels.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        }

        public async Task<RecipeLabel> AddAsync(RecipeLabel label, CancellationToken ct = default)
        {
            if (label.Id == Guid.Empty)
                label.Id = Guid.NewGuid();

            _db.RecipeLabels.Add(label);
            await _db.SaveChangesAsync(ct);
            return label;
        }

        public async Task<RecipeLabel> UpdateAsync(RecipeLabel label, CancellationToken ct = default)
        {
            var existing = await _db.RecipeLabels.FirstOrDefaultAsync(l => l.Id == label.Id, ct);
            if (existing == null) throw new InvalidOperationException($"Label {label.Id} not found");
            existing.Name = label.Name;
            existing.ColorHex = label.ColorHex;
            await _db.SaveChangesAsync(ct);
            
            // Queue for sync (Update)
            if (_syncService != null)
            {
                try
                {
                    await _syncService.QueueForSyncAsync(existing, SyncOperationType.Update, ct);
                    System.Diagnostics.Debug.WriteLine($"[RecipeLabelService] Queued label {existing.Id} for Update sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeLabelService] Failed to queue sync: {syncEx.Message}");
                }
            }
            
            return existing;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var existing = await _db.RecipeLabels.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (existing == null) return false;
            _db.RecipeLabels.Remove(existing);
            await _db.SaveChangesAsync(ct);
            
            // Queue for sync (Delete)
            if (_syncService != null)
            {
                try
                {
                    var deleteEntity = new RecipeLabel { Id = id, Name = existing.Name };
                    await _syncService.QueueForSyncAsync(deleteEntity, SyncOperationType.Delete, ct);
                    System.Diagnostics.Debug.WriteLine($"[RecipeLabelService] Queued label {id} for Delete sync");
                }
                catch (Exception syncEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[RecipeLabelService] Failed to queue sync: {syncEx.Message}");
                }
            }
            
            return true;
        }
    }
}
