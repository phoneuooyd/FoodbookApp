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
