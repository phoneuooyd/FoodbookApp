using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using System.Threading.Tasks;
using Foodbook.Models;
using CommunityToolkit.Mvvm.Messaging;

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

            // Register for FAB collapse message via WeakReferenceMessenger (replaces deprecated MessagingCenter)
            WeakReferenceMessenger.Default.Register<FabCollapseMessage>(this, async (_, __) =>
            {
                try
                {
                    var fab = this.FindByName<Foodbook.Views.Components.FloatingActionButtonComponent>("RecipesFab");
                    if (fab != null)
                    {
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            var method = fab.GetType().GetMethod("CollapseAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (method != null)
                            {
                                if (method.Invoke(fab, null) is Task t) await t;
                            }
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
                await _viewModel.LoadRecipesAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
            WeakReferenceMessenger.Default.Unregister<FabCollapseMessage>(this);
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
