using Microsoft.Maui.Controls;
using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        BindingContext = vm;
        InitializeComponent();
    }
}