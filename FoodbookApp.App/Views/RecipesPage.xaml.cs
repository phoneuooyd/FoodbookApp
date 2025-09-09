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

            MessagingCenter.Subscribe<RecipeViewModel>(this, "FabCollapse", async _ =>
            {
                try
                {
                    var fab = this.FindByName<Foodbook.Views.Components.FloatingActionButtonComponent>("RecipesFab");
                    if (fab != null)
                    {
                        // Use InvokeOnMainThread to ensure UI thread
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            var method = fab.GetType().GetMethod("CollapseAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (method != null)
                                await (Task)method.Invoke(fab, null);
                        });
                    }
                }
                catch { }
            });
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
                // Zamiast ReloadAsync (ustawia IsRefreshing) u¿ywamy ponownie LoadRecipesAsync
                // aby nie wchodziæ w stan ci¹g³ego odœwie¿ania pull-to-refresh.
                await _viewModel.LoadRecipesAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
            MessagingCenter.Unsubscribe<RecipeViewModel>(this, "FabCollapse");
        }
    }
}
