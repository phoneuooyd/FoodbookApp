using FoodbookApp.Interfaces;
using FoodbookApp.Services;
using Plugin.AdMob;

namespace Foodbook.Views.Components;

public enum AdBannerPlacement
{
    Home,
    Login,
    Loading
}

public partial class BannerAdView : ContentView
{
    public static readonly BindableProperty PlacementProperty =
        BindableProperty.Create(nameof(Placement), typeof(AdBannerPlacement), typeof(BannerAdView), AdBannerPlacement.Home);

    public static readonly BindableProperty MaxHeightRatioProperty =
        BindableProperty.Create(nameof(MaxHeightRatio), typeof(double), typeof(BannerAdView), 0.08d);

    public static readonly BindableProperty PreferredHeightProperty =
        BindableProperty.Create(nameof(PreferredHeight), typeof(double), typeof(BannerAdView), 64d);

    public static readonly BindableProperty AdUnitIdProperty =
        BindableProperty.Create(nameof(AdUnitId), typeof(string), typeof(BannerAdView), AdUnitIds.HomeBanner);

    public AdBannerPlacement Placement
    {
        get => (AdBannerPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public double MaxHeightRatio
    {
        get => (double)GetValue(MaxHeightRatioProperty);
        set => SetValue(MaxHeightRatioProperty, value);
    }

    public double PreferredHeight
    {
        get => (double)GetValue(PreferredHeightProperty);
        set => SetValue(PreferredHeightProperty, value);
    }

    public string AdUnitId
    {
        get => (string)GetValue(AdUnitIdProperty);
        set => SetValue(AdUnitIdProperty, value);
    }

    public BannerAdView()
    {
        InitializeComponent();
        AdControl.OnAdLoaded += OnAdLoaded;
        AdControl.OnAdFailedToLoad += OnAdFailedToLoad;
        Loaded += async (_, _) => await RefreshVisibilityAsync();
    }

    private void OnAdLoaded(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[BannerAdView] Ad loaded");
    }

    private void OnAdFailedToLoad(object? sender, IAdError e)
    {
        System.Diagnostics.Debug.WriteLine($"[BannerAdView] Ad failed: {e.Message}");
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsVisible = false;
            Panel.IsVisible = false;
        });
    }

    public async Task RefreshVisibilityAsync()
    {
        try
        {
            var visibility = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IAdVisibilityService>();
            var shouldShow = visibility is not null && Placement switch
            {
                AdBannerPlacement.Login => await visibility.ShouldShowLoginBannerAsync(),
                AdBannerPlacement.Loading => await visibility.ShouldShowLoadingBannerAsync(),
                _ => await visibility.ShouldShowHomeBannerAsync()
            };

            if (shouldShow)
            {
                AdUnitId = Placement switch
                {
                    AdBannerPlacement.Login => AdUnitIds.LoginBanner,
                    AdBannerPlacement.Loading => AdUnitIds.LoadingBanner,
                    _ => AdUnitIds.HomeBanner
                };
                ApplyReservedHeight();
            }

            IsVisible = shouldShow;
            Panel.IsVisible = shouldShow;
            AdControl.IsVisible = shouldShow;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BannerAdView] Visibility refresh failed: {ex.Message}");
            IsVisible = false;
        }
    }

    private void ApplyReservedHeight()
    {
        var display = DeviceDisplay.MainDisplayInfo;
        var screenHeightDp = display.Height / Math.Max(display.Density, 1);
        var maxHeight = Math.Max(50, screenHeightDp * MaxHeightRatio);
        var height = Math.Min(PreferredHeight, maxHeight);

        HeightRequest = height;
        Panel.HeightRequest = height;
    }
}
