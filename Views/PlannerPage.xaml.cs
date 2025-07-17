using Microsoft.Maui.Controls;
using Foodbook.ViewModels;

namespace Foodbook.Views
{
    public partial class PlannerPage : ContentPage
    {
        private readonly PlannerViewModel _viewModel;
        private bool _isInitialized;

        public PlannerPage(PlannerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            // Only load once or if explicitly needed
            if (!_isInitialized)
            {
                await _viewModel.LoadAsync();
                _isInitialized = true;
            }
            else
            {
                // If we're returning to the page, just reload if needed
                // This handles cases where data might have been modified
                await _viewModel.LoadAsync();
            }
        }

        protected override bool OnBackButtonPressed()
        {
            if (_viewModel?.CancelCommand?.CanExecute(null) == true)
                _viewModel.CancelCommand.Execute(null);
            return true;
        }
    }
}