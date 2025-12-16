using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using FoodbookApp;
using FoodbookApp.Interfaces;
using CommunityToolkit.Mvvm.Messaging;

namespace Foodbook.Views.Components
{
    public partial class TabBarComponent : ContentView, INotifyPropertyChanged
    {
        private TabItemModel? _selectedTab;
        private bool _isNavigating;
        private readonly Dictionary<string, ContentPage> _pageCache = new();
        private ILocalizationService? _localizationService;
        private Color _iconTintColor = Colors.Black;
        
        // Track navigation context: which tab owns which Shell routes
        private readonly Dictionary<string, string> _routeToTabMap = new()
        {
            ["AddRecipePage"] = "RecipesTab",
            ["IngredientFormPage"] = "IngredientsTab",
            ["MealFormPage"] = "PlannerTab",
            ["ShoppingListDetailPage"] = "ShoppingListTab",
            ["SettingsPage"] = "HomeTab",
            ["PlannerPage"] = "PlannerTab",
            ["ArchivePage"] = "PlannerTab",
            ["DataArchivizationPage"] = "HomeTab"
        };
        
        // Remember the last active tab before Shell navigation
        private string? _lastActiveTabBeforeNavigation = null;

        public static readonly BindableProperty TabItemsProperty =
            BindableProperty.Create(
                nameof(TabItems),
                typeof(ObservableCollection<TabItemModel>),
                typeof(TabBarComponent),
                defaultValueCreator: _ => new ObservableCollection<TabItemModel>(),
                propertyChanged: OnTabItemsChanged);

        public static readonly BindableProperty SelectedTabProperty =
            BindableProperty.Create(nameof(SelectedTab), typeof(TabItemModel), typeof(TabBarComponent), null,
                BindingMode.TwoWay, propertyChanged: OnSelectedTabChanged);

        public static readonly BindableProperty DefaultTabIndexProperty =
            BindableProperty.Create(nameof(DefaultTabIndex), typeof(int), typeof(TabBarComponent), 2);

        public static readonly BindableProperty IconTintColorProperty =
            BindableProperty.Create(nameof(IconTintColor), typeof(Color), typeof(TabBarComponent), Colors.Black,
                propertyChanged: OnIconTintColorChanged);

