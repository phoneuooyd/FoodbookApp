using System;
using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Microsoft.Extensions.DependencyInjection;
using Foodbook.Data;
using FoodbookApp.Localization;

namespace Foodbook.Views
{
    public partial class SettingsPage : ContentPage
    {
        private const bool DiagnosticMode = false;
        public bool IsDiagnosticMode => DiagnosticMode;

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
                bool confirm = await DisplayAlert(
                    GetSettingsText("UpdateIngredientsTitle", "Update ingredients database"),
                    GetSettingsText("UpdateIngredientsDescription", "Add missing ingredients from embedded ingredients.json without overwriting existing items."),
                    GetButtonText("Yes", "Yes"),
                    GetButtonText("No", "No"));
                if (!confirm) return;

                if (FoodbookApp.MauiProgram.ServiceProvider == null)
                {
                    await DisplayAlert(
                        GetSettingsText("UpdateIngredientsErrorTitle", "Error"),
                        GetSettingsText("UpdateIngredientsServiceUnavailableMessage", "DI service unavailable."),
                        GetButtonText("OK", "OK"));
                    return;
                }

                using var scope = FoodbookApp.MauiProgram.ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                int added = await SeedData.AddMissingIngredientsFromJsonAsync(db);

                if (added > 0)
                    await DisplayAlert(
                        GetSettingsText("UpdateIngredientsSuccessTitle", "Success"),
                        string.Format(GetSettingsText("UpdateIngredientsSuccessMessage", "Added {0} new ingredients to the database."), added),
                        GetButtonText("OK", "OK"));
                else
                    await DisplayAlert(
                        GetSettingsText("UpdateIngredientsNoChangesTitle", "Information"),
                        GetSettingsText("UpdateIngredientsNoChangesMessage", "All ingredients from the file already exist in the database."),
                        GetButtonText("OK", "OK"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] OnUpdateIngredientsClicked error: {ex.Message}");
                await DisplayAlert(
                    GetSettingsText("UpdateIngredientsErrorTitle", "Error"),
                    string.Format(GetSettingsText("UpdateIngredientsErrorMessage", "Update failed: {0}"), ex.Message),
                    GetButtonText("OK", "OK"));
            }
        }
        private static string GetSettingsText(string key, string fallback)
            => SettingsPageResources.ResourceManager.GetString(key) ?? fallback;

        private static string GetButtonText(string key, string fallback)
            => ButtonResources.ResourceManager.GetString(key) ?? fallback;

    }
}