namespace FoodbookApp.Interfaces;

public interface IAdPolicyService
{
    int RecipeInterstitialThreshold { get; }
    int IngredientInterstitialThreshold { get; }
    bool CanShowInterstitial(DateTime nowUtc);
    bool ShouldShowRecipeInterstitial(int recipeManualSaveCounter, DateTime nowUtc);
    bool ShouldShowIngredientInterstitial(int ingredientManualSaveCounter, DateTime nowUtc);
    void MarkInterstitialShown(DateTime nowUtc);
}
