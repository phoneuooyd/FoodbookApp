using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views
{
    public partial class ShoppingListPage : ContentPage
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
            
            await _viewModel.LoadPlansAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
        }
    }
}