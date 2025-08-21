using Microsoft.Maui.Controls;
using Foodbook.ViewModels;

namespace Foodbook.Views
{
    public partial class PlannerPage : ContentPage
    {
        private readonly PlannerViewModel _viewModel;
        private bool _isInitialized;
        private bool _hasEverLoaded;

        public PlannerPage(PlannerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            if (!_hasEverLoaded)
            {
                // First time loading - always load fresh data
                System.Diagnostics.Debug.WriteLine("?? PlannerPage: First load - loading fresh data");
                await _viewModel.LoadAsync(forceReload: false);
                _hasEverLoaded = true;
                _isInitialized = true;
            }
            else
            {
                // Subsequent loads - use cached data if available
                System.Diagnostics.Debug.WriteLine("? PlannerPage: Using cached data");
                await _viewModel.LoadAsync(forceReload: false);
            }
        }

        protected override bool OnBackButtonPressed()
        {
            if (_viewModel?.CancelCommand?.CanExecute(null) == true)
                _viewModel.CancelCommand.Execute(null);
            return true;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            System.Diagnostics.Debug.WriteLine("?? PlannerPage: Disappearing - data remains cached");
        }
    }
}