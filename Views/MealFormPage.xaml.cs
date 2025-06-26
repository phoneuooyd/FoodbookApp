using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using System.Threading.Tasks;
using System;

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class MealFormPage : ContentPage
{
    private PlannedMealFormViewModel ViewModel => BindingContext as PlannedMealFormViewModel;

    public MealFormPage(PlannedMealFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private int _itemId;
    public int ItemId
    {
        get => _itemId;
        set
        {
            _itemId = value;
            if (value > 0)
                Task.Run(async () => await ViewModel.LoadAsync(value));
            else
                Task.Run(async () => await ViewModel.LoadRecipesAsync());
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
