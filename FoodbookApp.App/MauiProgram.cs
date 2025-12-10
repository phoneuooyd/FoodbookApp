using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Foodbook.Data;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Views;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using CommunityToolkit.Maui;
using FoodbookApp.Interfaces;
using Sharpnado.CollectionView; // ? Sharpnado CollectionView namespace

namespace FoodbookApp
{
    public static class MauiProgram
    {
        public static IServiceProvider? ServiceProvider { get; private set; }
        public static MauiApp CreateMauiApp()
        {
            System.Diagnostics.Debug.WriteLine("[MauiProgram] CreateMauiApp start");
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseSharpnadoCollectionView(loggerEnable: false) // ? Initialize Sharpnado CollectionView with drag-and-drop support
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("BarlowCondensed-Regular.ttf", "BarlowCondensedRegular");
                    fonts.AddFont("BarlowCondensed-Light.ttf", "BarlowCondensedLight");
                    fonts.AddFont("BarlowCondensed-Medium.ttf", "BarlowCondensedMedium");
                    fonts.AddFont("BarlowCondensed-SemiBold.ttf", "BarlowCondensedSemibold");
                    fonts.AddFont("BarlowCondensed-ExtraLight.ttf", "BarlowCondensedExtraLight");
                    fonts.AddFont("BarlowCondensed-Thin.ttf", "BarlowCondensedThin");
                    fonts.AddFont("CherryBombOne-Regular.ttf", "CherryBombOneRegular");
                    fonts.AddFont("DynaPuff-Regular.ttf", "DynaPuffRegular");
                    fonts.AddFont("DynaPuff-Medium.ttf", "DynaPuffMedium");
                    fonts.AddFont("DynaPuff-SemiBold.ttf", "DynaPuffSemibold");
                    fonts.AddFont("DynaPuff-Bold.ttf", "DynaPuffBold");
                    fonts.AddFont("Gruppo-Regular.ttf", "GruppoRegular");
                    fonts.AddFont("PoiretOne-Regular.ttf", "PoiretOneRegular");
                    fonts.AddFont("JustMeAgainDownHere-Regular.ttf", "JustMeAgainDownHereRegular");
                    fonts.AddFont("Kalam-Regular.ttf", "KalamRegular");
                    fonts.AddFont("SendFlowers-Regular.ttf", "SendFlowersRegular");
                    fonts.AddFont("Yellowtail-Regular.ttf", "YellowtailRegular");
                    fonts.AddFont("Slabo27px-Regular.ttf", "Slabo27pxRegular");
                });

            // Enable debug logger also in Release to aid diagnostics
            builder.Logging.AddDebug();

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Registering DbContext");
            // Use extension to add AppDbContext
            builder.Services.AddAppDbContext();

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Registering services & view models");
            builder.Services.AddScoped<IRecipeService, RecipeService>();
            builder.Services.AddScoped<IPlannerService, PlannerService>();
            builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
            builder.Services.AddScoped<IPlanService, PlanService>();
            builder.Services.AddScoped<IIngredientService, IngredientService>();
            builder.Services.AddScoped<IFolderService, FolderService>();
            builder.Services.AddScoped<IRecipeLabelService, RecipeLabelService>();
            builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
            builder.Services.AddSingleton<LocalizationResourceManager>();
            builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
            builder.Services.AddSingleton<IThemeService, ThemeService>();
            builder.Services.AddSingleton<IFontService, FontService>();
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddScoped<RecipeViewModel>();
            builder.Services.AddTransient<AddRecipeViewModel>();
            builder.Services.AddScoped<PlannerViewModel>();
            builder.Services.AddScoped<PlannerEditViewModel>(); // NEW: Dedicated edit VM
            builder.Services.AddScoped<HomeViewModel>();
            builder.Services.AddScoped<ShoppingListViewModel>();
            builder.Services.AddScoped<ShoppingListDetailViewModel>();
            builder.Services.AddScoped<IngredientsViewModel>();
            builder.Services.AddScoped<IngredientFormViewModel>();
            builder.Services.AddScoped<PlannedMealFormViewModel>();
            builder.Services.AddScoped<ArchiveViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddTransient<SetupWizardViewModel>();
            // New: Planner lists VM
            builder.Services.AddTransient<PlannerListsViewModel>();

