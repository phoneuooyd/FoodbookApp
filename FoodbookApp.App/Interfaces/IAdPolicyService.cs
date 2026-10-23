namespace FoodbookApp.Interfaces;

public interface IAdPolicyService
{
    int RecipeInterstitialThreshold { get; }
    int IngredientInterstitialThreshold { get; }
    int PlanInterstitialThreshold { get; }
    int FoodbookInterstitialThreshold { get; }
    int ShoppingListInterstitialThreshold { get; }
    bool CanShowInterstitial(DateTime nowUtc);
    bool ShouldShowRecipeInterstitial(int recipeManualSaveCounter, DateTime nowUtc);
    bool ShouldShowIngredientInterstitial(int ingredientManualSaveCounter, DateTime nowUtc);
    bool ShouldShowPlanInterstitial(int planManualSaveCounter, DateTime nowUtc);
    bool ShouldShowFoodbookInterstitial(int foodbookManualSaveCounter, DateTime nowUtc);
    bool ShouldShowShoppingListInterstitial(int shoppingListManualSaveCounter, DateTime nowUtc);
    void MarkInterstitialShown(DateTime nowUtc);
}
