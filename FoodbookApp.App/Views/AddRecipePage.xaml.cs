using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views.Components;
using Foodbook.Views.Base;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Extensions; // For ShowPopupAsync extension

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
        private bool _isModalOpen = false; // Flag to prevent multiple modal opens

        // drag state for reordering ingredients
        private Ingredient? _draggingIngredient;

        // Suppress handling of Shell.Navigating when we do programmatic navigation
        private bool _suppressShellNavigating = false;

        public AddRecipePage(AddRecipeViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
            _themeHelper = new PageThemeHelper();

            // Intercept Shell navigation: subscribe to Navigating event to detect back nav
            Shell.Current.Navigating += OnShellNavigating;
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

            // Unsubscribe Shell navigating to avoid memory leaks
            try { Shell.Current.Navigating -= OnShellNavigating; } catch { }
            
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
                // Intercept hardware back button
                if (ViewModel?.HasUnsavedChanges == true)
                {
                    // Show confirmation dialog synchronously on UI thread
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        bool leave = await DisplayAlert("PotwierdŸ", "Masz niezapisane zmiany. Czy na pewno chcesz wyjœæ?", "Tak", "Nie");
                        if (leave)
                        {
                            // Discard changes and navigate back without re-triggering prompt
                            try
                            {
                                ViewModel?.DiscardChanges();
                            }
                            catch { }

                            _suppressShellNavigating = true;
                            try
                            {
                                var nav = Shell.Current?.Navigation;
                                if (nav?.ModalStack?.Count > 0)
                                    await nav.PopModalAsync(false);
                                else
                                    await Shell.Current.GoToAsync("..");
                            }
                            finally
                            {
                                // small delay to ensure navigation completes before re-enabling
                                await Task.Delay(200);
                                _suppressShellNavigating = false;
                            }
                        }
                    });
                    return true; // cancel default back (we'll navigate if user confirms)
                }

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

        // Handle Shell navigation (top-back / shell routing). We cancel navigation when unsaved changes.
        private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            try
            {
                if (_suppressShellNavigating) return;

                // Only care about navigation away from this page
                if (ViewModel?.HasUnsavedChanges == true)
                {
                    System.Diagnostics.Debug.WriteLine("AddRecipePage: Detected shell navigation with unsaved changes - prompting user");

                    // Cancel navigation now and ask user
                    e.Cancel();

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        bool leave = await DisplayAlert("PotwierdŸ", "Masz niezapisane zmiany. Czy na pewno chcesz wyjœæ?", "Tak", "Nie");
                        if (leave)
                        {
                            try
                            {
                                // Discard unsaved changes in VM
                                ViewModel?.DiscardChanges();
                            }
                            catch { }

                            // Perform navigation but suppress re-entrance to this handler
                            _suppressShellNavigating = true;
                            try
                            {
                                // If there is a modal on stack, pop it first (silent)
                                var nav = Shell.Current?.Navigation;
                                if (nav?.ModalStack?.Count > 0)
                                    await nav.PopModalAsync(false);

                                // Navigate to requested location
                                if (e.Target?.Location?.OriginalString != null)
                                {
                                    await Shell.Current.GoToAsync(e.Target.Location.OriginalString);
                                }
                            }
                            catch (Exception exNav)
                            {
                                System.Diagnostics.Debug.WriteLine($"Navigation after discard failed: {exNav.Message}");
                            }
                            finally
                            {
                                // small delay to ensure navigation completes before re-enabling
                                await Task.Delay(200);
                                _suppressShellNavigating = false;
                            }
                        }
                        else
                        {
                            // Do nothing; remain on the page
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnShellNavigating error: {ex.Message}");
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

        // Drag start for ingredient reordering
        private void OnIngredientDragStarting(object? sender, DragStartingEventArgs e)
        {
            if (sender is Element el && el.BindingContext is Ingredient ing)
            {
                _draggingIngredient = ing;
                e.Data.Properties["SourceItem"] = ing;
            }
        }

        // Insert zone handlers
        private void OnIngredientTopInsertDragOver(object? sender, DragEventArgs e)
        {
            if (sender is Element el && el.BindingContext is Ingredient ing)
            {
                ing.ShowInsertBefore = true;
            }
        }
        private void OnIngredientTopInsertDragLeave(object? sender, DragEventArgs e)
        {
            if (sender is Element el && el.BindingContext is Ingredient ing)
            {
                ing.ShowInsertBefore = false;
            }
        }
        private void OnIngredientTopInsertDrop(object? sender, DropEventArgs e)
        {
            try
            {
                if (ViewModel?.Ingredients == null || _draggingIngredient == null) return;
                if (sender is Element el && el.BindingContext is Ingredient target)
                {
                    target.ShowInsertBefore = false;
                    ReorderIngredient(_draggingIngredient, target, before: true);
                }
            }
            finally
            {
                _draggingIngredient = null;
            }
        }

        private void OnIngredientBottomInsertDragOver(object? sender, DragEventArgs e)
        {
            if (sender is Element el && el.BindingContext is Ingredient ing)
            {
                ing.ShowInsertAfter = true;
            }
        }
        private void OnIngredientBottomInsertDragLeave(object? sender, DragEventArgs e)
        {
            if (sender is Element el && el.BindingContext is Ingredient ing)
            {
                ing.ShowInsertAfter = false;
            }
        }
        private void OnIngredientBottomInsertDrop(object? sender, DropEventArgs e)
        {
            try
            {
                if (ViewModel?.Ingredients == null || _draggingIngredient == null) return;
                if (sender is Element el && el.BindingContext is Ingredient target)
                {
                    target.ShowInsertAfter = false;
                    ReorderIngredient(_draggingIngredient, target, before: false);
                }
            }
            finally
            {
                _draggingIngredient = null;
            }
        }

        private void ReorderIngredient(Ingredient source, Ingredient target, bool before)
        {
            if (ViewModel == null) return;
            var items = ViewModel.Ingredients;
            if (source == target) return;

            var oldIndex = items.IndexOf(source);
            var targetIndex = items.IndexOf(target);
            if (oldIndex < 0 || targetIndex < 0) return;

            if (!before) targetIndex += 1; // after

            // adjust for removal when moving forward
            if (oldIndex < targetIndex) targetIndex--;

            items.Move(oldIndex, targetIndex);

            // trigger recalculation to update nutrition display order if needed
            _ = ViewModel.RecalculateNutritionalValuesAsync();
        }

        // Open labels management popup (also acts as selector)
        private async void OnManageLabelsClicked(object sender, EventArgs e)
        {
            try
            {
                if (_isModalOpen)
                {
                    System.Diagnostics.Debug.WriteLine("Modal already open, ignoring click");
                    return;
                }
                _isModalOpen = true;

                var settingsVm = FoodbookApp.MauiProgram.ServiceProvider?.GetService<SettingsViewModel>();
                if (settingsVm == null)
                {
                    await DisplayAlert("B³¹d", "Nie mo¿na otworzyæ zarz¹dzania etykietami", "OK");
                    return;
                }

                var initiallySelected = ViewModel?.SelectedLabels.Select(l => l.Id).ToList() ?? new List<int>();
                var popup = new CRUDComponentPopup(settingsVm, initiallySelected);

                // Use extension method from CommunityToolkit.Maui.Extensions and await ResultTask
                var showTask = this.ShowPopupAsync(popup);
                var resultTask = popup.ResultTask;
                
                // Wait for either popup to close or result to be set
                await Task.WhenAny(showTask, resultTask);
                
                var result = resultTask.IsCompleted ? await resultTask : null;

                // Handle result
                if (result is IEnumerable<int> selectedIds && ViewModel != null)
                {
                    await ViewModel.LoadAvailableLabelsAsync();
                    var idSet = selectedIds.ToHashSet();
                    ViewModel.SelectedLabels.Clear();
                    foreach (var lbl in ViewModel.AvailableLabels.Where(l => idSet.Contains(l.Id)))
                        ViewModel.SelectedLabels.Add(lbl);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] CRUDComponentPopup error: {ex.Message}");
                await DisplayAlert("B³¹d", "Nie mo¿na otworzyæ zarz¹dzania etykietami", "OK");
            }
            finally
            {
                _isModalOpen = false;
            }
        }
    }
}