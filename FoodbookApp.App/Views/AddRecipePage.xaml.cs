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
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using FoodbookApp.Interfaces;

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

        // ✅ SIMPLIFIED: Shell navigation handling for unsaved changes (based on ShoppingListDetailPage)
        private bool _isSubscribedToShellNavigating = false;
        private bool _suppressShellNavigating = false;

        // ✅ OPTYMALIZACJA: Task cache dla asynchronicznych operacji
        private Task? _pendingLoadTask;
        private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

        // Track original popup background resources so we can restore them
        private object? _originalPopupBackgroundColorResource;
        private object? _originalPopupBackgroundBrushResource;
        private bool _isSubscribedToGlobalPopupEvents = false;

        private object? _originalPageBackgroundColorResource;
        private object? _originalPageBackgroundBrushResource;
        private bool _appliedLocalOpaqueBackground = false;

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

                // Hide underlying content and enforce opaque bg when this page is shown modally from a popup (PlannerPage/Popups scenarios)
                try
                {
                    if (IsModalPage())
                    {
                        HideUnderlyingContent();

                        var themeService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IThemeService>();
                        bool wallpaperEnabled = themeService?.IsWallpaperBackgroundEnabled() == true;
                        bool colorfulEnabled = themeService?.GetIsColorfulBackgroundEnabled() == true;

                        if (wallpaperEnabled || colorfulEnabled)
                        {
                            ApplyOpaqueLocalBackground();
                        }
                    }
                }
                catch { }

                // ✅ SIMPLIFIED: Subscribe to Shell.Navigating for unsaved changes prompt
                try
                {
                    if (!_isSubscribedToShellNavigating && Shell.Current != null)
                    {
                        Shell.Current.Navigating += OnShellNavigating;
                        _isSubscribedToShellNavigating = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Failed to subscribe Shell.Navigating: {ex.Message}");
                }

                // Subscribe to global popup state events so we can adjust popup overlay when page is modal
                try
                {
                    if (!_isSubscribedToGlobalPopupEvents)
                    {
                        SimplePicker.GlobalPopupStateChanged += OnGlobalPopupStateChanged;
                        SearchablePickerComponent.GlobalPopupStateChanged += OnGlobalPopupStateChanged;
                        _isSubscribedToGlobalPopupEvents = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Failed to subscribe to global popup events: {ex.Message}");
                }

                if (!_hasEverLoaded)
                {
                    // ✅ KRYTYCZNA OPTYMALIZACJA: Asynchroniczne ładowanie w tle
                    System.Diagnostics.Debug.WriteLine("🚀 AddRecipePage: First load - performing optimized initialization");
                    
                    // Reset synchronicznie
                    ViewModel?.Reset();

                    // ✅ Pokazuj UI natychmiast, ładuj dane w tle
                    _ = InitializeDataAsync();
                    
                    _hasEverLoaded = true;
                    _isInitialized = true;
                }
                else
                {
                    // Subsequent appearances (e.g., after popup close) - do not reset
                    System.Diagnostics.Debug.WriteLine("🔄 AddRecipePage: Skipping reset on re-appear");
                    
                    // ✅ OPTYMALIZACJA: Tylko labels refresh (lżejsze niż pełne składniki)
                    _ = ViewModel?.LoadAvailableLabelsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnAppearing: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"Błąd ładowania strony: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// ✅ NOWA METODA: Asynchroniczne ładowanie danych w tle bez blokowania UI
        /// </summary>
        private async Task InitializeDataAsync()
        {
            // Zabezpieczenie przed wielokrotnym ładowaniem
            if (_pendingLoadTask != null && !_pendingLoadTask.IsCompleted)
            {
                System.Diagnostics.Debug.WriteLine("⏳ AddRecipePage: Load already in progress, waiting...");
                await _pendingLoadTask;
                return;
            }

            await _loadSemaphore.WaitAsync();
            try
            {
                _pendingLoadTask = LoadDataInternalAsync();
                await _pendingLoadTask;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        /// <summary>
        /// ✅ NOWA METODA: Wewnętrzna metoda ładowania danych
        /// </summary>
        private async Task LoadDataInternalAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📦 AddRecipePage: Loading data in background...");

                // ✅ OPTYMALIZACJA: Ładuj równolegle składniki i etykiety
                var ingredientsTask = ViewModel?.LoadAvailableIngredientsAsync() ?? Task.CompletedTask;
                var labelsTask = ViewModel?.LoadAvailableLabelsAsync() ?? Task.CompletedTask;

                // Poczekaj na oba zadania
                await Task.WhenAll(ingredientsTask, labelsTask);

                System.Diagnostics.Debug.WriteLine("✅ AddRecipePage: Background data loaded successfully");

                // Jeśli edytujemy przepis, załaduj jego dane
                if (RecipeId > 0 && ViewModel != null)
                {
                    System.Diagnostics.Debug.WriteLine($"📖 AddRecipePage: Loading recipe {RecipeId}...");
                    await ViewModel.LoadRecipeAsync(RecipeId);
                }

                // ✅ FIX: Use SetInitialFolderId to preselect folder without marking dirty
                // This is not a user change, so it shouldn't mark the form as dirty
                if (FolderId > 0 && ViewModel != null)
                {
                    System.Diagnostics.Debug.WriteLine($"📁 AddRecipePage: Preselecting folder {FolderId}");
                    ViewModel.SetInitialFolderId(FolderId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ AddRecipePage: Error loading data: {ex.Message}");
                if (ViewModel != null)
                {
                    ViewModel.ValidationMessage = $"Błąd ładowania danych: {ex.Message}";
                }
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Restore underlying content visibility
            try { RestoreUnderlyingContent(); } catch { }

            _themeHelper.ThemeChanged -= OnThemeChanged;
            _themeHelper.CultureChanged -= OnCultureChanged;
            _themeHelper.Cleanup();

            // ✅ SIMPLIFIED: Unsubscribe Shell.Navigating
            try
            {
                if (_isSubscribedToShellNavigating && Shell.Current != null)
                {
                    Shell.Current.Navigating -= OnShellNavigating;
                    _isSubscribedToShellNavigating = false;
                }
            }
            catch { }

            // Unsubscribe global popup events
            try
            {
                if (_isSubscribedToGlobalPopupEvents)
                {
                    SimplePicker.GlobalPopupStateChanged -= OnGlobalPopupStateChanged;
                    SearchablePickerComponent.GlobalPopupStateChanged -= OnGlobalPopupStateChanged;
                    _isSubscribedToGlobalPopupEvents = false;
                }
            }
            catch { }

            // Cleanup local opaque override
            try
            {
                if (_appliedLocalOpaqueBackground)
                {
                    try { this.Resources.Remove("PageBackgroundColor"); } catch { }
                    try { this.Resources.Remove("PageBackgroundBrush"); } catch { }
                    _appliedLocalOpaqueBackground = false;
                }
            }
            catch { }
            
            System.Diagnostics.Debug.WriteLine("👋 AddRecipePage: Disappearing - preserving current state");
        }

        private void ApplyOpaqueLocalBackground()
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources == null) return;

                if (_originalPageBackgroundColorResource == null && app.Resources.TryGetValue("PageBackgroundColor", out var origColor))
                    _originalPageBackgroundColorResource = origColor;
                if (_originalPageBackgroundBrushResource == null && app.Resources.TryGetValue("PageBackgroundBrush", out var origBrush))
                    _originalPageBackgroundBrushResource = origBrush;

                Color pageBg = Colors.White;
                if (app.Resources.TryGetValue("PageBackgroundColor", out var pb) && pb is Color c)
                    pageBg = c;

                var overlay = Color.FromRgb(pageBg.Red, pageBg.Green, pageBg.Blue); // alpha=1
                this.Resources["PageBackgroundColor"] = overlay;
                this.Resources["PageBackgroundBrush"] = new SolidColorBrush(overlay);
                this.BackgroundColor = overlay;
                _appliedLocalOpaqueBackground = true;
                System.Diagnostics.Debug.WriteLine("[AddRecipePage] Applied opaque local background (wallpaper/colorful mode)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] ApplyOpaqueLocalBackground error: {ex.Message}");
            }
        }

        private void HideUnderlyingContent()
        {
            try
            {
                var underlying = Shell.Current?.CurrentPage;
                if (underlying != null && !ReferenceEquals(underlying, this))
                {
                    MainThread.BeginInvokeOnMainThread(() => underlying.Opacity = 0);
                    System.Diagnostics.Debug.WriteLine("[AddRecipePage] Hidden underlying page (Opacity=0)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] HideUnderlyingContent error: {ex.Message}");
            }
        }

        private void RestoreUnderlyingContent()
        {
            try
            {
                var underlying = Shell.Current?.CurrentPage;
                if (underlying != null && !ReferenceEquals(underlying, this))
                {
                    MainThread.BeginInvokeOnMainThread(() => underlying.Opacity = 1);
                    System.Diagnostics.Debug.WriteLine("[AddRecipePage] Restored underlying page (Opacity=1)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] RestoreUnderlyingContent error: {ex.Message}");
            }
        }

        // Global popup state handler - adjust popup overlay color when this page is presented modally
        private void OnGlobalPopupStateChanged(object? sender, bool isOpen)
        {
            try
            {
                // Only adjust when this page is shown modally (e.g., AddRecipePage pushed modally)
                if (!IsModalPage())
                    return;

                var app = Application.Current;
                if (app?.Resources == null)
                    return;

                // Resolve theme service to check wallpaper mode
                var themeService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IThemeService>();
                bool wallpaperEnabled = themeService?.IsWallpaperBackgroundEnabled() == true;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        // Grab current page background color if available
                        Color pageBg = Colors.White;
                        if (app.Resources.TryGetValue("PageBackgroundColor", out var existing) && existing is Color c)
                            pageBg = c;

                        if (isOpen)
                        {
                            // Store original resources once
                            if (_originalPopupBackgroundColorResource == null && app.Resources.TryGetValue("PopupBackgroundColor", out var origColor))
                                _originalPopupBackgroundColorResource = origColor;
                            if (_originalPopupBackgroundBrushResource == null && app.Resources.TryGetValue("PopupBackgroundBrush", out var origBrush))
                                _originalPopupBackgroundBrushResource = origBrush;

                            // If wallpaper enabled, use opaque overlay so wallpaper does not show through.
                            var alpha = wallpaperEnabled ? 1.0f : 0.82f;

                            var overlay = Color.FromRgba(pageBg.Red, pageBg.Green, pageBg.Blue, alpha);
                            app.Resources["PopupBackgroundColor"] = overlay;
                            app.Resources["PopupBackgroundBrush"] = new SolidColorBrush(overlay);

                            System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Overrode PopupBackgroundColor for modal context (wallpaperEnabled={wallpaperEnabled})");
                        }
                        else
                        {
                            // Restore originals if we saved them
                            if (_originalPopupBackgroundColorResource != null)
                            {
                                app.Resources["PopupBackgroundColor"] = _originalPopupBackgroundColorResource;
                                _originalPopupBackgroundColorResource = null;
                            }
                            if (_originalPopupBackgroundBrushResource != null)
                            {
                                app.Resources["PopupBackgroundBrush"] = _originalPopupBackgroundBrushResource;
                                _originalPopupBackgroundBrushResource = null;
                            }

                            System.Diagnostics.Debug.WriteLine("[AddRecipePage] Restored original PopupBackground resources after modal popup closed");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnGlobalPopupStateChanged error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Global popup state handler failed: {ex.Message}");
            }
        }

        // Helper to detect whether this page was pushed modally
        private bool IsModalPage()
        {
            try
            {
                var nav = Shell.Current?.Navigation ?? Application.Current?.MainPage?.Navigation;
                if (nav?.ModalStack == null) return false;
                return nav.ModalStack.Contains(this);
            }
            catch { return false; }
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Handle Shell navigation for unsaved changes (based on ShoppingListDetailPage)
        /// </summary>
        private void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            try
            {
                // If suppressed, allow navigation
                if (_suppressShellNavigating) return;
                
                // If pushing a new page, allow
                if (e.Source == ShellNavigationSource.Push) return;

                // Check for unsaved changes
                if (ViewModel?.HasUnsavedChanges == true)
                {
                    e.Cancel();
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        bool leave = await DisplayAlert(
                            FoodbookApp.Localization.AddRecipePageResources.ConfirmTitle, 
                            FoodbookApp.Localization.AddRecipePageResources.UnsavedChangesMessage, 
                            FoodbookApp.Localization.AddRecipePageResources.YesButton, 
                            FoodbookApp.Localization.AddRecipePageResources.NoButton);
                        
                        if (leave)
                        {
                            try { ViewModel?.DiscardChanges(); } catch { }
                            _suppressShellNavigating = true;
                            try
                            {
                                var targetLoc = e.Target?.Location?.OriginalString ?? string.Empty;
                                var nav = Shell.Current?.Navigation;
                                
                                // If modal, pop modal first
                                if (nav?.ModalStack?.Count > 0)
                                    await nav.PopModalAsync(false);
                                else if (!string.IsNullOrEmpty(targetLoc))
                                    await Shell.Current.GoToAsync(targetLoc);
                                else
                                    await Shell.Current.GoToAsync("..", false);
                            }
                            catch { }
                            finally
                            {
                                await Task.Delay(200);
                                _suppressShellNavigating = false;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnShellNavigating error: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ SIMPLIFIED: Handle hardware back button (based on ShoppingListDetailPage)
        /// </summary>
        protected override bool OnBackButtonPressed()
        {
            try
            {
                if (ViewModel?.HasUnsavedChanges == true)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        bool leave = await DisplayAlert(
                            FoodbookApp.Localization.AddRecipePageResources.ConfirmTitle, 
                            FoodbookApp.Localization.AddRecipePageResources.UnsavedChangesMessage, 
                            FoodbookApp.Localization.AddRecipePageResources.YesButton, 
                            FoodbookApp.Localization.AddRecipePageResources.NoButton);
                        
                        if (leave)
                        {
                            try { ViewModel?.DiscardChanges(); } catch { }
                            _suppressShellNavigating = true;
                            try
                            {
                                var nav = Shell.Current?.Navigation;
                                if (nav?.ModalStack?.Count > 0)
                                    await nav.PopModalAsync(false);
                                else
                                    await Shell.Current.GoToAsync("..", false);
                            }
                            catch { }
                            finally
                            {
                                await Task.Delay(200);
                                _suppressShellNavigating = false;
                            }
                        }
                    });
                    return true; // Cancel default back, we handle it
                }

                // No unsaved changes - use CancelCommand if available
                if (ViewModel?.CancelCommand?.CanExecute(null) == true)
                    ViewModel.CancelCommand.Execute(null);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] OnBackButtonPressed error: {ex.Message}");
                return base.OnBackButtonPressed();
            }
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
                    ViewModel.ValidationMessage = $"Błąd przełączania trybu: {ex.Message}";
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
                    ViewModel.ValidationMessage = $"Błąd przełączania trybu: {ex.Message}";
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
                    ViewModel.ValidationMessage = $"Błąd aktualizacji składnika: {ex.Message}";
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
                    await DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.CannotOpenLabelsManagement, FoodbookApp.Localization.AddRecipePageResources.OKButton);
                    return;
                }

                var initiallySelected = ViewModel?.SelectedLabels.Select(l => l.Id).ToList() ?? new List<int>();
                var popup = new CRUDComponentPopup(settingsVm, initiallySelected);

                // If this page is modal, override popup resources to use semi-transparent overlay (or opaque when wallpaper enabled)
                try
                {
                    if (IsModalPage())
                    {
                        var app = Application.Current;
                        if (app?.Resources != null)
                        {
                            // Resolve theme service to check wallpaper mode
                            var themeService = FoodbookApp.MauiProgram.ServiceProvider?.GetService<IThemeService>();
                            bool wallpaperEnabled = themeService?.IsWallpaperBackgroundEnabled() == true;

                            // Try to determine a suitable overlay color based on PageBackgroundColor
                            if (app.Resources.TryGetValue("PageBackgroundColor", out var pb) && pb is Color pageColor)
                            {
                                var alpha = wallpaperEnabled ? 1.0f : 0.82f;
                                var overlay = Color.FromRgba(pageColor.Red, pageColor.Green, pageColor.Blue, alpha);
                                popup.Resources["PopupBackgroundColor"] = overlay;
                                popup.Resources["PopupBackgroundBrush"] = new SolidColorBrush(overlay);
                                System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Applied local PopupBackgroundColor override for CRUDComponentPopup (modal, wallpaperEnabled={wallpaperEnabled})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AddRecipePage] Failed to apply local popup override: {ex.Message}");
                }

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
                await DisplayAlert(FoodbookApp.Localization.AddRecipePageResources.ErrorTitle, FoodbookApp.Localization.AddRecipePageResources.CannotOpenLabelsManagement, FoodbookApp.Localization.AddRecipePageResources.OKButton);
            }
            finally
            {
                _isModalOpen = false;
            }
        }
    }
}