            // Http / import
            builder.Services.AddScoped<HttpClient>();
            builder.Services.AddScoped<RecipeImporter>();

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Registering pages");
            builder.Services.AddScoped<HomePage>();
            builder.Services.AddScoped<RecipesPage>();
            builder.Services.AddTransient<AddRecipePage>();
            builder.Services.AddScoped<IngredientsPage>();
            builder.Services.AddScoped<IngredientFormPage>();
            builder.Services.AddScoped<PlannerPage>();
            builder.Services.AddScoped<PlannerListsPage>();
            builder.Services.AddScoped<MealFormPage>();
            builder.Services.AddScoped<ShoppingListPage>();
            builder.Services.AddScoped<ShoppingListDetailPage>();
            builder.Services.AddScoped<ArchivePage>();
            builder.Services.AddScoped<SettingsPage>();
            builder.Services.AddScoped<SetupWizardPage>();
            builder.Services.AddScoped<DataArchivizationPage>();
            
            // NEW: MainPage with custom TabBarComponent
            builder.Services.AddScoped<MainPage>();

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Registering routes");
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(RecipesPage), typeof(RecipesPage));
            Routing.RegisterRoute(nameof(AddRecipePage), typeof(AddRecipePage));
            Routing.RegisterRoute(nameof(IngredientFormPage), typeof(IngredientFormPage));
            Routing.RegisterRoute(nameof(IngredientsPage), typeof(IngredientsPage));
            Routing.RegisterRoute(nameof(PlannerPage), typeof(PlannerPage));
            Routing.RegisterRoute(nameof(PlannerListsPage), typeof(PlannerListsPage));
            Routing.RegisterRoute(nameof(MealFormPage), typeof(MealFormPage));
            Routing.RegisterRoute(nameof(ShoppingListPage), typeof(ShoppingListPage));
            Routing.RegisterRoute(nameof(ShoppingListDetailPage), typeof(ShoppingListDetailPage));
            Routing.RegisterRoute(nameof(ArchivePage), typeof(ArchivePage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(SetupWizardPage), typeof(SetupWizardPage));
            Routing.RegisterRoute(nameof(DataArchivizationPage), typeof(DataArchivizationPage));

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Building app");
            var app = builder.Build();
            ServiceProvider = app.Services;
            System.Diagnostics.Debug.WriteLine("[MauiProgram] ServiceProvider created");

            // Initialize DB via DatabaseService synchronously to avoid async signature here
            try
            {
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Starting conditional deployment check...");
                var dbService = app.Services.GetRequiredService<IDatabaseService>();
                
                // FIRST: Run conditional deployment (checks for clean install flag and pending migrations)
                var deploymentSuccess = dbService.ConditionalDeploymentAsync().GetAwaiter().GetResult();
                if (deploymentSuccess)
                {
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] Conditional deployment completed successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MauiProgram] WARNING: Conditional deployment reported failure");
                }

                // THEN: Run normal initialization (ensures migrations are applied if not already done)
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Running database initialization...");
                dbService.InitializeAsync().GetAwaiter().GetResult();
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Database initialization completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] DB init failed: {ex.Message}\n{ex.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine("[MauiProgram] CreateMauiApp finished");
            return app;
        }

        // Centralized logging methods
        private static void LogDebug(string message) => System.Diagnostics.Debug.WriteLine($"[MauiProgram] {message}");
        private static void LogWarning(string message) => System.Diagnostics.Debug.WriteLine($"[MauiProgram] WARNING: {message}");
        private static void LogError(string message) => System.Diagnostics.Debug.WriteLine($"[MauiProgram] ERROR: {message}");
    }
}
