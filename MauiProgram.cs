using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Foodbook.Data;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Views; // Dodaj to, jeśli rejestrujesz Pages

namespace FoodbookApp
{
    public static class MauiProgram
    {
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
            builder.Services.AddScoped<RecipeViewModel>();
            builder.Services.AddScoped<AddRecipeViewModel>();

            // 🧭 Rejestracja widoków (Pages), jeśli używasz DI do ich tworzenia
            builder.Services.AddScoped<RecipesPage>();
            builder.Services.AddScoped<AddRecipePage>();

            // 🧠 Rejestracja routów do Shell (opcjonalne, jeśli używasz Shell)
            Routing.RegisterRoute(nameof(RecipesPage), typeof(RecipesPage));
            Routing.RegisterRoute(nameof(AddRecipePage), typeof(AddRecipePage));

            // ✨ Build aplikacji
            var app = builder.Build();

            // 📦 Inicjalizacja bazy danych
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                // Możesz też dodać SeedData.InitializeAsync(db).Wait();
            }

            return app;
        }
    }
}
