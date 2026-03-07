using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class SetupWizardPage : ContentPage
{
    public SetupWizardPage(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is SetupWizardViewModel viewModel && viewModel.HandleBackNavigation())
        {
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
