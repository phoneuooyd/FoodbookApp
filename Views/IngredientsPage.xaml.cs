using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Data;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp;

namespace Foodbook.Views;

public partial class IngredientsPage : ContentPage
{
    private readonly IngredientsViewModel _viewModel;

    public IngredientsPage(IngredientsViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();

        if (_viewModel.Ingredients.Count == 0)
        {
            bool create = await DisplayAlert("Brak składników", "Utworzyć listę przykładowych składników?", "Tak", "Nie");
            if (create && MauiProgram.ServiceProvider != null)
            {
                using var scope = MauiProgram.ServiceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await SeedData.SeedIngredientsAsync(db);
                await _viewModel.LoadAsync();
            }
        }
    }
}
