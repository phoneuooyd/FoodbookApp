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
                    // OpenSans fonts (original)
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    
                    // Barlow Condensed font family - multiple weights
                    fonts.AddFont("BarlowCondensed-Regular.ttf", "BarlowCondensedRegular");
                    fonts.AddFont("BarlowCondensed-Light.ttf", "BarlowCondensedLight");
                    fonts.AddFont("BarlowCondensed-Medium.ttf", "BarlowCondensedMedium");
                    fonts.AddFont("BarlowCondensed-SemiBold.ttf", "BarlowCondensedSemibold");
                    fonts.AddFont("BarlowCondensed-ExtraLight.ttf", "BarlowCondensedExtraLight");
                    fonts.AddFont("BarlowCondensed-Thin.ttf", "BarlowCondensedThin");
                    
                    // Decorative and display fonts
                    fonts.AddFont("CherryBombOne-Regular.ttf", "CherryBombOneRegular");
                    fonts.AddFont("DynaPuff-Regular.ttf", "DynaPuffRegular");
                    fonts.AddFont("DynaPuff-Medium.ttf", "DynaPuffMedium");
                    fonts.AddFont("DynaPuff-SemiBold.ttf", "DynaPuffSemibold");
                    fonts.AddFont("DynaPuff-Bold.ttf", "DynaPuffBold");
                    
                    // Modern and geometric fonts
                    fonts.AddFont("Gruppo-Regular.ttf", "GruppoRegular");
                    fonts.AddFont("PoiretOne-Regular.ttf", "PoiretOneRegular");
                    
                    // Handwritten and script fonts
                    fonts.AddFont("JustMeAgainDownHere-Regular.ttf", "JustMeAgainDownHereRegular");
                    fonts.AddFont("Kalam-Regular.ttf", "KalamRegular");
                    fonts.AddFont("SendFlowers-Regular.ttf", "SendFlowersRegular");
                    fonts.AddFont("Yellowtail-Regular.ttf", "YellowtailRegular");
                    
                    // Serif font
                    fonts.AddFont("Slabo27px-Regular.ttf", "Slabo27pxRegular");
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
            builder.Services.AddSingleton<IThemeService, ThemeService>(); // Added ThemeService
            builder.Services.AddSingleton<IFontService, FontService>(); // Added FontService
            builder.Services.AddSingleton<IEventBus, EventBus>(); // Added EventBus for ViewModel communication

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
        /// Publiczna metoda do wykonania migracji i walidacji schematu bazy danych
        /// </summary>
        public static async Task<bool> MigrateDatabaseAsync()
        {
            try
            {
                LogDebug("Starting database migration and schema validation");
                
                if (ServiceProvider == null)
                {
                    LogError("ServiceProvider is null - cannot perform migration");
                    return false;
                }
                
                using var scope = ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                // Sprawdź i zwaliduj schemat bazy danych
                await ValidateDatabaseSchemaAsync(db);
                
                // Wykonaj inne potrzebne migracje
                await ApplyDatabaseMigrationsAsync(db);
                
                LogDebug("Database migration and schema validation completed successfully");
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

        private static async Task ValidateDatabaseSchemaAsync(AppDbContext db)
        {
            try
            {
                LogDebug("Starting comprehensive database schema validation");
                
                // Validate all tables and their expected columns
                await ValidateTableSchemaAsync(db, "Ingredients", new[]
                {
                    ("Id", "INTEGER"),
                    ("Name", "TEXT"),
                    ("Quantity", "REAL"),
                    ("Unit", "INTEGER"),
                    ("Calories", "REAL"),
                    ("Protein", "REAL"),
                    ("Fat", "REAL"),
                    ("Carbs", "REAL"),
                    ("RecipeId", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "Recipes", new[]
                {
                    ("Id", "INTEGER"),
                    ("Name", "TEXT"),
                    ("Description", "TEXT"),
                    ("Calories", "REAL"),
                    ("Protein", "REAL"),
                    ("Fat", "REAL"),
                    ("Carbs", "REAL"),
                    ("IloscPorcji", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "Plans", new[]
                {
                    ("Id", "INTEGER"),
                    ("StartDate", "TEXT"),
                    ("EndDate", "TEXT"),
                    ("IsArchived", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "PlannedMeals", new[]
                {
                    ("Id", "INTEGER"),
                    ("RecipeId", "INTEGER"),
                    ("Date", "TEXT"),
                    ("Portions", "INTEGER")
                });

                await ValidateTableSchemaAsync(db, "ShoppingListItems", new[]
                {
                    ("Id", "INTEGER"),
                    ("PlanId", "INTEGER"),
                    ("IngredientName", "TEXT"),
                    ("Unit", "INTEGER"),
                    ("IsChecked", "INTEGER"),
                    ("Quantity", "REAL"),
                    ("Order", "INTEGER")
                });

                LogDebug("Database schema validation completed successfully");
            }
            catch (Exception ex)
            {
                LogError($"Error during database schema validation: {ex.Message}");
                throw;
            }
        }

        private static async Task ValidateTableSchemaAsync(AppDbContext db, string tableName, (string columnName, string expectedType)[] expectedColumns)
        {
            try
            {
                LogDebug($"Validating table: {tableName}");
                
                // Get current table schema
                var currentColumns = await GetTableColumnsAsync(db, tableName);
                
                if (!currentColumns.Any())
                {
                    LogWarning($"Table {tableName} does not exist or has no columns");
                    return;
                }

                var missingColumns = new List<string>();
                var typeConflicts = new List<string>();

                foreach (var (columnName, expectedType) in expectedColumns)
                {
                    var existingColumn = currentColumns.FirstOrDefault(c => 
                        c.name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingColumn.name == null)
                    {
                        missingColumns.Add($"{columnName} ({expectedType})");
                    }
                    else
                    {
                        // Check type compatibility (SQLite type mapping)
                        var actualType = MapSqliteType(existingColumn.type);
                        var expectedMappedType = MapSqliteType(expectedType);
                        
                        if (!actualType.Equals(expectedMappedType, StringComparison.OrdinalIgnoreCase))
                        {
                            typeConflicts.Add($"{columnName}: expected {expectedType}, got {existingColumn.type}");
                        }
                    }
                }

                // Add missing columns
                foreach (var missingColumn in missingColumns)
                {
                    var columnParts = missingColumn.Split('(', ')');
                    var columnName = columnParts[0].Trim();
                    var columnType = columnParts.Length > 1 ? columnParts[1].Trim() : "TEXT";
                    
                    await AddMissingColumnAsync(db, tableName, columnName, columnType);
                }

                // Log type conflicts (but don't fix them as it could be destructive)
                foreach (var conflict in typeConflicts)
                {
                    LogWarning($"Type conflict in {tableName}: {conflict}");
                }

                LogDebug($"Table {tableName} validation completed. Missing columns added: {missingColumns.Count}, Type conflicts: {typeConflicts.Count}");
            }
            catch (Exception ex)
            {
                LogError($"Error validating table {tableName}: {ex.Message}");
                throw;
            }
        }

        private static async Task<List<(string name, string type)>> GetTableColumnsAsync(AppDbContext db, string tableName)
        {
            try
            {
                var query = $"PRAGMA table_info([{tableName}])";
                var connection = db.Database.GetDbConnection();
                
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = query;
                
                var columns = new List<(string name, string type)>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    // PRAGMA table_info returns: cid|name|type|notnull|dflt_value|pk
                    // We need columns 1 (name) and 2 (type)
                    var name = reader.GetString(1);
                    var type = reader.GetString(2);
                    columns.Add((name, type));
                }

                return columns;
            }
            catch (Exception ex)
            {
                LogError($"Error getting columns for table {tableName}: {ex.Message}");
                return new List<(string, string)>();
            }
        }

        private static async Task AddMissingColumnAsync(AppDbContext db, string tableName, string columnName, string columnType)
        {
            try
            {
                LogDebug($"Adding missing column {columnName} ({columnType}) to table {tableName}");
                
                var defaultValue = GetDefaultValueForType(columnType);
                var sql = $"ALTER TABLE [{tableName}] ADD COLUMN [{columnName}] {columnType} NOT NULL DEFAULT {defaultValue}";
                
                await db.Database.ExecuteSqlRawAsync(sql);
                LogDebug($"Successfully added column {columnName} to table {tableName}");
            }
            catch (Exception ex)
            {
                LogError($"Error adding column {columnName} to table {tableName}: {ex.Message}");
                throw;
            }
        }

        private static string GetDefaultValueForType(string sqliteType)
        {
            return sqliteType.ToUpperInvariant() switch
            {
                "INTEGER" => "0",
                "REAL" => "0.0",
                "TEXT" => "''",
                "BLOB" => "''",
                _ => "''"
            };
        }

        private static string MapSqliteType(string type)
        {
            // SQLite has dynamic typing, but we normalize to expected types
            return type.ToUpperInvariant() switch
            {
                "INT" or "INTEGER" or "BIGINT" => "INTEGER",
                "REAL" or "DOUBLE" or "FLOAT" => "REAL", 
                "TEXT" or "VARCHAR" or "CHAR" => "TEXT",
                "BLOB" => "BLOB",
                _ => "TEXT"
            };
        }

        private static async Task ApplyDatabaseMigrationsAsync(AppDbContext db)
        {
            try
            {
                LogDebug("Applying database migrations");
                
                // Wykonaj walidację schematu bazy danych
                await ValidateDatabaseSchemaAsync(db);
                
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
            
            // Migracja do wersji 1: walidacja pełnego schematu bazy danych
            await ValidateDatabaseSchemaAsync(db);
            
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
