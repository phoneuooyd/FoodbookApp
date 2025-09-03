using Microsoft.Maui.Controls;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Models;
using Foodbook.Views.Base;
using System.Threading.Tasks;

namespace Foodbook.Views
{
    [QueryProperty(nameof(RecipeId), "id")]
    public partial class AddRecipePage : ContentPage
    {
        public IEnumerable<Foodbook.Models.Unit> Units => Enum.GetValues(typeof(Foodbook.Models.Unit)).Cast<Foodbook.Models.Unit>();

        private AddRecipeViewModel ViewModel => BindingContext as AddRecipeViewModel;
        private readonly PageThemeHelper _themeHelper;
        
        // ?? NOWE: Debouncing timer dla zmian wartoœci
        private IDispatcherTimer? _valueChangeTimer;

        public AddRecipePage(AddRecipeViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            _themeHelper = new PageThemeHelper();
        }

        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();
                
                // Initialize theme and font handling
                _themeHelper.Initialize();
                
                // Zawsze resetuj stan ViewModelu na pocz¹tku
                ViewModel?.Reset();
                
                await ViewModel.LoadAvailableIngredientsAsync();
                
                // Tylko jeœli mamy RecipeId > 0, za³aduj przepis do edycji
                if (RecipeId > 0)
                    await ViewModel.LoadRecipeAsync(RecipeId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B³¹d ³adowania strony: {ex.Message}";
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Cleanup theme and font handling
            _themeHelper.Cleanup();
        }

        private int _recipeId;
        public int RecipeId
        {
            get => _recipeId;
            set => _recipeId = value;
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

        private void OnAutoModeClicked(object sender, EventArgs e)
        {
            try
            {
                if (ViewModel != null)
                {
                    ViewModel.UseCalculatedValues = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnAutoModeClicked: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B³¹d prze³¹czania trybu: {ex.Message}";
                }
            }
        }

        private void OnManualModeClicked(object sender, EventArgs e)
        {
            try
            {
                if (ViewModel != null)
                {
                    ViewModel.UseCalculatedValues = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnManualModeClicked: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B³¹d prze³¹czania trybu: {ex.Message}";
                }
            }
        }

        // ?? ZOPTYMALIZOWANE: Debounced przeliczenia z u¿yciem nowej async metody
        private void OnIngredientValueChanged(object sender, EventArgs e)
        {
            try
            {
                // Anuluj poprzedni timer
                _valueChangeTimer?.Stop();
                
                // Utwórz nowy timer z 500ms opóŸnieniem
                _valueChangeTimer = Application.Current.Dispatcher.CreateTimer();
                _valueChangeTimer.Interval = TimeSpan.FromMilliseconds(500);
                _valueChangeTimer.Tick += async (s, args) =>
                {
                    try
                    {
                        _valueChangeTimer.Stop();
                        // ?? U¿ywa nowej asynchronicznej metody
                        if (ViewModel != null)
                        {
                            await ViewModel.RecalculateNutritionalValuesAsync();
                        }
                    }
                    catch (Exception timerEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in timer callback: {timerEx.Message}");
                    }
                };
                _valueChangeTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnIngredientValueChanged: {ex.Message}");
            }
        }

        private async void OnIngredientNameChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is Picker picker && picker.BindingContext is Ingredient ingredient)
                {
                    // Aktualizuj wartoœci od¿ywcze sk³adnika na podstawie wybranej nazwy
                    await ViewModel.UpdateIngredientNutritionalValuesAsync(ingredient);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnIngredientNameChanged: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B³¹d aktualizacji sk³adnika: {ex.Message}";
                }
            }
        }
    }
}