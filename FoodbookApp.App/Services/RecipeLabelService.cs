using Foodbook.Data;
using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Foodbook.Services
{
    public class RecipeLabelService : IRecipeLabelService
    {
        private readonly AppDbContext _db;
        public RecipeLabelService(AppDbContext db) => _db = db;

        public async Task<List<RecipeLabel>> GetAllAsync(CancellationToken ct = default)
        {
            return await _db.RecipeLabels.AsNoTracking().OrderBy(l => l.Name).ToListAsync(ct);
        }

        public async Task<RecipeLabel?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _db.RecipeLabels.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        }

        public async Task<RecipeLabel> AddAsync(RecipeLabel label, CancellationToken ct = default)
        {
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
            return existing;
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
        {
            var existing = await _db.RecipeLabels.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (existing == null) return false;
            _db.RecipeLabels.Remove(existing);
            await _db.SaveChangesAsync(ct);
            return true;
        }
    }
}
