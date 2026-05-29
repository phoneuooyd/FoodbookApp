using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Foodbook.Utils;
using FoodbookApp;
using FoodbookApp.Interfaces;
using FoodbookApp.Localization;

namespace Foodbook.Views.Components
{
    public partial class TabBarComponent : ContentView, INotifyPropertyChanged
    {
        private TabItemModel? _selectedTab;
        private bool _isNavigating;
        private readonly Dictionary<string, ContentPage> _pageCache = new();
        private readonly Dictionary<ContentPage, View> _pageViewCache = new();
        private readonly List<CommunityToolkit.Maui.Behaviors.IconTintColorBehavior> _tabIconTintBehaviors = new();
        private readonly SemaphoreSlim _contentSwapGate = new(1, 1);
        private CancellationTokenSource? _tabLoadCts;
        private int _tabLoadVersion;
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
                    EnsureTintBehaviorCache();

                    foreach (var behavior in _tabIconTintBehaviors)
                    {
                        behavior.TintColor = color;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] ApplyTintColorToImages failed: {ex.Message}");
                }
            });
        }

        private void EnsureTintBehaviorCache()
        {
            if (_tabIconTintBehaviors.Count > 0 || TabBarContainer == null)
            {
                return;
            }

            foreach (var child in TabBarContainer.Children)
            {
                if (child is not Border border || border.Content is not Grid grid)
                {
                    continue;
                }

                var image = grid.Children.OfType<Image>().FirstOrDefault();
                var behavior = image?.Behaviors.OfType<CommunityToolkit.Maui.Behaviors.IconTintColorBehavior>().FirstOrDefault();
                if (behavior != null)
                {
                    _tabIconTintBehaviors.Add(behavior);
                }
            }
        }

        private void InvalidateTintBehaviorCache()
            => _tabIconTintBehaviors.Clear();

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

            Loaded += async (_, __) =>
            {
                try
                {
                    // Ensure default tab is selected on startup
                    if (TabItems != null && TabItems.Count > 0)
                    {
                        var index = Math.Clamp(DefaultTabIndex, 0, TabItems.Count - 1);
                        var desired = TabItems[index];

                        if (SelectedTab != desired)
                            SelectedTab = desired;

                        // Force initial render + lifecycle for embedded pages (ContentPage.OnAppearing isn't automatic)
                        if (ContentContainer?.Content == null && SelectedTab != null)
                        {
                            await UpdateContentAsync(previousTab: null, tab: SelectedTab);
                        }

                        // Give UI a tick, then ensure Appearing fired for initial page
                        await Task.Yield();
                        try { _currentPageInstance?.SendAppearing(); } catch { }
                    }

                    InvalidateTintBehaviorCache();
                    EnsureTintBehaviorCache();
                    ApplyTintColorToImages(IconTintColor);
                }
                catch { }
            };

            Unloaded += (_, __) => CancelPendingTabLoad();

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
                component.InvalidateTintBehaviorCache();

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
            if (newTab == null) return;

            newTab.IsSelected = true;
            _selectedTab = newTab;
            _ = UpdateContentAsync(oldTab, newTab);
        }

        private ContentPage? _currentPageInstance;

        private async Task UpdateContentAsync(TabItemModel? previousTab, TabItemModel tab)
        {
            await _contentSwapGate.WaitAsync();
            try
            {
                if (ContentContainer == null) return;

                try
                {
                    _currentPageInstance?.SendDisappearing();
                }
                catch { }

                var previousView = ContentContainer.Content as View;
                var direction = ResolveTransitionDirection(previousTab, tab);

                var pageInstance = ResolvePageInstance(tab);
                if (pageInstance == null)
                    return;

                var viewToRender = ResolveViewForPage(pageInstance);
                if (viewToRender == null)
                    return;

                if (ReferenceEquals(previousView, viewToRender))
                {
                    _currentPageInstance = pageInstance;
                    try { pageInstance.SendAppearing(); } catch { }
                    ScheduleTabLoad(pageInstance);
                    return;
                }

                DetachViewFromParent(viewToRender);

                if (previousView != null)
                {
                    ContentContainer.Content = null;
                    if (TransitionOverlay != null)
                    {
                        TransitionOverlay.Content = previousView;
                        TransitionOverlay.IsVisible = true;
                    }
                }
                else if (TransitionOverlay != null)
                {
                    TransitionOverlay.Content = null;
                    TransitionOverlay.IsVisible = false;
                }

                if (!ReferenceEquals(viewToRender.BindingContext, pageInstance.BindingContext))
                {
                    viewToRender.BindingContext = pageInstance.BindingContext;
                }
                ContentContainer.Content = viewToRender;
                _currentPageInstance = pageInstance;

                try
                {
                    pageInstance.SendAppearing();
                }
                catch { }

                await TabContentTransitionAnimator.AnimateContentSwapAsync(
                    TransitionOverlay,
                    previousView,
                    viewToRender,
                    direction);

                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Rendered content with BindingContext: {viewToRender.BindingContext?.GetType().Name ?? "null"}");
                ScheduleTabLoad(pageInstance);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Error updating content: {ex.Message}");
            }
            finally
            {
                _contentSwapGate.Release();
            }
        }

        private ContentPage? ResolvePageInstance(TabItemModel tab)
        {
            ContentPage? pageInstance = null;

            if (!string.IsNullOrEmpty(tab.Route) && _pageCache.TryGetValue(tab.Route, out var cachedPage))
            {
                pageInstance = cachedPage;
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] Using cached page for route: {tab.Route}");
            }
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
            else if (tab.ContentTemplate != null)
            {
                var created = tab.ContentTemplate.CreateContent();
                if (created is ContentPage cp)
                {
                    pageInstance = cp;
                }
            }

            return pageInstance;
        }

        private View? ResolveViewForPage(ContentPage pageInstance)
        {
            if (_pageViewCache.TryGetValue(pageInstance, out var cachedView))
                return cachedView;

            var viewToRender = pageInstance.Content;
            if (viewToRender == null)
                return null;

            pageInstance.Content = null;
            _pageViewCache[pageInstance] = viewToRender;
            return viewToRender;
        }

        private static void DetachViewFromParent(View view)
        {
            if (view.Parent is ContentView contentViewParent)
            {
                contentViewParent.Content = null;
            }
            else if (view.Parent is ContentPage contentPageParent)
            {
                contentPageParent.Content = null;
            }
            else if (view.Parent is Border borderParent)
            {
                borderParent.Content = null;
            }
        }

        private int ResolveTransitionDirection(TabItemModel? previousTab, TabItemModel currentTab)
        {
            if (TabItems == null || previousTab == null)
                return 1;

            var previousIndex = TabItems.IndexOf(previousTab);
            var currentIndex = TabItems.IndexOf(currentTab);
            if (previousIndex < 0 || currentIndex < 0 || previousIndex == currentIndex)
                return 1;

            return currentIndex > previousIndex ? 1 : -1;
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
                
                var title = _localizationService?.GetString("HomePageResources", "ExitAppTitle") ?? HomePageResources.ExitAppTitle;
                var message = _localizationService?.GetString("HomePageResources", "ExitAppConfirmation") ?? HomePageResources.ExitAppConfirmation;

                bool result = await Application.Current!.MainPage!.DisplayAlert(title, message, ButtonResources.Yes, ButtonResources.No);
                
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
        /// Checks if current page or its ViewModel has unsaved changes.
        /// Uses IHasUnsavedChanges interface instead of reflection.
        /// </summary>
        private async Task<bool> ShouldPreventNavigationAsync()
        {
            try
            {
                var currentPage = Shell.Current?.CurrentPage;
                if (currentPage == null) return false;

                // Check page itself
                if (currentPage is IHasUnsavedChanges pageWithChanges && pageWithChanges.HasUnsavedChanges)
                    return await ShowUnsavedChangesDialogAsync();

                // Check ViewModel
                if (currentPage.BindingContext is IHasUnsavedChanges vmWithChanges && vmWithChanges.HasUnsavedChanges)
                    return await ShowUnsavedChangesDialogAsync();

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
                var title = _localizationService?.GetString("CommonResources", "UnsavedChangesTitle") ?? AddRecipePageResources.ConfirmTitle;
                var message = _localizationService?.GetString("CommonResources", "UnsavedChangesMessage") ?? AddRecipePageResources.UnsavedChangesMessage;
                var leave = _localizationService?.GetString("CommonResources", "LeaveButton") ?? ButtonResources.Yes;
                var cancel = _localizationService?.GetString("CommonResources", "CancelButton") ?? ButtonResources.Cancel;

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
        private async Task TriggerInitialLoadAsync(ContentPage page, int loadVersion, CancellationToken cancellationToken)
        {
            try
            {
                await ComponentAnimationHelper.DelayForPostTransitionLoadAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || loadVersion != _tabLoadVersion || !ReferenceEquals(page, _currentPageInstance))
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] TriggerInitialLoadAsync for {page.GetType().Name}");

                // Initialize theme/font handling if present (still via reflection, low risk, private method)
                var initThemeMethod = page.GetType().GetMethod("InitializeThemeAndFontHandling", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                initThemeMethod?.Invoke(page, null);

                // Use interface instead of reflection for tab activation
                if (page is ITabLoadable tabLoadable)
                {
                    System.Diagnostics.Debug.WriteLine($"[TabBarComponent] ITabLoadable.OnTabActivatedAsync on {page.GetType().Name}");
                    await tabLoadable.OnTabActivatedAsync();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] {page.GetType().Name} does not implement ITabLoadable - skipping load");
            }
            catch (OperationCanceledException)
            {
                // A newer tab selection superseded this request.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] TriggerInitialLoadAsync error: {ex.Message}");
            }
        }

        private void ScheduleTabLoad(ContentPage page)
        {
            CancelPendingTabLoad();

            _tabLoadCts = new CancellationTokenSource();
            var loadVersion = Interlocked.Increment(ref _tabLoadVersion);
            _ = TriggerInitialLoadAsync(page, loadVersion, _tabLoadCts.Token);
        }

        private void CancelPendingTabLoad()
        {
            try
            {
                _tabLoadCts?.Cancel();
                _tabLoadCts?.Dispose();
            }
            catch { }
            finally
            {
                _tabLoadCts = null;
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RefreshIconTintColorInternal();
                RefreshTabBarVisualsInternal();
            });
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

        private static Color ResolveColorResource(object? resource, Color fallback)
        {
            if (resource is Color color)
            {
                return color;
            }

            if (resource is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return fallback;
        }

        private static bool TryGetTabBarTitleColors(ResourceDictionary resources, out Color activeColor, out Color unselectedColor)
        {
            activeColor = Colors.Black;
            unselectedColor = Colors.Gray;

            if (!resources.TryGetValue("TabBarForeground", out var activeObj) ||
                !resources.TryGetValue("TabBarUnselected", out var unselectedObj))
            {
                return false;
            }

            activeColor = ResolveColorResource(activeObj, Colors.Black);
            unselectedColor = ResolveColorResource(unselectedObj, Colors.Gray);
            return true;
        }

        private void UpdateTabTitleColors(Color activeColor, Color unselectedColor)
        {
            if (TabBarContainer == null)
            {
                return;
            }

            foreach (var child in TabBarContainer.Children)
            {
                if (child is not Border border || border.Content is not Grid grid)
                {
                    continue;
                }

                if (border.BindingContext is not TabItemModel tabItem)
                {
                    continue;
                }

                var titleLabel = grid.Children.OfType<Label>().FirstOrDefault();
                if (titleLabel != null)
                {
                    titleLabel.TextColor = tabItem.IsSelected ? activeColor : unselectedColor;
                }
            }
        }

        // Ensure tab item shadow and pressed backgrounds immediately reflect current theme
        private void RefreshTabBarVisualsInternal()
        {
            try
            {
                if (TabBarContainer == null)
                {
                    return;
                }

                var app = Application.Current;
                var resources = app?.Resources;
                if (resources == null)
                {
                    TabBarContainer.InvalidateMeasure();
                    return;
                }

                if (!TryGetTabBarTitleColors(resources, out var activeColor, out var unselectedColor))
                {
                    TabBarContainer.InvalidateMeasure();
                    return;
                }

                UpdateTabTitleColors(activeColor, unselectedColor);

                TabBarContainer.InvalidateMeasure();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabBarComponent] RefreshTabBarVisualsInternal failed: {ex.Message}");
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
