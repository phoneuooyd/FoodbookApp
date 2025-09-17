using System.Globalization;
using System.Threading;
using System.Resources;
using FoodbookApp.Localization;

namespace Foodbook.Services;

public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, ResourceManager> _resourceManagers = new();

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    public event EventHandler? CultureChanged;

    public void SetCulture(string cultureName)
    {
        // Guard against null/empty and invalid culture names; use safe fallbacks
        var effectiveName = string.IsNullOrWhiteSpace(cultureName)
            ? CultureInfo.CurrentUICulture.Name
            : cultureName;

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(effectiveName);
        }
        catch (CultureNotFoundException)
        {
            // Try neutral language (e.g., "pl" from "pl-PL")
            try
            {
                var neutral = new CultureInfo(effectiveName).TwoLetterISOLanguageName;
                culture = CultureInfo.GetCultureInfo(neutral);
            }
            catch
            {
                // Final fallback to English to avoid crashes
                culture = CultureInfo.GetCultureInfo("en");
            }
        }

        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CurrentCulture = culture;

        // Update all resource managers with the new culture
        AddRecipePageResources.Culture = culture;
        ArchivePageResources.Culture = culture;
        HomePageResources.Culture = culture;
        IngredientFormPageResources.Culture = culture;
        IngredientsPageResources.Culture = culture;
        MealFormPageResources.Culture = culture;
        PlannerPageResources.Culture = culture;
        RecipesPageResources.Culture = culture;
        SettingsPageResources.Culture = culture;
        SetupWizardPageResources.Culture = culture; 
        ShoppingListDetailPageResources.Culture = culture;
        ShoppingListPageResources.Culture = culture;
        TabBarResources.Culture = culture;
        ButtonResources.Culture = culture;
        UnitResources.Culture = culture;

        System.Diagnostics.Debug.WriteLine($"[LocalizationService] Culture changed to: {culture.Name} (requested: '{cultureName}')");
        CultureChanged?.Invoke(this, EventArgs.Empty);
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
