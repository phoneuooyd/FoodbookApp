using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Services;

namespace Foodbook.Views
{
    public partial class RecipesPage : ContentPage
    {
        private readonly RecipeViewModel _viewModel;

        public RecipesPage(RecipeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadRecipesAsync();
        }

        private async void OnAddRecipeClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///AddRecipePage");
        }
    }
}
