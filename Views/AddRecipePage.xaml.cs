using Microsoft.Maui.Controls;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Models;
using System.Threading.Tasks;

namespace Foodbook.Views
{
    [QueryProperty(nameof(RecipeId), "id")]
    public partial class AddRecipePage : ContentPage
    {
        public IEnumerable<Foodbook.Models.Unit> Units => Enum.GetValues(typeof(Foodbook.Models.Unit)).Cast<Foodbook.Models.Unit>();

        private AddRecipeViewModel ViewModel => BindingContext as AddRecipeViewModel;
        private readonly ILocalizationService _localizationService;

        public AddRecipePage(AddRecipeViewModel vm, ILocalizationService localizationService)
        {
            InitializeComponent();
            BindingContext = vm;
            _localizationService = localizationService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            _localizationService.CultureChanged += OnCultureChanged;

            // Zawsze resetuj stan ViewModelu na początku
            ViewModel?.Reset();
            
            await ViewModel.LoadAvailableIngredientsAsync();
            
            // Tylko jeśli mamy RecipeId > 0, załaduj przepis do edycji
            if (RecipeId > 0)
                await ViewModel.LoadRecipeAsync(RecipeId);
        }

        private int _recipeId;
        public int RecipeId
        {
            get => _recipeId;
            set => _recipeId = value;
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _localizationService.CultureChanged -= OnCultureChanged;
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
            // Opóźnij przeliczenie o 500ms, żeby nie było ciągłych obliczeń podczas pisania
            var timer = Application.Current.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                ViewModel?.RecalculateNutritionalValues();
            };
            timer.Start();
        }

        private async void OnIngredientNameChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.BindingContext is Ingredient ingredient)
            {
                // Aktualizuj wartości odżywcze składnika na podstawie wybranej nazwy
                await ViewModel.UpdateIngredientNutritionalValuesAsync(ingredient);
            }
        }

        private void OnCultureChanged()
        {
            ViewModel?.RefreshTranslations();
        }
    }
}