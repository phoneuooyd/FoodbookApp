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

            // 💾 Rejestracja EFCore DbContext
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "foodbook.db");
                options.UseSqlite($"Filename={dbPath}");
            });

            // 🔧 Rejestracja serwisów i VM
            builder.Services.AddScoped<IRecipeService, RecipeService>();
            builder.Services.AddScoped<IPlannerService, PlannerService>();
            builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
            builder.Services.AddScoped<IPlanService, PlanService>();
            builder.Services.AddScoped<IIngredientService, IngredientService>();
            builder.Services.AddScoped<ILocalizationService, LocalizationService>(); // Dodany serwis lokalizacji

            builder.Services.AddScoped<RecipeViewModel>();
            builder.Services.AddScoped<AddRecipeViewModel>();
            builder.Services.AddScoped<PlannerViewModel>();
            builder.Services.AddScoped<HomeViewModel>();
            builder.Services.AddScoped<ShoppingListViewModel>();
            builder.Services.AddScoped<ShoppingListDetailViewModel>();
            builder.Services.AddScoped<IngredientsViewModel>();
            builder.Services.AddScoped<IngredientFormViewModel>();
            builder.Services.AddScoped<PlannedMealFormViewModel>();
            builder.Services.AddScoped<ArchiveViewModel>(); // Dodany ArchiveViewModel
            builder.Services.AddScoped<SettingsViewModel>(); // Dodany SettingsViewModel

            // Rejestracja HttpClient i RecipeImporter
            builder.Services.AddScoped<HttpClient>();
            builder.Services.AddScoped<RecipeImporter>();

            // 🧭 Rejestracja widoków (Pages), jeśli używasz DI do ich tworzenia
            builder.Services.AddScoped<HomePage>();
            builder.Services.AddScoped<RecipesPage>();
            builder.Services.AddScoped<AddRecipePage>();
            builder.Services.AddScoped<IngredientsPage>();
            builder.Services.AddScoped<IngredientFormPage>();
            builder.Services.AddScoped<PlannerPage>();
            builder.Services.AddScoped<MealFormPage>();
            builder.Services.AddScoped<ShoppingListPage>();
            builder.Services.AddScoped<ShoppingListDetailPage>();
            builder.Services.AddScoped<ArchivePage>(); // Dodana ArchivePage
            builder.Services.AddScoped<SettingsPage>(); // Dodana SettingsPage

            // 🧠 Rejestracja routów do Shell (opcjonalne, jeśli używasz Shell)
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
            

            // ✨ Build aplikacji
            var app = builder.Build();
            ServiceProvider = app.Services;

            // 📦 Inicjalizacja bazy danych w tle - poprawiona wersja
            Task.Run(async () => await SeedDatabaseAsync(app.Services));

            return app;
        }

        private static async Task SeedDatabaseAsync(IServiceProvider services)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🚀 Starting database initialization...");
                
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                System.Diagnostics.Debug.WriteLine("📊 Ensuring database is created...");
                await db.Database.EnsureCreatedAsync();
                
                System.Diagnostics.Debug.WriteLine("🔍 Checking existing data...");
                var hasIngredients = await db.Ingredients.AnyAsync();
                var hasRecipes = await db.Recipes.AnyAsync();
                
                System.Diagnostics.Debug.WriteLine($"📈 Current state: Ingredients={hasIngredients}, Recipes={hasRecipes}");

                // Zawsze próbuj załadować składniki jeśli ich nie ma
                if (!hasIngredients)
                {
                    System.Diagnostics.Debug.WriteLine("🌱 No ingredients found, starting seeding...");
                    await SeedData.SeedIngredientsAsync(db);
                }
                
                // Dodaj przykładowy przepis jeśli nie ma przepisów
                if (!hasRecipes)
                {
                    System.Diagnostics.Debug.WriteLine("📝 No recipes found, adding example recipe...");
                    await SeedData.InitializeAsync(db);
                }
                
                System.Diagnostics.Debug.WriteLine("✅ Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error during database initialization: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Nie rzucaj wyjątku - aplikacja powinna działać nawet bez seed data
            }
        }
    }
}
