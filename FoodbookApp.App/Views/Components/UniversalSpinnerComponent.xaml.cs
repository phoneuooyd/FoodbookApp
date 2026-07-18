using FoodbookApp.Interfaces;

namespace Foodbook.Views.Components;

public partial class UniversalSpinnerComponent : ContentView
{
    private bool _spinnerVisible;
    private ILocalizationService? _localizationService;
    private bool _statusExplicitlySet;
    private bool _settingInternally;
    private string _lastStatusKey = string.Empty;

    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(UniversalSpinnerComponent), false,
            propertyChanged: OnStateChanged);

    public static readonly BindableProperty IsSavingProperty =
        BindableProperty.Create(nameof(IsSaving), typeof(bool), typeof(UniversalSpinnerComponent), false,
            propertyChanged: OnStateChanged);

    public static readonly BindableProperty LoadingStatusProperty =
        BindableProperty.Create(nameof(LoadingStatus), typeof(string), typeof(UniversalSpinnerComponent), string.Empty,
            propertyChanged: OnLoadingStatusChanged);

    public static readonly BindableProperty LoadingProgressProperty =
        BindableProperty.Create(nameof(LoadingProgress), typeof(double), typeof(UniversalSpinnerComponent), 0.0);

    public static readonly BindableProperty ShowProgressProperty =
        BindableProperty.Create(nameof(ShowProgress), typeof(bool), typeof(UniversalSpinnerComponent), false);

    public static readonly BindableProperty ShowTipProperty =
        BindableProperty.Create(nameof(ShowTip), typeof(bool), typeof(UniversalSpinnerComponent), false);

    public static readonly BindableProperty TipTextProperty =
        BindableProperty.Create(nameof(TipText), typeof(string), typeof(UniversalSpinnerComponent), string.Empty);

    public static readonly BindableProperty ShowAdBannerProperty =
        BindableProperty.Create(nameof(ShowAdBanner), typeof(bool), typeof(UniversalSpinnerComponent), false,
            propertyChanged: OnStateChanged);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public bool IsSaving
    {
        get => (bool)GetValue(IsSavingProperty);
        set => SetValue(IsSavingProperty, value);
    }

    public string LoadingStatus
    {
        get => (string)GetValue(LoadingStatusProperty);
        set
        {
            SetValue(LoadingStatusProperty, value);
            _statusExplicitlySet = true;
        }
    }

    public double LoadingProgress
    {
        get => (double)GetValue(LoadingProgressProperty);
        set => SetValue(LoadingProgressProperty, value);
    }

    public bool ShowProgress
    {
        get => (bool)GetValue(ShowProgressProperty);
        set => SetValue(ShowProgressProperty, value);
    }

    public bool ShowTip
    {
        get => (bool)GetValue(ShowTipProperty);
        set => SetValue(ShowTipProperty, value);
    }

    public string TipText
    {
        get => (string)GetValue(TipTextProperty);
        set => SetValue(TipTextProperty, value);
    }

    public bool ShowAdBanner
    {
        get => (bool)GetValue(ShowAdBannerProperty);
        set => SetValue(ShowAdBannerProperty, value);
    }

    public bool SpinnerVisible
    {
        get => _spinnerVisible;
        private set
        {
            if (_spinnerVisible == value) return;
            _spinnerVisible = value;
            OnPropertyChanged();
        }
    }

    public UniversalSpinnerComponent()
    {
        InitializeComponent();
        SubscribeLocalization();
    }

    private void SubscribeLocalization()
    {
        try
        {
            _localizationService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
            if (_localizationService != null)
            {
                _localizationService.CultureChanged += OnCultureChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UniversalSpinnerComponent] Failed to subscribe localization: {ex.Message}");
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (_statusExplicitlySet) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(_lastStatusKey)) return;

                _settingInternally = true;
                try
                {
                    SetValue(LoadingStatusProperty, GetLocalized(_lastStatusKey));
                }
                finally
                {
                    _settingInternally = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UniversalSpinnerComponent] OnCultureChanged error: {ex.Message}");
            }
        });
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not UniversalSpinnerComponent self) return;

        self.SpinnerVisible = self.IsLoading || self.IsSaving;
        self.RefreshLoadingBanner();

        if (self._statusExplicitlySet) return;

        if (self.IsLoading)
        {
            self._lastStatusKey = "Loading";
            self._settingInternally = true;
            try
            {
                self.SetValue(LoadingStatusProperty, self.GetLocalized("Loading"));
            }
            finally
            {
                self._settingInternally = false;
            }
        }
        else if (self.IsSaving)
        {
            self._lastStatusKey = "Saving";
            self._settingInternally = true;
            try
            {
                self.SetValue(LoadingStatusProperty, self.GetLocalized("Saving"));
            }
            finally
            {
                self._settingInternally = false;
            }
        }
    }

    private void RefreshLoadingBanner()
    {
        if (!SpinnerVisible || !ShowAdBanner)
        {
            LoadingBanner.IsVisible = false;
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await LoadingBanner.RefreshVisibilityAsync());
    }

    private static void OnLoadingStatusChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not UniversalSpinnerComponent self) return;
        if (self._settingInternally) return;
        if (newValue is string s && !string.IsNullOrEmpty(s))
        {
            self._statusExplicitlySet = true;
        }
    }

    private string GetLocalized(string key)
    {
        if (_localizationService == null) return key;
        var value = _localizationService.GetString("UniversalSpinnerComponentResources", key);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}
