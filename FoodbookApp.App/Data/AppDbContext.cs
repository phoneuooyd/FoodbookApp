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
        public DbSet<Folder> Folders => Set<Folder>();
        public DbSet<RecipeLabel> RecipeLabels => Set<RecipeLabel>();

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

            // Add indexes for better query performance
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

            // ShoppingListItem configuration
            modelBuilder.Entity<ShoppingListItem>()
                .HasOne(sli => sli.Plan)
                .WithMany()
                .HasForeignKey(sli => sli.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite unique index for shopping list items
            modelBuilder.Entity<ShoppingListItem>()
                .HasIndex(sli => new { sli.PlanId, sli.IngredientName, sli.Unit })
                .IsUnique()
                .HasDatabaseName("IX_ShoppingListItems_PlanId_IngredientName_Unit");

            // Folders
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.SubFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Folder>()
                .HasIndex(f => f.ParentFolderId)
                .HasDatabaseName("IX_Folders_ParentFolderId");

            modelBuilder.Entity<Folder>()
                .HasIndex(f => f.Name)
                .HasDatabaseName("IX_Folders_Name");

            modelBuilder.Entity<Recipe>()
                .HasOne(r => r.Folder)
                .WithMany(f => f.Recipes)
                .HasForeignKey(r => r.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Recipe>()
                .HasIndex(r => r.FolderId)
                .HasDatabaseName("IX_Recipes_FolderId");

            // Recipe labels: many-to-many
            modelBuilder.Entity<Recipe>()
                .HasMany(r => r.Labels)
                .WithMany()
                .UsingEntity<Dictionary<string, object>>(
                    "RecipeRecipeLabel",
                    j => j.HasOne<RecipeLabel>().WithMany().HasForeignKey("LabelsId").OnDelete(DeleteBehavior.Cascade),
                    j => j.HasOne<Recipe>().WithMany().HasForeignKey("RecipesId").OnDelete(DeleteBehavior.Cascade)
                );

            modelBuilder.Entity<RecipeLabel>()
                .HasIndex(l => l.Name)
                .HasDatabaseName("IX_RecipeLabels_Name");

            base.OnModelCreating(modelBuilder);
        }
    }
}