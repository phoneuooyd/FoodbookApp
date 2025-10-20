using Microsoft.Maui.Controls;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Models;
using Foodbook.Views.Base;
using System.Threading.Tasks;
using Foodbook.Views.Components;
using CommunityToolkit.Maui.Extensions;

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
        private bool _isUpdatingLabelsSelection; // Flag to prevent circular updates
        private bool _isModalOpen = false; // Flag to prevent multiple modal opens

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
                _themeHelper.Initialize();
                _themeHelper.ThemeChanged += OnThemeChanged;
                _themeHelper.CultureChanged += OnCultureChanged;

                if (!_hasEverLoaded)
                {
                    // First time loading - perform full initialization
                    System.Diagnostics.Debug.WriteLine("?? AddRecipePage: First load - performing full initialization");
                    
                    ViewModel?.Reset();
                    await (ViewModel?.LoadAvailableIngredientsAsync() ?? Task.CompletedTask);
                    await (ViewModel?.LoadAvailableLabelsAsync() ?? Task.CompletedTask);

                    if (RecipeId > 0 && ViewModel != null)
                    {
                        await ViewModel.LoadRecipeAsync(RecipeId);
                        // Synchronize CollectionView selection with ViewModel after loading
                        SyncLabelsSelection();
                    }

                    // If navigation passed FolderId, preselect it
                    if (FolderId > 0 && ViewModel != null)
                        ViewModel.SelectedFolderId = FolderId;
                        
                    _hasEverLoaded = true;
                    _isInitialized = true;
                }
                else
                {
                    // Subsequent appearances (e.g., after popup close) - do not reset
                    System.Diagnostics.Debug.WriteLine("?? AddRecipePage: Skipping reset on re-appear");
                    // Refresh labels list in case user added/removed labels
                    await (ViewModel?.LoadAvailableLabelsAsync() ?? Task.CompletedTask);
                    // Re-sync selection after refresh
                    SyncLabelsSelection();
                }
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
            _themeHelper.ThemeChanged -= OnThemeChanged;
            _themeHelper.CultureChanged -= OnCultureChanged;
            _themeHelper.Cleanup();
            
            System.Diagnostics.Debug.WriteLine("? AddRecipePage: Disappearing - preserving current state");
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel == null) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Re-raise computed/bound properties so converter-based bindings recompute with new palette
                    ViewModel.SelectedTabIndex = ViewModel.SelectedTabIndex; // updates Is*TabSelected
                    ViewModel.IsManualMode = ViewModel.IsManualMode;         // updates IsImportMode
                    ViewModel.UseCalculatedValues = ViewModel.UseCalculatedValues; // updates UseManualValues
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnThemeChanged error: {ex.Message}");
            }
        }

        private void OnCultureChanged(object? sender, EventArgs e)
        {
            try
            {
                if (ViewModel == null) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    System.Diagnostics.Debug.WriteLine("[AddRecipePage] Culture changed - refreshing unit pickers");
                    
                    // Force refresh of all SimplePicker controls by triggering property changes
                    // This will cause the DisplayText to be recalculated with the new culture
                    RefreshUnitPickers();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnCultureChanged error: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh all unit pickers by finding SimplePicker controls in the visual tree
        /// </summary>
        private void RefreshUnitPickers()
        {
            try
            {
                // Find all SimplePicker controls and trigger their DisplayText refresh
                var pickers = FindVisualChildren<SimplePicker>(this);
                foreach (var picker in pickers)
                {
                    picker.RefreshDisplayText();
                }
                
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Refreshed {pickers.Count()} unit pickers");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Error refreshing unit pickers: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to find all visual children of a specific type
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(Element element) where T : Element
        {
            if (element is T match)
                yield return match;

            // Special handling for TabComponent - search all tabs, not just the visible one
            if (element is TabComponent tabComponent)
            {
                foreach (var tab in tabComponent.Tabs)
                {
                    if (tab.Content != null)
                    {
                        foreach (var descendant in FindVisualChildren<T>(tab.Content))
                            yield return descendant;
                    }
                }
            }
            else if (element is Layout layout)
            {
                foreach (var child in layout.Children)
                {
                    if (child is Element childElement)
                    {
                        foreach (var descendant in FindVisualChildren<T>(childElement))
                            yield return descendant;
                    }
                }
            }
            else if (element is ContentView contentView && contentView.Content != null)
            {
                foreach (var descendant in FindVisualChildren<T>(contentView.Content))
                    yield return descendant;
            }
            else if (element is ScrollView scrollView && scrollView.Content != null)
            {
                foreach (var descendant in FindVisualChildren<T>(scrollView.Content))
                    yield return descendant;
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

        private void OnIngredientValueChanged(object sender, EventArgs e)
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
                // Support both native Picker and custom SearchablePickerComponent
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
                
                // Alternative approach: try to get the ingredient from binding context of parent
                if (sender is View element)
                {
                    var ingredientFromElement = element.BindingContext as Ingredient;
                    if (ingredientFromElement != null)
                    {
                        await (ViewModel?.UpdateIngredientNutritionalValuesAsync(ingredientFromElement) ?? Task.CompletedTask);
                    }
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

        // Synchronize CollectionView selected items with ViewModel
        private void SyncLabelsSelection()
        {
            try
            {
                if (ViewModel == null || _isUpdatingLabelsSelection) return;

                _isUpdatingLabelsSelection = true;
                System.Diagnostics.Debug.WriteLine($"??? Syncing labels selection: {ViewModel.SelectedLabels.Count} labels");

                // Clear current selection
                LabelsCollectionView.SelectedItems?.Clear();

                // Select items that are in ViewModel.SelectedLabels
                foreach (var label in ViewModel.SelectedLabels)
                {
                    // Find matching label in AvailableLabels by Id
                    var matchingLabel = ViewModel.AvailableLabels.FirstOrDefault(l => l.Id == label.Id);
                    if (matchingLabel != null)
                    {
                        LabelsCollectionView.SelectedItems?.Add(matchingLabel);
                        System.Diagnostics.Debug.WriteLine($"   ? Selected: {matchingLabel.Name}");
                    }
                }

                _isUpdatingLabelsSelection = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error syncing labels selection: {ex.Message}");
                _isUpdatingLabelsSelection = false;
            }
        }

        // Handle CollectionView selection changes
        private void OnLabelsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ViewModel == null || _isUpdatingLabelsSelection) return;

                _isUpdatingLabelsSelection = true;
                System.Diagnostics.Debug.WriteLine("??? Labels selection changed");

                // Clear and update SelectedLabels in ViewModel
                ViewModel.SelectedLabels.Clear();
                
                foreach (var item in e.CurrentSelection)
                {
                    if (item is RecipeLabel label)
                    {
                        ViewModel.SelectedLabels.Add(label);
                        System.Diagnostics.Debug.WriteLine($"   ? Added to ViewModel: {label.Name}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"??? Total selected labels: {ViewModel.SelectedLabels.Count}");
                
                // Update visual state of all label frames
                UpdateLabelFramesVisualState();
                
                _isUpdatingLabelsSelection = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error handling labels selection: {ex.Message}");
                _isUpdatingLabelsSelection = false;
            }
        }

        // Update border colors of all label frames based on selection
        private void UpdateLabelFramesVisualState()
        {
            try
            {
                if (ViewModel == null) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var selectedIds = ViewModel.SelectedLabels.Select(l => l.Id).ToHashSet();
                    
                    // Iterate through all items in CollectionView
                    for (int i = 0; i < ViewModel.AvailableLabels.Count; i++)
                    {
                        var label = ViewModel.AvailableLabels[i];
                        
                        // Try to find the visual element for this item
                        // Note: This is a workaround since CollectionView doesn't expose direct access to item containers
                        // We'll use a different approach - iterate through visual tree
                    }
                    
                    // Alternative: Force CollectionView to update its item visuals
                    UpdateAllFrameBorders(LabelsCollectionView, selectedIds);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error updating label frames visual state: {ex.Message}");
            }
        }

        // Recursively find and update all Frame elements in CollectionView
        private void UpdateAllFrameBorders(Element element, HashSet<int> selectedIds)
        {
            try
            {
                if (element is Frame frame && frame.BindingContext is RecipeLabel label)
                {
                    // Update border color based on selection
                    var primaryColor = Application.Current?.Resources.TryGetValue("Primary", out var color) == true 
                        ? (Color)color 
                        : Color.FromArgb("#FF6200");
                    
                    frame.BorderColor = selectedIds.Contains(label.Id) ? primaryColor : Colors.Transparent;
                }

                // Recursively process children
                if (element is Layout layout)
                {
                    foreach (var child in layout.Children)
                    {
                        if (child is Element childElement)
                        {
                            UpdateAllFrameBorders(childElement, selectedIds);
                        }
                    }
                }
                else if (element is ContentView contentView && contentView.Content != null)
                {
                    UpdateAllFrameBorders(contentView.Content, selectedIds);
                }
                else if (element is ScrollView scrollView && scrollView.Content != null)
                {
                    UpdateAllFrameBorders(scrollView.Content, selectedIds);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in UpdateAllFrameBorders: {ex.Message}");
            }
        }

        // Open labels management popup
        private async void OnManageLabelsClicked(object sender, EventArgs e)
        {
            try
            {
                // Prevent multiple modal opens
                if (_isModalOpen)
                {
                    System.Diagnostics.Debug.WriteLine("Modal already open, ignoring click");
                    return;
                }

                _isModalOpen = true;

                // Get SettingsViewModel from DI
                var settingsVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<SettingsViewModel>();
                if (settingsVm == null)
                {
                    await DisplayAlert("B³¹d", "Nie mo¿na otworzyæ zarz¹dzania etykietami", "OK");
                    return;
                }

                var popup = new CRUDComponentPopup(settingsVm);
                var hostPage = Application.Current?.Windows.FirstOrDefault()?.Page ?? this;
                await hostPage.ShowPopupAsync(popup);
                
                // Refresh labels list after popup closes
                await (ViewModel?.LoadAvailableLabelsAsync() ?? Task.CompletedTask);
                // Re-sync selection after refresh
                SyncLabelsSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] CRUDComponentPopup error: {ex.Message}");
                await DisplayAlert("B³¹d", "Nie mo¿na otworzyæ zarz¹dzania etykietami", "OK");
            }
            finally
            {
                // Always reset the flag, even if an error occurred
                _isModalOpen = false;
            }
        }

        // Toggle selection when a label chip is tapped
        private void OnLabelTapped(object? sender, TappedEventArgs e)
        {
            try
            {
                if (ViewModel == null) return;

                // Resolve tapped item from BindingContext of Border
                if (sender is Element element && element.BindingContext is RecipeLabel tappedLabel)
                {
                    var selected = LabelsCollectionView.SelectedItems;

                    // Toggle: if selected -> remove; else -> add
                    var alreadySelected = selected?.OfType<RecipeLabel>().Any(l => l.Id == tappedLabel.Id) == true;
                    if (alreadySelected)
                    {
                        // Remove from CollectionView selection
                        selected?.Remove(tappedLabel);
                        // Remove from ViewModel selection
                        var vmItem = ViewModel.SelectedLabels.FirstOrDefault(l => l.Id == tappedLabel.Id);
                        if (vmItem != null) 
                            ViewModel.SelectedLabels.Remove(vmItem);
                    }
                    else
                    {
                        selected?.Add(tappedLabel);
                        // Keep VM in sync (avoid duplicates by Id)
                        if (!ViewModel.SelectedLabels.Any(l => l.Id == tappedLabel.Id))
                            ViewModel.SelectedLabels.Add(tappedLabel);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error in OnLabelTapped: {ex.Message}");
            }
        }
    }
}