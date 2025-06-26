using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using System.Threading.Tasks;
<<<<<<< CRUD
using System;
=======
>>>>>>> main

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class IngredientFormPage : ContentPage
{
    private IngredientFormViewModel ViewModel => BindingContext as IngredientFormViewModel;

    public IngredientFormPage(IngredientFormViewModel vm)
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
        }
    }
<<<<<<< CRUD

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
=======
>>>>>>> main
}
