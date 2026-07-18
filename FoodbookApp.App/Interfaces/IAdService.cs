namespace FoodbookApp.Interfaces;

public interface IAdService
{
    bool IsAdLoaded { get; }
    event EventHandler? InterstitialAdLoaded;
    Task InitializeAsync();
    void PreloadInterstitial();
    Task<bool> TryShowInterstitialAsync();
}
