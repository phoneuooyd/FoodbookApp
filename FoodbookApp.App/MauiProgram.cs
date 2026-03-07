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
using Supabase;
using Microsoft.IdentityModel.Tokens;
using FoodbookApp.Services.Auth;
using Foodbook.Views.Components;
using FoodbookApp.Services.Supabase;

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
            builder.Services.AddScoped<IFeatureAccessService, FeatureAccessService>();
            builder.Services.AddScoped<IIngredientService, IngredientService>();
            builder.Services.AddScoped<IFolderService, FolderService>();
            builder.Services.AddScoped<IRecipeLabelService, RecipeLabelService>();
            builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
            builder.Services.AddSingleton<LocalizationResourceManager>();
            builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
            builder.Services.AddSingleton<IThemeService, ThemeService>();
            builder.Services.AddSingleton<IFontService, FontService>();
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

            // MAUI client-side JWT handling:
            // - store token in SecureStorage
            // - attach token to outgoing HttpClient requests
            // - optionally validate token locally via JwtValidator (no AspNetCore middleware)
            builder.Services.AddSingleton<IAuthTokenStore, SecureStorageAuthTokenStore>();
            builder.Services.AddTransient<BearerTokenHandler>();
            builder.Services.AddSingleton<IJwtValidator>(_ => new JwtValidator(new JwtValidationOptions
            {
                SigningKey = builder.Configuration["Authentication:JwtSecret"] ?? string.Empty,
                Audience = builder.Configuration["Authentication:ValidAudience"] ?? string.Empty,
                Issuer = builder.Configuration["Authentication:ValidIssuer"] ?? string.Empty
            }));
            builder.Services.AddScoped<ISupabaseAuthService, SupabaseAuthService>();
            builder.Services.AddScoped<IAccountService, AccountService>();
            
            builder.Services.AddScoped<Supabase.Client>(_ =>
            new Supabase.Client(
                "https://gscbdvezastxpyndkauh.supabase.co",
                "sb_publishable_gwkJSRidW1DP28CCEeQUDA_ELLTHT92",
                new SupabaseOptions 
                { 
                    AutoRefreshToken = true,
                    AutoConnectRealtime = false // disable default realtime to avoid 403 storms; connect explicitly when needed
                }
            ));
            
            // Http / import - must register BEFORE SupabaseRestClient
            builder.Services.AddScoped(sp =>
            {
                var handler = sp.GetRequiredService<BearerTokenHandler>();
                // CRITICAL: DelegatingHandler requires InnerHandler to be set
                handler.InnerHandler = new HttpClientHandler();
                
                var client = new HttpClient(handler, disposeHandler: true);

                var baseUrl = builder.Configuration["Supabase:Url"];
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    client.BaseAddress = new Uri(baseUrl);
                }

                return client;
            });

            // Dedicated HttpClient for `RecipeImporter` (plain HTTP, no auth handler)
            builder.Services.AddScoped<RecipeImporter>(sp =>
            {
                var ingredientService = sp.GetRequiredService<IIngredientService>();
                return new RecipeImporter(new HttpClient(), ingredientService);
            });

            // Register SupabaseRestClient BEFORE SupabaseCrudService - must have HttpClient available
            builder.Services.AddScoped(sp =>
            {
                var httpClient = sp.GetRequiredService<HttpClient>();
                var tokenStore = sp.GetRequiredService<IAuthTokenStore>();
                return new SupabaseRestClient(
                    httpClient,
                    tokenStore,
                    "https://gscbdvezastxpyndkauh.supabase.co",
                    "sb_publishable_gwkJSRidW1DP28CCEeQUDA_ELLTHT92"
                );
            });
            
            // Register SupabaseCrudService AFTER SupabaseRestClient
            builder.Services.AddScoped<ISupabaseCrudService, SupabaseCrudService>();
            
            // Supabase Sync Service - singleton for durable queue and immediate processing across app lifecycle
            builder.Services.AddSingleton<ISupabaseSyncService, SupabaseSyncService>();
            
            // Deduplication Service - singleton to maintain cache across login sessions
            builder.Services.AddSingleton<IDeduplicationService, DeduplicationService>();

            builder.Services.AddTransient<RecipeViewModel>();
            builder.Services.AddTransient<AddRecipeViewModel>();
            builder.Services.AddTransient<PlannerViewModel>();
            builder.Services.AddScoped<PlannerEditViewModel>(); 
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<ShoppingListViewModel>();
            builder.Services.AddScoped<ShoppingListDetailViewModel>();
            builder.Services.AddScoped<IngredientsViewModel>();
            builder.Services.AddScoped<IngredientFormViewModel>();
            builder.Services.AddScoped<PlannedMealFormViewModel>();
            builder.Services.AddScoped<ArchiveViewModel>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddTransient<SetupWizardViewModel>();
            // New: Planner lists VM
            builder.Services.AddTransient<PlannerListsViewModel>();

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Registering pages");
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<RecipesPage>();
            builder.Services.AddTransient<AddRecipePage>();
            builder.Services.AddTransient<IngredientsPage>();
            builder.Services.AddScoped<IngredientFormPage>();
            builder.Services.AddTransient<PlannerPage>();
            builder.Services.AddScoped<PlannerListsPage>();
            builder.Services.AddScoped<MealFormPage>();
            builder.Services.AddTransient<ShoppingListPage>();
            builder.Services.AddScoped<ShoppingListDetailPage>();
            builder.Services.AddScoped<ArchivePage>();
            builder.Services.AddScoped<SettingsPage>();
            builder.Services.AddScoped<SetupWizardPage>();
            builder.Services.AddScoped<DataArchivizationPage>();
            builder.Services.AddScoped<ProfilePage>();
            
            // NEW: MainPage with custom TabBarComponent
            builder.Services.AddScoped<MainPage>();

            // NEW: Popups
            builder.Services.AddTransient<RegisterPopup>();

            System.Diagnostics.Debug.WriteLine("[MauiProgram] Registering routes");
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(ProfilePage), typeof(ProfilePage));
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

            // Database startup: Initialize only (no conditional deployment on app start)
            try
            {
                System.Diagnostics.Debug.WriteLine("[MauiProgram] Initializing database...");
                var dbService = app.Services.GetRequiredService<IDatabaseService>();
                Task.Run(() => dbService.InitializeAsync()).GetAwaiter().GetResult();
                System.Diagnostics.Debug.WriteLine("[MauiProgram] ✓ Database ready");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MauiProgram] DB init failed: {ex.Message}\n{ex.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine("[MauiProgram] CreateMauiApp finished");
            return app;
        }
    }
}
