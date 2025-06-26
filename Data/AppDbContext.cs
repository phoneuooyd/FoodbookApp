using Microsoft.EntityFrameworkCore;
using Foodbook.Models;

namespace Foodbook.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Recipe>()
                .HasMany(r => r.Ingredients)
                .WithOne(i => i.Recipe)
                .HasForeignKey(i => i.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ingredient>()
                .Property(i => i.RecipeId)
                .IsRequired(false);

            modelBuilder.Entity<PlannedMeal>()
                .HasOne(pm => pm.Recipe)
                .WithMany()
                .HasForeignKey(pm => pm.RecipeId);

            base.OnModelCreating(modelBuilder);
        }
    }
}