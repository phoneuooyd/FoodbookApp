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
            
            // Zawsze resetuj stan ViewModelu na pocz¹tku
            ViewModel?.Reset();
            
            await ViewModel.LoadAvailableIngredientsAsync();
            
            // Tylko jeœli mamy RecipeId > 0, za³aduj przepis do edycji
            if (RecipeId > 0)
                await ViewModel.LoadRecipeAsync(RecipeId);
        }

        private int _recipeId;
        public int RecipeId
        {
            get => _recipeId;
            set => _recipeId = value;
        }

        protected override bool OnBackButtonPressed()
        {
            if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                ViewModel.CancelCommand.Execute(null);
            return true;
        }

        private void OnAutoModeClicked(object sender, EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UseCalculatedValues = true;
            }
        }

        private void OnManualModeClicked(object sender, EventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.UseCalculatedValues = false;
            }
        }

        private void OnIngredientValueChanged(object sender, EventArgs e)
        {
            // OpóŸnij przeliczenie o 500ms, ¿eby nie by³o ci¹g³ych obliczeñ podczas pisania
            var timer = Application.Current.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                ViewModel?.RecalculateNutritionalValues();
            };
            timer.Start();
        }
    }
}