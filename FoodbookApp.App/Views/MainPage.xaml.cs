using Microsoft.Maui.Controls;
using Foodbook.Views.Components;
using FoodbookApp;
using FoodbookApp.Interfaces;
using System.Threading.Tasks;
using System;

namespace Foodbook.Views
{
    /// <summary>
    /// Main page that hosts the custom TabBarComponent.
    /// Ensures Home tab is selected on startup and data loads correctly.
    /// </summary>
    public partial class MainPage : ContentPage
    {
        private ILocalizationService? _localizationService;
        private bool _hasInitialized = false;

        public MainPage()
        {
            InitializeComponent();
            _localizationService = MauiProgram.ServiceProvider?.GetService<ILocalizationService>();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Ensure Home tab is selected ONLY on initial app start, not after navigating back from Shell pages
            if (!_hasInitialized)
            {
                TrySelectHomeTab();
                _hasInitialized = true;
            }
        }

        private void TrySelectHomeTab()
        {
            try
            {
                if (MainTabBar?.TabItems == null || MainTabBar.TabItems.Count == 0) return;
                var home = MainTabBar.TabItems.FirstOrDefault(t => t.Route == "HomeTab");
                if (home != null && MainTabBar.SelectedTab != home)
                {
                    MainTabBar.SelectedTab = home;
                }
            }
            catch { }
        }

        protected override bool OnBackButtonPressed()
        {
            // Delegate to TabBarComponent's enhanced back button handler asynchronously
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await MainTabBar.HandleBackButtonAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Back handling error: {ex.Message}");
                }
            });
            return true; // consume back press immediately
        }

        /// <summary>
        /// Get the TabBar component for external navigation
        /// </summary>
        public TabBarComponent TabBar => MainTabBar;
    }
}
