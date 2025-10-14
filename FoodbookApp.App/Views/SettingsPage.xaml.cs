using System;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using CommunityToolkit.Maui.Extensions;
using Foodbook.Views.Components;

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

        private async void OnManageLabelsClicked(object sender, EventArgs e)
        {
            if (BindingContext is not SettingsViewModel vm) return;
            var popup = new CRUDComponentPopup(vm);
            try
            {
                var hostPage = Application.Current?.Windows.FirstOrDefault()?.Page ?? this;
                await hostPage.ShowPopupAsync(popup);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] CRUDComponentPopup error: {ex.Message}");
            }
        }
    }
}