using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views
{
    public partial class PlannerPage : ContentPage
    {
        private readonly PlannerViewModel _viewModel;
        private readonly PageThemeHelper _themeHelper;
        private bool _isInitialized;
        private bool _hasEverLoaded;

        public PlannerPage(PlannerViewModel viewModel)
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
                // Do not auto-refresh on subsequent appearances (e.g., after popup close)
                System.Diagnostics.Debug.WriteLine("?? PlannerPage: Skipping auto refresh on re-appear");
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
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
            
            System.Diagnostics.Debug.WriteLine("?? PlannerPage: Disappearing - data remains cached");
        }
    }
}