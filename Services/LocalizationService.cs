using System.Globalization;
using System.Resources;
using FoodbookApp.Localization;

namespace Foodbook.Services;

public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, ResourceManager> _resourceManagers = new();

    public event Action? CultureChanged;

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    public void SetCulture(string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CurrentCulture = culture;

        AddRecipePageResources.Culture = culture;
        ArchivePageResources.Culture = culture;
        HomePageResources.Culture = culture;
        IngredientFormPageResources.Culture = culture;
        IngredientsPageResources.Culture = culture;
        MealFormPageResources.Culture = culture;
        PlannerPageResources.Culture = culture;
        RecipesPageResources.Culture = culture;
        SettingsPageResources.Culture = culture;
        ShoppingListDetailPageResources.Culture = culture;
        ShoppingListPageResources.Culture = culture;
        TabBarResources.Culture = culture;

        CultureChanged?.Invoke();
    }

    public string GetString(string baseName, string key)
    {
        if (!_resourceManagers.TryGetValue(baseName, out var manager))
        {
            manager = new ResourceManager($"FoodbookApp.Localization.{baseName}", typeof(LocalizationService).Assembly);
            _resourceManagers[baseName] = manager;
        }

        return manager.GetString(key, CurrentCulture) ?? string.Empty;
    }
}
