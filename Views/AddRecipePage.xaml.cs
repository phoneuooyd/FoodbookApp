using Microsoft.Maui.Controls;
using Foodbook.Services;
using Foodbook.ViewModels;
using System.Threading.Tasks;

namespace Foodbook.Views
{
    [QueryProperty(nameof(RecipeId), "id")]
    public partial class AddRecipePage : ContentPage
    {
        public IEnumerable<Foodbook.Models.Unit> Units => Enum.GetValues(typeof(Foodbook.Models.Unit)).Cast<Foodbook.Models.Unit>();

        private AddRecipeViewModel ViewModel => BindingContext as AddRecipeViewModel;

        public AddRecipePage(AddRecipeViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ViewModel.LoadAvailableIngredientsAsync();
        }

        private int _recipeId;
        public int RecipeId
        {
            get => _recipeId;
            set
            {
                _recipeId = value;
                if (value > 0)
                    Task.Run(async () => await ViewModel.LoadRecipeAsync(value));
            }
        }

        protected override bool OnBackButtonPressed()
        {
            if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                ViewModel.CancelCommand.Execute(null);
            return true;
        }
    }
}