using Foodbook.Services;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services;

public sealed class AdPolicyService : IAdPolicyService
{
    private readonly IAdPreferencesService _preferences;

    public AdPolicyService(IAdPreferencesService preferences, IClock clock)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        ArgumentNullException.ThrowIfNull(clock);
    }

    public int RecipeInterstitialThreshold => 5;
    public int IngredientInterstitialThreshold => 3;
    public int PlanInterstitialThreshold => 3;
    public int FoodbookInterstitialThreshold => 3;
    public int ShoppingListInterstitialThreshold => 5;

    public bool CanShowInterstitial(DateTime nowUtc)
    {
        return true;
    }

    public bool ShouldShowRecipeInterstitial(int recipeManualSaveCounter, DateTime nowUtc)
        => recipeManualSaveCounter >= RecipeInterstitialThreshold && CanShowInterstitial(nowUtc);

    public bool ShouldShowIngredientInterstitial(int ingredientManualSaveCounter, DateTime nowUtc)
        => ingredientManualSaveCounter >= IngredientInterstitialThreshold && CanShowInterstitial(nowUtc);

    public bool ShouldShowPlanInterstitial(int planManualSaveCounter, DateTime nowUtc)
        => planManualSaveCounter >= PlanInterstitialThreshold && CanShowInterstitial(nowUtc);

    public bool ShouldShowFoodbookInterstitial(int foodbookManualSaveCounter, DateTime nowUtc)
        => foodbookManualSaveCounter >= FoodbookInterstitialThreshold && CanShowInterstitial(nowUtc);

    public bool ShouldShowShoppingListInterstitial(int shoppingListManualSaveCounter, DateTime nowUtc)
        => shoppingListManualSaveCounter >= ShoppingListInterstitialThreshold && CanShowInterstitial(nowUtc);

    public void MarkInterstitialShown(DateTime nowUtc)
        => _preferences.SetLastInterstitialShownUtc(nowUtc);
}
