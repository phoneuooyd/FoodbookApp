using Microsoft.EntityFrameworkCore;
using Foodbook.Models;
using System.IO;
#if ANDROID
using Microsoft.Maui.Storage;
#endif

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

        // Used by DI at runtime
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Fallback constructor for design-time tooling (dotnet-ef) to avoid requiring IDesignTimeDbContextFactory
        public AppDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Configure only if not already configured by DI
            if (!optionsBuilder.IsConfigured)
            {
#if ANDROID
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbookapp.db");
#else
                var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "foodbook.dev.db");
#endif
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

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

            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => i.Name)
                .HasDatabaseName("IX_Ingredients_Name");

            modelBuilder.Entity<Ingredient>()
                .HasIndex(i => new { i.RecipeId, i.Name })
                .HasDatabaseName("IX_Ingredients_RecipeId_Name");

            modelBuilder.Entity<PlannedMeal>()
                .HasOne(pm => pm.Recipe)
                .WithMany()
                .HasForeignKey(pm => pm.RecipeId);

            modelBuilder.Entity<PlannedMeal>()
                .HasOne<Plan>()
                .WithMany()
                .HasForeignKey(pm => pm.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Recipe>()
                .HasIndex(r => r.Name)
                .HasDatabaseName("IX_Recipes_Name");

            modelBuilder.Entity<Plan>()
                .Property(p => p.Type)
                .HasConversion<int>()
                .HasDefaultValue(PlanType.Planner);

            modelBuilder.Entity<ShoppingListItem>()
                .HasOne(sli => sli.Plan)
                .WithMany()
                .HasForeignKey(sli => sli.PlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ShoppingListItem>()
                .HasIndex(sli => new { sli.PlanId, sli.IngredientName, sli.Unit })
                .IsUnique()
                .HasDatabaseName("IX_ShoppingListItems_PlanId_IngredientName_Unit");

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