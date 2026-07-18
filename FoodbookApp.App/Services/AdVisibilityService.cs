using Foodbook.Models;
using FoodbookApp.Interfaces;

namespace FoodbookApp.Services;

public sealed class AdVisibilityService : IAdVisibilityService
{
    private readonly IFeatureAccessService _featureAccessService;
    private readonly IPreferencesService _preferencesService;
    private readonly IAdPreferencesService _adPreferencesService;

    public AdVisibilityService(
        IFeatureAccessService featureAccessService,
        IPreferencesService preferencesService,
        IAdPreferencesService adPreferencesService)
    {
        _featureAccessService = featureAccessService ?? throw new ArgumentNullException(nameof(featureAccessService));
        _preferencesService = preferencesService ?? throw new ArgumentNullException(nameof(preferencesService));
        _adPreferencesService = adPreferencesService ?? throw new ArgumentNullException(nameof(adPreferencesService));
    }

    public async Task<bool> ShouldShowHomeBannerAsync()
        => !await IsPremiumUserAsync() && _adPreferencesService.GetSupportDeveloper();

    public async Task<bool> ShouldShowLoginBannerAsync()
        => !await IsPremiumUserAsync();

    public async Task<bool> ShouldShowLoadingBannerAsync()
        => !await IsPremiumUserAsync();

    public async Task<bool> ShouldShowInterstitialAsync()
        => !await IsPremiumUserAsync();

    private async Task<bool> IsPremiumUserAsync()
    {
        if (IsPremiumPlanChoice(_preferencesService.GetPlanChoice()))
        {
            return true;
        }

        try
        {
            return await _featureAccessService.CanUsePremiumFeatureAsync(PremiumFeature.AutoPlanner);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdVisibilityService] Premium check failed: {ex.Message}");
            return false;
        }
    }

    private static bool IsPremiumPlanChoice(string? planChoice)
        => !string.IsNullOrWhiteSpace(planChoice)
           && planChoice.Trim().StartsWith("Premium", StringComparison.OrdinalIgnoreCase);
}
