using Foodbook.Services;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services;

public sealed class AdCounterService : IAdCounterService
{
    private readonly IAdPreferencesService _preferences;
    private readonly IAdPolicyService _policy;
    private readonly IAdVisibilityService _visibility;
    private readonly IAdService _adService;
    private readonly IClock _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AdCounterService(
        IAdPreferencesService preferences,
        IAdPolicyService policy,
        IAdVisibilityService visibility,
        IAdService adService,
        IClock clock)
    {
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
        _adService = adService ?? throw new ArgumentNullException(nameof(adService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task RecordManualRecipeSaveAsync()
        => await RecordManualSaveAsync(
            _preferences.GetRecipeManualSaveCounter,
            _preferences.SetRecipeManualSaveCounter,
            counter => _policy.ShouldShowRecipeInterstitial(counter, _clock.UtcNow));

    public async Task RecordManualIngredientSaveAsync()
        => await RecordManualSaveAsync(
            _preferences.GetIngredientManualSaveCounter,
            _preferences.SetIngredientManualSaveCounter,
            counter => _policy.ShouldShowIngredientInterstitial(counter, _clock.UtcNow));

    public async Task RecordManualPlanSaveAsync()
        => await RecordManualSaveAsync(
            _preferences.GetPlanManualSaveCounter,
            _preferences.SetPlanManualSaveCounter,
            counter => _policy.ShouldShowPlanInterstitial(counter, _clock.UtcNow));

    public async Task RecordManualFoodbookSaveAsync()
        => await RecordManualSaveAsync(
            _preferences.GetFoodbookManualSaveCounter,
            _preferences.SetFoodbookManualSaveCounter,
            counter => _policy.ShouldShowFoodbookInterstitial(counter, _clock.UtcNow));

    public async Task RecordManualShoppingListSaveAsync()
        => await RecordManualSaveAsync(
            _preferences.GetShoppingListManualSaveCounter,
            _preferences.SetShoppingListManualSaveCounter,
            counter => _policy.ShouldShowShoppingListInterstitial(counter, _clock.UtcNow));

    private async Task RecordManualSaveAsync(
        Func<int> getCounter,
        Action<int> setCounter,
        Func<int, bool> shouldShowWhenSupportIsDisabled)
    {
        await _gate.WaitAsync();
        try
        {
            var counter = getCounter() + 1;
            setCounter(counter);

            var shouldShow = _preferences.GetSupportDeveloper()
                || shouldShowWhenSupportIsDisabled(counter);

            if (!shouldShow)
            {
                return;
            }

            if (!await _visibility.ShouldShowInterstitialAsync())
            {
                return;
            }

            if (await TryShowInterstitialWithoutThrowingAsync())
            {
                setCounter(0);
                _policy.MarkInterstitialShown(_clock.UtcNow);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> TryShowInterstitialWithoutThrowingAsync()
    {
        try
        {
            return await _adService.TryShowInterstitialAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdCounterService] Interstitial failed: {ex.Message}");
            return false;
        }
    }
}
