using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using System.Threading.Tasks;
using Foodbook.Models;
using FoodbookApp;

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class IngredientFormPage : ContentPage
{
    private IngredientFormViewModel ViewModel => BindingContext as IngredientFormViewModel;
    private readonly PageThemeHelper _themeHelper;

    public IngredientFormPage(IngredientFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _themeHelper = new PageThemeHelper();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Initialize theme and font handling
        _themeHelper.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Cleanup theme and font handling
        _themeHelper.Cleanup();
    }

    private int _itemId;
    public int ItemId
    {
        get => _itemId;
        set
        {
            try
            {
                _itemId = value;
                if (value > 0)
                    Task.Run(async () => await ViewModel.LoadAsync(value));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting ItemId: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B³¹d ³adowania sk³adnika: {ex.Message}";
                }
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        try
        {
            if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                ViewModel.CancelCommand.Execute(null);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnBackButtonPressed: {ex.Message}");
            return base.OnBackButtonPressed();
        }
    }
}
