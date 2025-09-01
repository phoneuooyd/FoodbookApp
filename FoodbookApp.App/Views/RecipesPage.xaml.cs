using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views
{
    public partial class RecipesPage : ContentPage
    {
        private readonly RecipeViewModel _viewModel;
        private readonly PageThemeHelper _themeHelper;
        private bool _isInitialized;

        public RecipesPage(RecipeViewModel viewModel)
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
            
            // Only load once or if explicitly needed
            if (!_isInitialized)
            {
                await _viewModel.LoadRecipesAsync();
                _isInitialized = true;
            }
            else
            {
                // If we're returning to the page, just refresh if needed
                // This handles cases where recipes might have been added/modified
                await _viewModel.ReloadAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
        }
    }
}
