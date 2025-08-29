using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Foodbook.Data;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Views;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace FoodbookApp
{
    public static class MauiProgram
    {
        public static IServiceProvider? ServiceProvider { get; private set; }
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>() // <-- App.xaml.cs
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Rejestracja EFCore DbContext
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbook.db");
                options.UseSqlite($"Filename={dbPath}");
            });

            // Rejestracja serwisów i VM
            builder.Services.AddScoped<IRecipeService, RecipeService>();
            builder.Services.AddScoped<IPlannerService, PlannerService>();
            builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
            builder.Services.AddScoped<IPlanService, PlanService>();
            builder.Services.AddScoped<IIngredientService, IngredientService>();
            builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
            builder.Services.AddSingleton<LocalizationResourceManager>();
            builder.Services.AddSingleton<IPreferencesService, PreferencesService>(); // Added PreferencesService

            builder.Services.AddScoped<RecipeViewModel>();
            builder.Services.AddTransient<AddRecipeViewModel>(); // Zmieniono na Transient
            builder.Services.AddScoped<PlannerViewModel>();
            builder.Services.AddScoped<HomeViewModel>();
            builder.Services.AddScoped<ShoppingListViewModel>();
            builder.Services.AddScoped<ShoppingListDetailViewModel>();
            builder.Services.AddScoped<IngredientsViewModel>();
            builder.Services.AddScoped<IngredientFormViewModel>();
            builder.Services.AddScoped<PlannedMealFormViewModel>();
            builder.Services.AddScoped<ArchiveViewModel>(); // Dodany ArchiveViewModel
            builder.Services.AddSingleton<SettingsViewModel>();

            // Rejestracja HttpClient i RecipeImporter
            builder.Services.AddScoped<HttpClient>();
            builder.Services.AddScoped<RecipeImporter>();

            // Rejestracja widoków (Pages), jeśli używasz DI do ich tworzenia
            builder.Services.AddScoped<HomePage>();
            builder.Services.AddScoped<RecipesPage>();
            builder.Services.AddTransient<AddRecipePage>(); // Zmieniono na Transient
            builder.Services.AddScoped<IngredientsPage>();
            builder.Services.AddScoped<IngredientFormPage>();
            builder.Services.AddScoped<PlannerPage>();
            builder.Services.AddScoped<MealFormPage>();
            builder.Services.AddScoped<ShoppingListPage>();
            builder.Services.AddScoped<ShoppingListDetailPage>();
            builder.Services.AddScoped<ArchivePage>(); // Dodana ArchivePage
            builder.Services.AddScoped<SettingsPage>(); // Dodana SettingsPage

            // Rejestracja routów do Shell (opcjonalne, jeśli używasz Shell)
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(RecipesPage), typeof(RecipesPage));
            Routing.RegisterRoute(nameof(AddRecipePage), typeof(AddRecipePage));
            Routing.RegisterRoute(nameof(IngredientFormPage), typeof(IngredientFormPage));
            Routing.RegisterRoute(nameof(IngredientsPage), typeof(IngredientsPage));
            Routing.RegisterRoute(nameof(PlannerPage), typeof(PlannerPage));
            Routing.RegisterRoute(nameof(MealFormPage), typeof(MealFormPage));
            Routing.RegisterRoute(nameof(ShoppingListPage), typeof(ShoppingListPage));
            Routing.RegisterRoute(nameof(ShoppingListDetailPage), typeof(ShoppingListDetailPage));
            Routing.RegisterRoute(nameof(ArchivePage), typeof(ArchivePage)); // Dodana rejestracja routingu dla ArchivePage
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage)); // Dodana rejestracja routingu dla SettingsPage
            

            // Build aplikacji
            var app = builder.Build();
            ServiceProvider = app.Services;

            // Inicjalizacja bazy danych w tle
            Task.Run(() => SeedDatabaseAsync(app.Services));

            return app;
        }

        /// <summary>
        /// Publiczna metoda do wykonania migracji bazy danych
        /// </summary>
        public static async Task<bool> MigrateDatabaseAsync()
        {
            try
            {
                LogDebug("Starting database migration");
                
                if (ServiceProvider == null)
                {
                    LogError("ServiceProvider is null - cannot perform migration");
                    return false;
                }
                
                using var scope = ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                // Sprawdź czy kolumna Order istnieje w tabeli ShoppingListItems
                await EnsureOrderColumnExistsAsync(db);
                
                // Wykonaj inne potrzebne migracje
                await ApplyDatabaseMigrationsAsync(db);
                
                LogDebug("Database migration completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Database migration failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Publiczna metoda do pełnego resetu bazy danych
        /// </summary>
        public static async Task<bool> ResetDatabaseAsync()
        {
            try
            {
                LogDebug("Starting database reset");
                
                if (ServiceProvider == null)
                {
                    LogError("ServiceProvider is null - cannot perform reset");
                    return false;
                }
                
                using var scope = ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                LogDebug("Deleting and recreating database");
                await db.Database.EnsureDeletedAsync();
                await db.Database.EnsureCreatedAsync();
                
                await SeedData.InitializeAsync(db);
                LogDebug("Database reset completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"Database reset failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private static async Task EnsureOrderColumnExistsAsync(AppDbContext db)
        {
            try
            {
                LogDebug("Checking if Order column exists in ShoppingListItems table");
                
                // Sprawdź czy kolumna Order istnieje
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT \"Order\" FROM \"ShoppingListItems\" LIMIT 1");
                LogDebug("Order column already exists");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such column"))
            {
                LogDebug("Order column missing, adding it");
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"ShoppingListItems\" ADD COLUMN \"Order\" INTEGER NOT NULL DEFAULT 0");
                LogDebug("Order column added successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error checking/adding Order column: {ex.Message}");
                throw;
            }
        }

        private static async Task ApplyDatabaseMigrationsAsync(AppDbContext db)
        {
            try
            {
                LogDebug("Applying database migrations");
                
                // Tutaj można dodać kolejne migracje w przyszłości
                // Przykład: dodanie nowych kolumn, indeksów, itp.
                
                // Sprawdź wersję schematu i wykonaj odpowiednie migracje
                await EnsureDatabaseVersionAsync(db);
                
                LogDebug("All migrations applied successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error applying migrations: {ex.Message}");
                throw;
            }
        }

        private static async Task EnsureDatabaseVersionAsync(AppDbContext db)
        {
            try
            {
                // Sprawdź czy tabela wersji istnieje
                var tableExists = await db.Database.ExecuteSqlRawAsync(
                    "CREATE TABLE IF NOT EXISTS \"DatabaseVersion\" (\"Version\" INTEGER PRIMARY KEY)");
                
                // Pobierz aktualną wersję
                var currentVersion = await GetDatabaseVersionAsync(db);
                LogDebug($"Current database version: {currentVersion}");
                
                // Wykonaj migracje w zależności od wersji
                if (currentVersion < 1)
                {
                    await MigrateToVersion1Async(db);
                    await SetDatabaseVersionAsync(db, 1);
                }
                
                // Kolejne wersje można dodać tutaj
                // if (currentVersion < 2) { ... }
            }
            catch (Exception ex)
            {
                LogError($"Error managing database version: {ex.Message}");
                throw;
            }
        }

        private static async Task<int> GetDatabaseVersionAsync(AppDbContext db)
        {
            try
            {
                var result = await db.Database.SqlQueryRaw<int>(
                    "SELECT COALESCE(MAX(Version), 0) FROM DatabaseVersion").FirstOrDefaultAsync();
                return result;
            }
            catch
            {
                return 0; // Brak tabeli wersji = wersja 0
            }
        }

        private static async Task SetDatabaseVersionAsync(AppDbContext db, int version)
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT OR REPLACE INTO DatabaseVersion (Version) VALUES ({0})", version);
        }

        private static async Task MigrateToVersion1Async(AppDbContext db)
        {
            LogDebug("Migrating to version 1");
            
            // Migracja do wersji 1: dodanie kolumny Order
            await EnsureOrderColumnExistsAsync(db);
            
            LogDebug("Migration to version 1 completed");
        }

        private static async Task SeedDatabaseAsync(IServiceProvider services)
        {
            try
            {
                LogDebug("Starting database initialization");
                
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                LogDebug("Ensuring database is created");
                await db.Database.EnsureCreatedAsync();
                LogDebug("Database created successfully");

                // Wykonaj migracje jeśli potrzebne
                await ApplyDatabaseMigrationsAsync(db);

                await SeedData.InitializeAsync(db);
                LogDebug("Data initialization completed");
            }
            catch (Exception ex)
            {
                LogError($"Database initialization failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                LogWarning("App will continue without seeded data");
            }
        }

        // Centralized logging methods
        private static void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiProgram] {message}");
        }

        private static void LogWarning(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiProgram] WARNING: {message}");
        }

        private static void LogError(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MauiProgram] ERROR: {message}");
        }
    }
}
