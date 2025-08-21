using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using System.Threading.Tasks;
using Foodbook.Models;
using FoodbookApp;

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

    protected override bool OnBackButtonPressed()
    {
        if (ViewModel?.CancelCommand?.CanExecute(null) == true)
            ViewModel.CancelCommand.Execute(null);
        return true;
    }
}
