using Microsoft.Maui.Controls;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Models;
using Foodbook.Views.Base;
using System.Threading.Tasks;
using Foodbook.Views.Components;
using FoodbookApp; // added for MauiProgram
using CommunityToolkit.Maui.Extensions; // for ShowPopupAsync

namespace Foodbook.Views
{
    [QueryProperty(nameof(RecipeId), "id")]
    [QueryProperty(nameof(FolderId), "folderId")]
    public partial class AddRecipePage : ContentPage
    {
        private AddRecipeViewModel? ViewModel => BindingContext as AddRecipeViewModel;
        private readonly PageThemeHelper _themeHelper;
        
        private IDispatcherTimer? _valueChangeTimer;
        private bool _isInitialized;
        private bool _hasEverLoaded;

        public AddRecipePage(AddRecipeViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            _themeHelper = new PageThemeHelper();
        }

        // New: open labels popup and refresh labels afterwards
        private async void OnAddLabelClicked(object sender, System.EventArgs e)
        {
            try
            {
                // SettingsViewModel contains labels management logic (singleton)
                var settingsVm = MauiProgram.ServiceProvider?.GetService(typeof(SettingsViewModel)) as SettingsViewModel;
                if (settingsVm == null)
                    return;
                var popup = new CRUDComponentPopup(settingsVm);
                var hostPage = Application.Current?.Windows.FirstOrDefault()?.Page as ContentPage ?? this;
                await hostPage.ShowPopupAsync(popup);

                // After popup closes reload labels & sync selection by Id
                if (ViewModel != null)
                {
                    await ViewModel.LoadAvailableLabelsAsync();
                    // Ensure SelectedLabels contains only those still existing (avoid stale references)
                    var selectedIds = ViewModel.SelectedLabels.Select(l => l.Id).ToHashSet();
                    ViewModel.SelectedLabels.Clear();
                    foreach (var lbl in ViewModel.AvailableLabels)
                        if (selectedIds.Contains(lbl.Id)) ViewModel.SelectedLabels.Add(lbl);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnAddLabelClicked error: {ex.Message}");
            }
        }

        protected override async void OnAppearing()
        {
            try
            {
                base.OnAppearing();
                _themeHelper.Initialize();
                _themeHelper.ThemeChanged += OnThemeChanged;

                if (!_hasEverLoaded)
                {
                    // First time loading - perform full initialization
                    System.Diagnostics.Debug.WriteLine("?? AddRecipePage: First load - performing full initialization");
                    
                    ViewModel?.Reset();
                    await (ViewModel?.LoadAvailableIngredientsAsync() ?? Task.CompletedTask);
                    await (ViewModel?.LoadAvailableLabelsAsync() ?? Task.CompletedTask);

                    if (RecipeId > 0 && ViewModel != null)
                        await ViewModel.LoadRecipeAsync(RecipeId);

                    if (FolderId > 0 && ViewModel != null)
                        ViewModel.SelectedFolderId = FolderId;
                        
                    _hasEverLoaded = true;
                    _isInitialized = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("?? AddRecipePage: Skipping reset on re-appear");
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B??d ?adowania strony: {ex.Message}";
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _themeHelper.ThemeChanged -= OnThemeChanged;
            _themeHelper.Cleanup();
            
            System.Diagnostics.Debug.WriteLine("?? AddRecipePage: Disappearing - preserving current state");
        }

        private void OnThemeChanged(object? sender, System.EventArgs e)
        {
            try
            {
                if (ViewModel == null) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ViewModel.SelectedTabIndex = ViewModel.SelectedTabIndex; 
                    ViewModel.IsManualMode = ViewModel.IsManualMode;         
                    ViewModel.UseCalculatedValues = ViewModel.UseCalculatedValues; 
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnThemeChanged error: {ex.Message}");
            }
        }

        private int _recipeId;
        public int RecipeId { get => _recipeId; set => _recipeId = value; }

        private int _folderId;
        public int FolderId { get => _folderId; set => _folderId = value; }

        protected override bool OnBackButtonPressed()
        {
            try
            {
                if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                    ViewModel.CancelCommand.Execute(null);
                return true;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnBackButtonPressed: {ex.Message}");
                return base.OnBackButtonPressed();
            }
        }

        private void OnAutoModeClicked(object sender, System.EventArgs e)
        {
            try
            {
                if (ViewModel != null)
                {
                    ViewModel.UseCalculatedValues = true;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnAutoModeClicked: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B??d prze??czania trybu: {ex.Message}";
                }
            }
        }

        private void OnManualModeClicked(object sender, System.EventArgs e)
        {
            try
            {
                if (ViewModel != null)
                {
                    ViewModel.UseCalculatedValues = false;
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnManualModeClicked: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B??d prze??czania trybu: {ex.Message}";
                }
            }
        }

        private void OnIngredientValueChanged(object sender, System.EventArgs e)
        {
            try
            {
                _valueChangeTimer?.Stop();
                var dispatcher = Application.Current?.Dispatcher ?? this.Dispatcher;
                _valueChangeTimer = dispatcher?.CreateTimer();
                if (_valueChangeTimer == null) return;

                _valueChangeTimer.Interval = TimeSpan.FromMilliseconds(500);
                _valueChangeTimer.Tick += async (s, args) =>
                {
                    try
                    {
                        _valueChangeTimer.Stop();
                        if (ViewModel != null)
                        {
                            await ViewModel.RecalculateNutritionalValuesAsync();
                        }
                    }
                    catch (System.Exception timerEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in timer callback: {timerEx.Message}");
                    }
                };
                _valueChangeTimer.Start();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnIngredientValueChanged: {ex.Message}");
            }
        }

        private async void OnIngredientNameChanged(object sender, System.EventArgs e)
        {
            try
            {
                if (sender is Picker picker && picker.BindingContext is Ingredient ingredientFromPicker)
                {
                    await (ViewModel?.UpdateIngredientNutritionalValuesAsync(ingredientFromPicker) ?? Task.CompletedTask);
                    return;
                }
                if (sender is SearchablePickerComponent comp && comp.BindingContext is Ingredient ingredient)
                {
                    await (ViewModel?.UpdateIngredientNutritionalValuesAsync(ingredient) ?? Task.CompletedTask);
                    return;
                }
                if (sender is View element)
                {
                    var ingredientFromElement = element.BindingContext as Ingredient;
                    if (ingredientFromElement != null)
                    {
                        await (ViewModel?.UpdateIngredientNutritionalValuesAsync(ingredientFromElement) ?? Task.CompletedTask);
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnIngredientNameChanged: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"B??d aktualizacji sk?adnika: {ex.Message}";
                }
            }
        }
    }
}