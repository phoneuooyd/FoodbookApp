using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Data;

namespace Foodbook.Views;

public partial class IngredientsPage : ContentPage
{
    private readonly IngredientsViewModel _viewModel;
    private readonly AppDbContext _db;

    public IngredientsPage(IngredientsViewModel vm, AppDbContext db)
    {
        InitializeComponent();
        _viewModel = vm;
        _db = db;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
        if (_viewModel.Ingredients.Count == 0)
        {
            bool seed = await DisplayAlert("Brak składników", "Utworzyć listę przykładowych składników?", "Tak", "Nie");
            if (seed)
            {
                await SeedData.SeedIngredientsAsync(_db);
                await _viewModel.LoadAsync();
            }
        }
    }
}
