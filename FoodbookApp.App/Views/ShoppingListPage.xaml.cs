using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using FoodbookApp.Interfaces;

namespace Foodbook.Views
{
    public partial class ShoppingListPage : ContentPage, ITabLoadable
    {
        private readonly ShoppingListViewModel _viewModel;
        private readonly PageThemeHelper _themeHelper;

        public ShoppingListPage(ShoppingListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            _themeHelper = new PageThemeHelper();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Initialize theme and font handling
            _themeHelper.Initialize();
            _viewModel.StartListening();
            
            await _viewModel.LoadPlansAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
            _viewModel.StopListening();
        }

        /// <summary>
        /// Called by TabBarComponent when this tab is activated.
        /// </summary>
        public async Task OnTabActivatedAsync()
        {
            try
            {
                _viewModel.StartListening();
                await _viewModel.LoadPlansAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShoppingListPage] OnTabActivatedAsync error: {ex.Message}");
            }
        }
    }
}