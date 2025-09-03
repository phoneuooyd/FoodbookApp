using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    private readonly PageThemeHelper _themeHelper;

    public SettingsPage(SettingsViewModel vm)
    {
        BindingContext = vm;
        InitializeComponent();
        _themeHelper = new PageThemeHelper();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
    }
}