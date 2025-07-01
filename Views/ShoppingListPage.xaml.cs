using Microsoft.Maui.Controls;
using Foodbook.ViewModels;

namespace Foodbook.Views
{
    public partial class ShoppingListPage : ContentPage
    {
        private readonly ShoppingListViewModel _viewModel;

        public ShoppingListPage(ShoppingListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadPlansAsync();
        }

        private async void OnGoToPlannerClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync($"//{nameof(PlannerPage)}");
        }
    }
}