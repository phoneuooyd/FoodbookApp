using Microsoft.Maui.Controls;
using Foodbook.ViewModels;

namespace Foodbook.Views
{
    public partial class PlannerPage : ContentPage
    {
        private readonly PlannerViewModel _viewModel;

        public PlannerPage(PlannerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            if (_viewModel?.CancelCommand?.CanExecute(null) == true)
                _viewModel.CancelCommand.Execute(null);
            return true;
        }
    }
}