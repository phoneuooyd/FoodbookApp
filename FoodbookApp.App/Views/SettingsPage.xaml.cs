using System;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;

namespace Foodbook.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly PageThemeHelper _themeHelper;

        private SettingsViewModel ViewModel => BindingContext as SettingsViewModel;

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
            _themeHelper.ThemeChanged += OnThemeChanged;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _themeHelper.ThemeChanged -= OnThemeChanged;
            _themeHelper.Cleanup();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel == null) return;
                // Re-raise SelectedTabIndex to force BoolToColorConverter to recompute with new palette
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ViewModel.SelectedTabIndex = ViewModel.SelectedTabIndex;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnThemeChanged error: {ex.Message}");
            }
        }

        private async void OnOpenArchivizationClicked(object sender, EventArgs e)
        {
            await Shell.Current.Navigation.PushAsync(new DataArchivizationPage(
                FoodbookApp.MauiProgram.ServiceProvider.GetRequiredService<FoodbookApp.Interfaces.IDatabaseService>(),
                FoodbookApp.MauiProgram.ServiceProvider.GetRequiredService<FoodbookApp.Interfaces.IPreferencesService>()
            ));
        }

        private async void OnUpdateIngredientsClicked(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert("Aktualizacja sk³adników", "Czy na pewno chcesz za³adowaæ/aktualizowaæ bazê sk³adników z pliku ingredients.json?", "Tak", "Nie");
                if (!confirm) return;

                if (FoodbookApp.MauiProgram.ServiceProvider == null)
                {
                    await DisplayAlert("B³¹d", "Serwis DI niedostêpny.", "OK");
                    return;
                }

                using var scope = FoodbookApp.MauiProgram.ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await SeedData.SeedIngredientsAsync(db);

                await DisplayAlert("Sukces", "Baza sk³adników zosta³a zaktualizowana.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnUpdateIngredientsClicked error: {ex.Message}");
                await DisplayAlert("B³¹d", $"Aktualizacja nie powiod³a siê: {ex.Message}", "OK");
            }
        }
    }
}