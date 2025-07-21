using Microsoft.Maui.Controls;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(Foodbook.ViewModels.SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}