        private static void OnIconTintColorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TabBarComponent component && newValue is Color newColor)
            {
                component.ApplyTintColorToImages(newColor);
            }
        }

        private void ApplyTintColorToImages(Color color)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // Force refresh TintColor on all Image behaviors in TabBar
                    foreach (var child in TabBarContainer.Children)
                    {
                        if (child is Border border && border.Content is Grid grid)
                        {
                            var image = grid.Children.OfType<Image>().FirstOrDefault();
                            if (image != null)
                            {
                                var behavior = image.Behaviors.OfType<CommunityToolkit.Maui.Behaviors.IconTintColorBehavior>().FirstOrDefault();
                                if (behavior != null)
                                {
                                    // Directly set TintColor on behavior to force refresh
                                    behavior.TintColor = color;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] ApplyTintColorToImages failed: {ex.Message}");
                }
            });
        }

        public ObservableCollection<TabItemModel> TabItems
        {
            get => (ObservableCollection<TabItemModel>)GetValue(TabItemsProperty);
            set => SetValue(TabItemsProperty, value);
        }

        public TabItemModel? SelectedTab
        {
            get => (TabItemModel?)GetValue(SelectedTabProperty);
            set => SetValue(SelectedTabProperty, value);
        }

        public int DefaultTabIndex
        {
            get => (int)GetValue(DefaultTabIndexProperty);
            set => SetValue(DefaultTabIndexProperty, value);
        }

        public Color IconTintColor
        {
            get => (Color)GetValue(IconTintColorProperty);
            set => SetValue(IconTintColorProperty, value);
        }

        public ICommand SelectTabCommand { get; }

        public TabBarComponent()
        {
            InitializeComponent();
            SelectTabCommand = new Command<TabItemModel>(async tab => await OnTabSelectedAsync(tab));
            _localizationService = MauiProgram.ServiceProvider?.GetService<ILocalizationService>();

            // Set IconTintColor from resources
            RefreshIconTintColorInternal();

            // Subscribe to theme changes via ThemeService event
            if (MauiProgram.ServiceProvider?.GetService<IThemeService>() is IThemeService themeService)
            {
                themeService.ThemeChanged += OnThemeChanged;
            }

            Loaded += (_, __) =>
            {
                try
                {
                    // Ensure Home is selected on startup
                    if (TabItems != null && TabItems.Count > 0)
                    {
                        var index = Math.Clamp(DefaultTabIndex, 0, TabItems.Count - 1);
                        if (SelectedTab != TabItems[index])
                        {
                            SelectedTab = TabItems[index];
                        }
                    }
                }
                catch { }
            };
            
            // Subscribe to Shell navigation to track context
            if (Shell.Current != null)
            {
                Shell.Current.Navigated += OnShellNavigated;
            }
        }

        /// <summary>
        /// Track Shell navigation to remember which tab to return to
        /// </summary>
        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            try
            {
                var currentRoute = e.Current?.Location?.ToString() ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Shell navigated to: {currentRoute}");
                
                // If navigating away from Main, remember current tab
                if (!currentRoute.Contains("//Main") && _selectedTab != null)
                {
                    _lastActiveTabBeforeNavigation = _selectedTab.Route;
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Remembered tab before navigation: {_lastActiveTabBeforeNavigation}");
                }
                
                // If navigating back to Main, restore the correct tab
                if (currentRoute.Contains("//Main") && !string.IsNullOrEmpty(_lastActiveTabBeforeNavigation))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Returning to Main, restoring tab: {_lastActiveTabBeforeNavigation}");
                    var tabToRestore = TabItems?.FirstOrDefault(t => t.Route == _lastActiveTabBeforeNavigation);
                    if (tabToRestore != null && tabToRestore != _selectedTab)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            SelectedTab = tabToRestore;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] OnShellNavigated error: {ex.Message}");
            }
        }

        private static void OnTabItemsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TabBarComponent component && newValue is ObservableCollection<TabItemModel> tabs)
            {
                // Select default tab if none selected
                if (component.SelectedTab == null && tabs.Count > 0)
                {
                    var defaultIndex = Math.Clamp(component.DefaultTabIndex, 0, tabs.Count - 1);
                    component.SelectedTab = tabs[defaultIndex];
                }
            }
        }

        private static void OnSelectedTabChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is TabBarComponent component)
            {
                component.UpdateSelectedTab(oldValue as TabItemModel, newValue as TabItemModel);
            }
        }

        private void UpdateSelectedTab(TabItemModel? oldTab, TabItemModel? newTab)
        {
            if (oldTab != null) oldTab.IsSelected = false;
            if (newTab != null)
            {
                newTab.IsSelected = true;
                _selectedTab = newTab;
                UpdateContent(newTab);
            }
        }

        private void UpdateContent(TabItemModel tab)
        {
            try
            {
                if (ContentContainer == null) return;

                ContentPage? pageInstance = null;

                // Use cached page instance
                if (!string.IsNullOrEmpty(tab.Route) && _pageCache.TryGetValue(tab.Route, out var cachedPage))
                {
                    pageInstance = cachedPage;
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Using cached page for route: {tab.Route}");
                }
                // Resolve page from DI
                else if (tab.PageType != null)
                {
                    pageInstance = MauiProgram.ServiceProvider?.GetService(tab.PageType) as ContentPage;
                    if (pageInstance != null)
                    {
                        if (!string.IsNullOrEmpty(tab.Route))
                            _pageCache[tab.Route] = pageInstance;
                        System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Created new page from DI for route: {tab.Route}, VM: {pageInstance.BindingContext?.GetType().Name ?? "null"}");
                    }
                }
                // Use ContentTemplate if provided
                else if (tab.ContentTemplate != null)
                {
                    var created = tab.ContentTemplate.CreateContent();
                    if (created is ContentPage cp)
                    {
                        pageInstance = cp;
                    }
                }

                if (pageInstance != null)
                {
                    var viewToRender = pageInstance.Content;
                    if (viewToRender != null)
                    {
                        viewToRender.BindingContext = pageInstance.BindingContext;
                        ContentContainer.Content = viewToRender;
                        System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Rendered content with BindingContext: {viewToRender.BindingContext?.GetType().Name ?? "null"}");
                        _ = TriggerInitialLoadAsync(pageInstance);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Error updating content: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles tab selection with navigation stack management and unsaved changes detection
        /// </summary>
        private async Task OnTabSelectedAsync(TabItemModel? selectedTab)
        {
            if (selectedTab == null || _isNavigating || selectedTab == _selectedTab) return;
            try
            {
                _isNavigating = true;
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Tab selected: {selectedTab.Title}");

                // Check if current page has unsaved changes
                if (await ShouldPreventNavigationAsync()) return;

                // Clear Shell navigation stack when switching tabs
                try
                {
                    var navigation = Shell.Current?.Navigation;
                    if (navigation != null && navigation.NavigationStack.Count > 1)
                    {
                        await navigation.PopToRootAsync(false);
                    }
                }
                catch { }

                // Switch to selected tab
                SelectedTab = selectedTab;

                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Successfully navigated to: {selectedTab.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Error during tab selection: {ex.Message}");
            }
            finally { _isNavigating = false; }
        }

        /// <summary>
        /// Handles back button press - simple logic:
        /// - On HomePage: Show exit confirmation
        /// - On other tabs: Navigate to HomePage
        /// - In Shell pages: Navigate back to appropriate tab
        /// </summary>
        public async Task<bool> HandleBackButtonAsync()
        {
            try
            {
                var currentLocation = Shell.Current?.CurrentState?.Location?.ToString() ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] HandleBackButtonAsync - Location: {currentLocation}, CurrentTab: {_selectedTab?.Route}");

                // SCENARIO 1: We're in a Shell-navigated page (detail page like AddRecipePage, IngredientFormPage, etc.)
                if (!currentLocation.Contains("//Main"))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] In Shell page, navigating back to Main");
                    
                    // Extract route name to determine which tab to return to
                    var routeName = ExtractRouteFromLocation(currentLocation);
                    
                    if (!string.IsNullOrEmpty(routeName) && _routeToTabMap.TryGetValue(routeName, out var targetTabRoute))
                    {
                        System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Returning to tab: {targetTabRoute}");
                        
                        // Navigate back to Main
                        await Shell.Current.GoToAsync("//Main");
                        
                        // Switch to the correct tab
                        var targetTab = TabItems?.FirstOrDefault(t => t.Route == targetTabRoute);
                        if (targetTab != null)
                        {
                            SelectedTab = targetTab;
                        }
                    }
                    else
                    {
                        // Fallback: just go back to Main
                        await Shell.Current.GoToAsync("//Main");
                    }
                    
                    return true; // Handled
                }

                // SCENARIO 2: We're on MainPage (TabBar) - check which tab is active
                
                // If we're NOT on HomePage, navigate to HomePage
                if (_selectedTab?.Route != "HomeTab")
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] On '{_selectedTab?.Route}', navigating to HomePage");
                    
                    var homeTab = TabItems?.FirstOrDefault(t => t.Route == "HomeTab");
                    if (homeTab != null)
                    {
                        SelectedTab = homeTab;
                        System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Navigated to HomePage");
                    }
                    
                    return true; // Handled
                }
                
                // SCENARIO 3: We're on HomePage - show exit confirmation
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] On HomePage, showing exit confirmation");
                
                var title = _localizationService?.GetString("HomePageResources", "ExitAppTitle") ?? "Wyjœcie";
                var message = _localizationService?.GetString("HomePageResources", "ExitAppConfirmation") ?? "Czy chcesz wyjœæ z aplikacji?";
                
                bool result = await Application.Current!.MainPage!.DisplayAlert(title, message, "Tak", "Nie");
                
                if (result)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] User confirmed exit");
                    Application.Current?.Quit();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] User cancelled exit");
                }
                
                return true; // Handled (don't let system close app without confirmation)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] HandleBackButtonAsync error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extract route name from Shell location string
        /// </summary>
        private string ExtractRouteFromLocation(string location)
        {
            try
            {
                // Location format: "//Main/AddRecipePage" or "app:///Main/AddRecipePage"
                var parts = location.Split('/');
                return parts.LastOrDefault(p => !string.IsNullOrWhiteSpace(p) && p != "Main") ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Checks if current page implements unsaved changes warning (_isDirty pattern)
        /// </summary>
        private async Task<bool> ShouldPreventNavigationAsync()
        {
            try
            {
                var currentPage = Shell.Current?.CurrentPage as BindableObject;
                if (currentPage == null) return false;

                // Check for _isDirty field
                var pageType = currentPage.GetType();
                var isDirtyField = pageType.GetField("_isDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (isDirtyField != null && isDirtyField.GetValue(currentPage) is bool isDirty && isDirty)
                    return await ShowUnsavedChangesDialogAsync();

                // Try to find HasUnsavedChanges property in ViewModel
                var bindingContext = currentPage.BindingContext;
                if (bindingContext != null)
                {
                    var vmType = bindingContext.GetType();
                    var hasUnsavedProp = vmType.GetProperty("HasUnsavedChanges");
                    if (hasUnsavedProp != null && hasUnsavedProp.GetValue(bindingContext) is bool hasUnsaved && hasUnsaved)
                        return await ShowUnsavedChangesDialogAsync();
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Error checking unsaved changes: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shows confirmation dialog for unsaved changes (localized)
        /// </summary>
        private async Task<bool> ShowUnsavedChangesDialogAsync()
        {
            try
            {
                var title = _localizationService?.GetString("CommonResources", "UnsavedChangesTitle") ?? "Niezapisane zmiany";
                var message = _localizationService?.GetString("CommonResources", "UnsavedChangesMessage") ?? "Masz niezapisane zmiany. Czy na pewno chcesz opuœciæ tê stronê?";
                var leave = _localizationService?.GetString("CommonResources", "LeaveButton") ?? "Opuœæ";
                var cancel = _localizationService?.GetString("CommonResources", "CancelButton") ?? "Anuluj";

                var result = await Application.Current!.MainPage!.DisplayAlert(title, message, leave, cancel);
                return !result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Error showing dialog: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Navigate to a specific tab by route
        /// </summary>
        public async Task NavigateToTabAsync(string route)
        {
            var tab = TabItems?.FirstOrDefault(t => t.Route == route);
            if (tab != null)
                await OnTabSelectedAsync(tab);
        }

        // Trigger data loading for embedded pages
        private async Task TriggerInitialLoadAsync(ContentPage page)
        {
            try
            {
                var vm = page.BindingContext;
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] TriggerInitialLoadAsync for {page.GetType().Name}, VM: {vm?.GetType().Name ?? "null"}");
                if (vm == null) 
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] No VM found for {page.GetType().Name}, skipping load");
                    return;
                }

                // First, initialize theme/font handling if the page has it
                var initThemeMethod = page.GetType().GetMethod("InitializeThemeAndFontHandling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (initThemeMethod != null)
                {
                    initThemeMethod.Invoke(page, null);
                }

                // Handle specific page initializations
                var pageType = page.GetType();
                if (pageType.Name == "IngredientsPage")
                {
                    var initSubsMethod = pageType.GetMethod("InitializeSubscriptionsForTabBar", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (initSubsMethod != null)
                    {
                        initSubsMethod.Invoke(page, null);
                    }
                }
                else if (pageType.Name == "RecipesPage")
                {
                    // Reset folder breadcrumb to root on the Recipes tab by calling VM method
                    var resetMethod = vm.GetType().GetMethod("ResetFolderNavigation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (resetMethod != null)
                    {
                        resetMethod.Invoke(vm, null);
                    }
                }
                else if (pageType.Name == "ShoppingListPage")
                {
                    var startListeningMethod = pageType.GetMethod("StartListening", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (startListeningMethod != null)
                    {
                        startListeningMethod.Invoke(page, null);
                    }
                }

                var vmType = vm.GetType();
                var loadRecipes = vmType.GetMethod("LoadRecipesAsync");
                if (loadRecipes != null)
                {
                    var task = loadRecipes.Invoke(vm, null) as Task;
                    if (task != null) await task;
                    return;
                }
                var loadIngredients = vmType.GetMethod("LoadAsync");
                if (loadIngredients != null)
                {
                    var task = loadIngredients.Invoke(vm, null) as Task;
                    if (task != null) await task;
                    return;
                }
                var loadPlans = vmType.GetMethod("LoadPlansAsync");
                if (loadPlans != null)
                {
                    var task = loadPlans.Invoke(vm, null) as Task;
                    if (task != null) await task;
                    return;
                }
                var loadHome = vmType.GetMethod("LoadAsync");
                if (loadHome != null)
                {
                    var task = loadHome.Invoke(vm, null) as Task;
                    if (task != null) await task;
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] TriggerInitialLoadAsync error: {ex.Message}");
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(RefreshIconTintColorInternal);
        }

        // Only refresh the IconTintColor from static resources; no full TabBar reload
        private void RefreshIconTintColorInternal()
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources == null) return;

                // Use dedicated TabBarIconTint resource (adjusts for dark/light mode automatically)
                if (app.Resources.TryGetValue("TabBarIconTint", out var iconTintObj))
                {
                    if (iconTintObj is Color c)
                    {
                        IconTintColor = c;
                    }
                    else if (iconTintObj is SolidColorBrush b)
                    {
                        IconTintColor = b.Color;
                    }
                }
                else if (app.Resources.TryGetValue("Primary", out var primaryObj))
                {
                    // Fallback to Primary if TabBarIconTint not found
                    if (primaryObj is Color c)
                    {
                        IconTintColor = c;
                    }
                    else if (primaryObj is SolidColorBrush b)
                    {
                        IconTintColor = b.Color;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] RefreshIconTintColorInternal failed: {ex.Message}");
            }
        }

        public new event PropertyChangedEventHandler? PropertyChanged;
        protected virtual new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class TabItemModel : BindableObject
    {
        private bool _isSelected;

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(TabItemModel), string.Empty);
        public static readonly BindableProperty IconProperty =
            BindableProperty.Create(nameof(Icon), typeof(ImageSource), typeof(TabItemModel), null);
        public static readonly BindableProperty RouteProperty =
            BindableProperty.Create(nameof(Route), typeof(string), typeof(TabItemModel), string.Empty);
        public static readonly BindableProperty ContentTemplateProperty =
            BindableProperty.Create(nameof(ContentTemplate), typeof(DataTemplate), typeof(TabItemModel), null);
        public static readonly BindableProperty PageTypeProperty =
            BindableProperty.Create(nameof(PageType), typeof(Type), typeof(TabItemModel), null);

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public ImageSource? Icon { get => (ImageSource?)GetValue(IconProperty); set => SetValue(IconProperty, value); }
        public string Route { get => (string)GetValue(RouteProperty); set => SetValue(RouteProperty, value); }
        public DataTemplate? ContentTemplate { get => (DataTemplate?)GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }
        public Type? PageType { get => (Type?)GetValue(PageTypeProperty); set => SetValue(PageTypeProperty, value); }
        public bool IsSelected { get => _isSelected; set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } } }
    }
}
