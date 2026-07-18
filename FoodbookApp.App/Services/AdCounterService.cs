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
    {
        await _gate.WaitAsync();
        try
        {
            var counter = _preferences.GetRecipeManualSaveCounter() + 1;
            _preferences.SetRecipeManualSaveCounter(counter);

            if (!_policy.ShouldShowRecipeInterstitial(counter, _clock.UtcNow))
            {
                return;
            }

            if (!await _visibility.ShouldShowInterstitialAsync())
            {
                return;
            }

            if (await TryShowInterstitialWithoutThrowingAsync())
            {
                _preferences.SetRecipeManualSaveCounter(0);
                _policy.MarkInterstitialShown(_clock.UtcNow);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordManualIngredientSaveAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var counter = _preferences.GetIngredientManualSaveCounter() + 1;
            _preferences.SetIngredientManualSaveCounter(counter);

            if (!_policy.ShouldShowIngredientInterstitial(counter, _clock.UtcNow))
            {
                return;
            }

            if (!await _visibility.ShouldShowInterstitialAsync())
            {
                return;
            }

            if (await TryShowInterstitialWithoutThrowingAsync())
            {
                _preferences.SetIngredientManualSaveCounter(0);
                _policy.MarkInterstitialShown(_clock.UtcNow);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordManualPlanSaveAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var counter = _preferences.GetPlanManualSaveCounter() + 1;
            _preferences.SetPlanManualSaveCounter(counter);

            if (!_policy.ShouldShowPlanInterstitial(counter, _clock.UtcNow))
            {
                return;
            }

            if (!await _visibility.ShouldShowInterstitialAsync())
            {
                return;
            }

            if (await TryShowInterstitialWithoutThrowingAsync())
            {
                _preferences.SetPlanManualSaveCounter(0);
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
