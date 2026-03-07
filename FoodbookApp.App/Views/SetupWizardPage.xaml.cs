using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class SetupWizardPage : ContentPage
{
    private readonly SetupWizardViewModel _viewModel;

    public SetupWizardPage(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.CanGoBack)
        {
            _viewModel.MoveToPreviousStep();
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
