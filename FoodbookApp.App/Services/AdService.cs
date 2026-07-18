using FoodbookApp.Interfaces;
using Plugin.AdMob.Services;

namespace FoodbookApp.Services;

public sealed class AdService : IAdService
{
    private IInterstitialAdService? _interstitial;
    private bool _initialized;

    public bool IsAdLoaded => _interstitial?.IsAdLoaded ?? false;

    public event EventHandler? InterstitialAdLoaded;

    public AdService()
    {
        _ = InitializeAsync();
    }

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;

        System.Diagnostics.Debug.WriteLine("[AdService] Init via Plugin.AdMob");
        try
        {
            var s = IPlatformApplication.Current!.Services!;
            _interstitial = s.GetRequiredService<IInterstitialAdService>();
            _interstitial.OnAdLoaded += (_, _) =>
            {
                System.Diagnostics.Debug.WriteLine("[AdService] Interstitial loaded");
                InterstitialAdLoaded?.Invoke(this, EventArgs.Empty);
            };
            PreloadInterstitial();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdService] Init error: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public void PreloadInterstitial()
    {
        if (_interstitial == null) return;
        try
        {
            _interstitial.PrepareAd(AdUnitIds.Interstitial);
            System.Diagnostics.Debug.WriteLine("[AdService] Preload");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdService] Preload error: {ex.Message}");
        }
    }

    public async Task<bool> TryShowInterstitialAsync()
    {
        if (_interstitial == null || !_interstitial.IsAdLoaded)
            return false;

        try
        {
            _interstitial.ShowAd();
            PreloadInterstitial();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AdService] Show error: {ex.Message}");
            return false;
        }
    }
}
