using Microsoft.Maui.Controls;
using Foodbook.ViewModels;

namespace Foodbook.Views;

[QueryProperty(nameof(PlanId), "id")]
public partial class ShoppingListDetailPage : ContentPage
{
    private readonly ShoppingListDetailViewModel _viewModel;

    public int PlanId { get; set; }

    public ShoppingListDetailPage(ShoppingListDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync(PlanId);
    }
}
