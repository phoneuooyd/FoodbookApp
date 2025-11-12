using Foodbook.ViewModels;
using Foodbook.Views.Base;
using Microsoft.Maui.Controls;

namespace Foodbook.Views;

public partial class PlannerListsPage : ContentPage
{
    private readonly PageThemeHelper _themeHelper;

    public PlannerListsPage(PlannerListsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _themeHelper = new PageThemeHelper();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _themeHelper.Initialize();
        if (BindingContext is PlannerListsViewModel vm)
        {
            await vm.LoadPlansAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _themeHelper.Cleanup();
    }
}
