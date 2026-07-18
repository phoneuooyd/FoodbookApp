namespace FoodbookApp.Interfaces;

public interface IAdVisibilityService
{
    Task<bool> ShouldShowHomeBannerAsync();
    Task<bool> ShouldShowLoginBannerAsync();
    Task<bool> ShouldShowLoadingBannerAsync();
    Task<bool> ShouldShowInterstitialAsync();
}
