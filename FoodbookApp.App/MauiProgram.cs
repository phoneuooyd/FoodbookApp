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
        private static bool _isDatabaseInitialized = false;
        private static readonly object _initLock = new object();

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
                LogDebug($"Database path: {dbPath}");
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

            // Inicjalizacja bazy danych - bez Task.Run aby uniknąć race conditions
            _ = InitializeDatabaseAsync(app.Services);

            return app;
        }

        /// <summary>
        /// Publiczna metoda do sprawdzenia czy baza została zainicjalizowana
        /// </summary>
        public static bool IsDatabaseInitialized
        {
            get { lock (_initLock) { return _isDatabaseInitialized; } }
        }

        /// <summary>
        /// Publiczna metoda do ręcznej inicjalizacji bazy danych
        /// </summary>
        public static async Task EnsureDatabaseInitializedAsync()
        {
            if (!IsDatabaseInitialized && ServiceProvider != null)
            {
                await InitializeDatabaseAsync(ServiceProvider);
            }
        }

        private static async Task InitializeDatabaseAsync(IServiceProvider services)
        {
            lock (_initLock)
            {
                if (_isDatabaseInitialized) return;
            }

            try
            {
                LogDebug("Starting database initialization");
                
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                LogDebug("Ensuring database is created");
                await db.Database.EnsureCreatedAsync();
                LogDebug("Database created successfully");

                // Sprawdź czy są już składniki
                var hasIngredients = await db.Ingredients.AnyAsync();
                LogDebug($"Existing ingredients found: {hasIngredients}");

                if (!hasIngredients)
                {
                    LogDebug("No ingredients found, attempting to seed");
                    await SeedData.SeedIngredientsAsync(db);
                }

                lock (_initLock)
                {
                    _isDatabaseInitialized = true;
                }
                
                LogDebug("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                LogError($"Database initialization failed: {ex.Message}");
                LogError($"Stack trace: {ex.StackTrace}");
                LogWarning("App will continue without seeded data");
                
                // Nie ustawiamy _isDatabaseInitialized = true przy błędzie
                // aby umożliwić ponowną próbę
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
