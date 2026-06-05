namespace Foodbook.Views.Components;

public partial class UniversalSpinnerComponent : ContentView
{
    private bool _spinnerVisible;

    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(UniversalSpinnerComponent), false,
            propertyChanged: OnStateChanged);

    public static readonly BindableProperty IsSavingProperty =
        BindableProperty.Create(nameof(IsSaving), typeof(bool), typeof(UniversalSpinnerComponent), false,
            propertyChanged: OnStateChanged);

    public static readonly BindableProperty LoadingStatusProperty =
        BindableProperty.Create(nameof(LoadingStatus), typeof(string), typeof(UniversalSpinnerComponent), "Loading...");

    public static readonly BindableProperty LoadingProgressProperty =
        BindableProperty.Create(nameof(LoadingProgress), typeof(double), typeof(UniversalSpinnerComponent), 0.0);

    public static readonly BindableProperty ShowProgressProperty =
        BindableProperty.Create(nameof(ShowProgress), typeof(bool), typeof(UniversalSpinnerComponent), false);

    public static readonly BindableProperty ShowTipProperty =
        BindableProperty.Create(nameof(ShowTip), typeof(bool), typeof(UniversalSpinnerComponent), false);

    public static readonly BindableProperty TipTextProperty =
        BindableProperty.Create(nameof(TipText), typeof(string), typeof(UniversalSpinnerComponent), string.Empty);

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
        set => SetValue(LoadingStatusProperty, value);
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
    }

    private static void OnStateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is UniversalSpinnerComponent self)
        {
            self.SpinnerVisible = self.IsLoading || self.IsSaving;
        }
    }
}
