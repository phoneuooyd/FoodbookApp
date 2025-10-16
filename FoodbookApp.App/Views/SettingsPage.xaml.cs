using System;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly PageThemeHelper _themeHelper;

        public SettingsPage(SettingsViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            _themeHelper = new PageThemeHelper();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _themeHelper.Initialize();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _themeHelper.Cleanup();
        }

        private async void OnOpenArchivizationClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PushAsync(new DataArchivizationPage(
                FoodbookApp.MauiProgram.ServiceProvider.GetRequiredService<FoodbookApp.Interfaces.IDatabaseService>(),
                FoodbookApp.MauiProgram.ServiceProvider.GetRequiredService<FoodbookApp.Interfaces.IPreferencesService>()
            ));
        }
    }
}