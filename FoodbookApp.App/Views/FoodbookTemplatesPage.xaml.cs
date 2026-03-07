using Foodbook.ViewModels;

namespace Foodbook.Views;

public partial class FoodbookTemplatesPage : ContentPage
{
    public FoodbookTemplatesPage(FoodbookTemplatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FoodbookTemplatesViewModel vm)
        {
            await vm.LoadAsync();
        }
    }
}
