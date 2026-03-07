using Foodbook.ViewModels;
using Foodbook.Views.Base;

namespace Foodbook.Views;

public partial class FoodbookTemplatesPage : ContentPage
{
    private readonly PageThemeHelper _themeHelper;

    public FoodbookTemplatesPage(FoodbookTemplatesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _themeHelper = new PageThemeHelper();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _themeHelper.Initialize();

        if (BindingContext is FoodbookTemplatesViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _themeHelper.Cleanup();
    }
}
