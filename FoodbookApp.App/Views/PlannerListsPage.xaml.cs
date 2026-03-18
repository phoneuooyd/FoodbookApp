using Foodbook.ViewModels;
using Foodbook.Views.Base;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;

namespace Foodbook.Views;

public partial class PlannerListsPage : ContentPage, ITabLoadable
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

    public async Task OnTabActivatedAsync()
    {
        try
        {
            if (BindingContext is PlannerListsViewModel vm)
                await vm.LoadPlansAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlannerListsPage] OnTabActivatedAsync error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _themeHelper.Cleanup();
    }
}
