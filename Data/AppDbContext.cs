using Microsoft.EntityFrameworkCore;
using Foodbook.Models;

namespace Foodbook.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Recipe> Recipes => Set<Recipe>();
        public DbSet<Ingredient> Ingredients => Set<Ingredient>();
        public DbSet<PlannedMeal> PlannedMeals => Set<PlannedMeal>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

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

            // ShoppingListItem relationships
            modelBuilder.Entity<ShoppingListItem>()
                .HasOne(sli => sli.Plan)
                .WithMany()
                .HasForeignKey(sli => sli.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add indexes for better query performance
            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => i.RecipeId)
                .HasDatabaseName("IX_Ingredients_RecipeId");

            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => i.Name)
                .HasDatabaseName("IX_Ingredients_Name");

            // Composite index for filtering standalone ingredients and ordering by name
            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => new { i.RecipeId, i.Name })
                .HasDatabaseName("IX_Ingredients_RecipeId_Name");

            modelBuilder.Entity<PlannedMeal>()
                .HasOne(pm => pm.Recipe)
                .WithMany()
                .HasForeignKey(pm => pm.RecipeId);

            // Index for recipe searches
            modelBuilder.Entity<Recipe>()
                .HasIndex(r => r.Name)
                .HasDatabaseName("IX_Recipes_Name");

            // ShoppingListItem indexes
            modelBuilder.Entity<ShoppingListItem>()
                .HasIndex(sli => sli.PlanId)
                .HasDatabaseName("IX_ShoppingListItems_PlanId");

            modelBuilder.Entity<ShoppingListItem>()
                .HasIndex(sli => sli.Name)
                .HasDatabaseName("IX_ShoppingListItems_Name");

            base.OnModelCreating(modelBuilder);
        }
    }
}