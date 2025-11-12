using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace Foodbook.Data
{
#if DEBUG && (WINDOWS || MACCATALYST)
    // Only available for design-time tooling on desktop during development.
    // Prevents interference with Android packaging and Release builds.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "foodbook.dev.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
#endif
}
