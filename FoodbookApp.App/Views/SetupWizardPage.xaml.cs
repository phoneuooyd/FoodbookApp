using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class SetupWizardPage : ContentPage
{
    public SetupWizardPage(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}