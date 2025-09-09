using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using System.Threading.Tasks;
using Foodbook.Models;

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

        private void OnBackClicked(object? sender, System.EventArgs e)
        {
            if (_viewModel.GoBackCommand?.CanExecute(null) == true)
                _viewModel.GoBackCommand.Execute(null);
        }

        // Handle dropping a recipe onto the back button: move up one level
        private async void OnBackDrop(object? sender, DropEventArgs e)
        {
            try
            {
                if (e?.Data?.Properties?.TryGetValue("SourceItem", out var source) == true && source is Recipe recipe)
                {
                    await _viewModel.MoveRecipeUpAsync(recipe);
                }
            }
            catch
            {
                // ignore errors
            }
        }

        // Drop anywhere on breadcrumb area moves up one level
        private async void OnBreadcrumbDrop(object? sender, DropEventArgs e)
        {
            try
            {
                if (e?.Data?.Properties?.TryGetValue("SourceItem", out var source) == true && source is Recipe recipe)
                {
                    await _viewModel.MoveRecipeUpAsync(recipe);
                }
            }
            catch { }
        }
    }
}
