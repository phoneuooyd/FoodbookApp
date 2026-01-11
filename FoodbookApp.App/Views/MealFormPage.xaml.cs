using Microsoft.Maui.Controls;
using Foodbook.ViewModels;
using Foodbook.Views.Base;
using System.Threading.Tasks;

namespace Foodbook.Views;

[QueryProperty(nameof(ItemId), "id")]
public partial class MealFormPage : ContentPage
{
    private PlannedMealFormViewModel ViewModel => BindingContext as PlannedMealFormViewModel;
    private readonly PageThemeHelper _themeHelper;

    public MealFormPage(PlannedMealFormViewModel vm)
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

    private Guid _itemId;
    public Guid ItemId
    {
        get => _itemId;
        set
        {
            _itemId = value;
            if (value != Guid.Empty)
                Task.Run(async () => await ViewModel.LoadAsync(value));
            else
                Task.Run(async () => await ViewModel.LoadRecipesAsync());
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (ViewModel?.CancelCommand?.CanExecute(null) == true)
            ViewModel.CancelCommand.Execute(null);
        return true;
    }
}
