using Foodbook.Services;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services;

public sealed class AdPolicyService : IAdPolicyService
{
    private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InterstitialCooldown = TimeSpan.FromSeconds(60);

    private readonly IAdPreferencesService _preferences;
    private readonly IClock _clock;
    private readonly DateTime _appStartedUtc;

    public AdPolicyService(IAdPreferencesService preferences, IClock clock)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _appStartedUtc = _clock.UtcNow;
    }

    public int RecipeInterstitialThreshold => 3;
    public int IngredientInterstitialThreshold => 5;
    public int PlanInterstitialThreshold => 3;

    public bool CanShowInterstitial(DateTime nowUtc)
    {
        if (nowUtc - _appStartedUtc < StartupGracePeriod)
        {
            return false;
        }

        var lastShown = _preferences.GetLastInterstitialShownUtc();
        return lastShown is null || nowUtc - lastShown.Value >= InterstitialCooldown;
    }

    public bool ShouldShowRecipeInterstitial(int recipeManualSaveCounter, DateTime nowUtc)
        => recipeManualSaveCounter >= RecipeInterstitialThreshold && CanShowInterstitial(nowUtc);

    public bool ShouldShowIngredientInterstitial(int ingredientManualSaveCounter, DateTime nowUtc)
        => ingredientManualSaveCounter >= IngredientInterstitialThreshold && CanShowInterstitial(nowUtc);

    public bool ShouldShowPlanInterstitial(int planManualSaveCounter, DateTime nowUtc)
        => planManualSaveCounter >= PlanInterstitialThreshold && CanShowInterstitial(nowUtc);

    public void MarkInterstitialShown(DateTime nowUtc)
        => _preferences.SetLastInterstitialShownUtc(nowUtc);
}
