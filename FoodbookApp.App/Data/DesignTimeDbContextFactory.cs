using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace Foodbook.Data
{
    // Umo¿liwia generowanie migracji EF Core bez uruchamiania aplikacji MAUI (design-time)
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Lokalna œcie¿ka pliku DB tylko na potrzeby design-time
            var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "foodbook.dev.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
