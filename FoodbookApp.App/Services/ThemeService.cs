using Foodbook.Models;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls; // ImageSource, Color
using Application = Microsoft.Maui.Controls.Application;

#if ANDROID
using Android.OS;
using Android.Views;
using Microsoft.Maui.Platform;
using AndroidX.Core.View;
#endif

namespace Foodbook.Services
{
    public class ThemeService : IThemeService
    {
        public event EventHandler? ThemeChanged;

        private Foodbook.Models.AppTheme _currentTheme = Foodbook.Models.AppTheme.System;
        private AppColorTheme _currentColorTheme = AppColorTheme.Default;
        private bool _isColorfulBackgroundEnabled;
        private bool _isWallpaperEnabled;
        private readonly Dictionary<AppColorTheme, ThemeColors> _availableColorThemes;

        // Supported wallpapers per color theme
        // Files are expected in Resources/Wallpapers with pattern: <theme>_light.jpg and <theme>_dark.jpg
        // If only single is provided, it will be used for both light and dark
        private readonly Dictionary<AppColorTheme, (string? single, string? light, string? dark)> _wallpaperMap = new()
        {
            // Default
            [AppColorTheme.Default]    = (single: null, light: "default_light.jpg",    dark: "default_dark.jpg"),
            // Nature family
            [AppColorTheme.Nature]     = (single: null, light: "nature_light.jpg",     dark: "nature_dark.jpg"),
            [AppColorTheme.Forest]     = (single: null, light: "forest_light.jpg",     dark: "forest_dark.jpg"),
            // Warm colors
            [AppColorTheme.Autumn]     = (single: null, light: "autumn_light.jpg",     dark: "autumn_dark.jpg"),
            [AppColorTheme.Warm]       = (single: null, light: "warm_light.jpg",       dark: "warm_dark.jpg"),
            [AppColorTheme.Sunset]     = (single: null, light: "sunset_light.jpg",     dark: "sunset_dark.jpg"),
            [AppColorTheme.Vibrant]    = (single: null, light: "vibrant_light.jpg",    dark: "vibrant_dark.jpg"),
            // Neutral
            [AppColorTheme.Monochrome] = (single: "monochrome.jpg", light: null,       dark: null),
            // Cool colors
            [AppColorTheme.Navy]       = (single: null, light: "navy_light.jpg",       dark: "navy_dark.jpg"),
            [AppColorTheme.Mint]       = (single: null, light: "mint_light.jpg",       dark: "mint_dark.jpg"),
            [AppColorTheme.Sky]        = (single: null, light: "sky_light.jpg",        dark: "sky_dark.jpg"),
            // Fun
            [AppColorTheme.Bubblegum]  = (single: null, light: "bubblegum_light.jpg",  dark: "bubblegum_dark.jpg"),
        };

        public ThemeService()
        {
            _availableColorThemes = InitializeThemes();
        }

        public Foodbook.Models.AppTheme GetCurrentTheme() => _currentTheme;
        public AppColorTheme GetCurrentColorTheme() => _currentColorTheme;
        public Dictionary<AppColorTheme, ThemeColors> GetAvailableColorThemes() => _availableColorThemes;
        public ThemeColors GetThemeColors(AppColorTheme colorTheme) => _availableColorThemes.TryGetValue(colorTheme, out var colors) ? colors : _availableColorThemes[AppColorTheme.Default];

        public bool GetIsColorfulBackgroundEnabled() => _isColorfulBackgroundEnabled;

        public void SetColorfulBackground(bool useColorfulBackground)
        {
            if (useColorfulBackground && _isWallpaperEnabled)
                _isWallpaperEnabled = false;

            _isColorfulBackgroundEnabled = useColorfulBackground;
            ApplyColorTheme(_currentColorTheme);
        }

        public void EnableWallpaperBackground(bool isEnabled)
        {
            if (isEnabled && !IsWallpaperAvailableFor(_currentColorTheme))
            {
                // Not available for this theme
                isEnabled = false;
            }

            if (isEnabled && _isColorfulBackgroundEnabled)
                _isColorfulBackgroundEnabled = false;

            _isWallpaperEnabled = isEnabled;
            ApplyColorTheme(_currentColorTheme);
        }

        public bool IsWallpaperBackgroundEnabled() => _isWallpaperEnabled;

        public bool IsWallpaperAvailableFor(AppColorTheme colorTheme)
        {
            if (_wallpaperMap.TryGetValue(colorTheme, out var tuple))
            {
                return !string.IsNullOrWhiteSpace(tuple.single) || !string.IsNullOrWhiteSpace(tuple.light) || !string.IsNullOrWhiteSpace(tuple.dark);
            }
            return false;
        }

        public void SetTheme(Foodbook.Models.AppTheme theme)
        {
            _currentTheme = theme;
            var application = Application.Current;
            if (application == null) return;

            application.UserAppTheme = theme switch
            {
                Foodbook.Models.AppTheme.Light => Microsoft.Maui.ApplicationModel.AppTheme.Light,
                Foodbook.Models.AppTheme.Dark => Microsoft.Maui.ApplicationModel.AppTheme.Dark,
                _ => Microsoft.Maui.ApplicationModel.AppTheme.Unspecified
            };

            ApplyColorTheme(_currentColorTheme);
        }

        public void SetColorTheme(AppColorTheme colorTheme)
        {
            _currentColorTheme = colorTheme;

            // If the new theme doesn't support wallpapers, disable them
            if (_isWallpaperEnabled && !IsWallpaperAvailableFor(colorTheme))
                _isWallpaperEnabled = false;

            ApplyColorTheme(colorTheme);
        }

        public void UpdateSystemBars()
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources == null) return;
                var shellBg = TryGetColor(app, "ShellBackgroundColor") ?? TryGetColor(app, "Primary");
                if (shellBg is null) return;
                ApplySystemBars(shellBg);
            }
            catch { }
        }

        private static Color? TryGetColor(Application app, string key)
        {
            if (app.Resources.TryGetValue(key, out var obj) && obj is Color c) return c; return null;
        }

        // -------- Helpers --------
        private static double RelativeLuminance(Color c)
        {
            double Channel(double ch) { ch /= 255.0; return ch <= 0.03928 ? ch / 12.92 : Math.Pow((ch + 0.055) / 1.055, 2.4); }
            return 0.2126 * Channel(c.Red) + 0.7152 * Channel(c.Green) + 0.0722 * Channel(c.Blue);
        }

        private static double ContrastRatio(Color a, Color b)
        {
            var l1 = RelativeLuminance(a) + 0.05; var l2 = RelativeLuminance(b) + 0.05; return l1 > l2 ? l1 / l2 : l2 / l1;
        }

        private static Color ChooseReadableEnhanced(Color background, Color preferredLight, Color preferredDark)
        {
            var luminance = RelativeLuminance(background);
            return luminance > 0.45 ? preferredDark : preferredLight;
        }

        private static Color EnsureContrastEnhanced(Color foreground, Color background, Color fallback)
        {
            return ContrastRatio(foreground, background) < 4.5 ? fallback : foreground;
        }

        private static (Color active, Color unselected) GetOptimalTabBarColors(Color background, bool isDark, AppColorTheme colorTheme)
        {
            var luminance = RelativeLuminance(background);
            Color active, unselected;
            if (luminance > 0.4) { active = Color.FromArgb("#000000"); unselected = Color.FromArgb("#424242"); }
            else { active = Color.FromArgb("#FFFFFF"); unselected = Color.FromArgb("#E0E0E0"); }
            if (colorTheme == AppColorTheme.Monochrome && isDark && luminance < 0.3) { active = Color.FromArgb("#FFFFFF"); unselected = Color.FromArgb("#CCCCCC"); }
            var activeContrast = ContrastRatio(active, background);
            var unselectedContrast = ContrastRatio(unselected, background);
            if (activeContrast < 4.5) active = luminance > 0.5 ? Color.FromArgb("#000000") : Color.FromArgb("#FFFFFF");
            if (unselectedContrast < 3.0) unselected = luminance > 0.5 ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
            return (active, unselected);
        }

        private static Color Lighten(Color color, double factor)
        {
            factor = Math.Clamp(factor, 0, 1);
            return Color.FromRgb(
                color.Red + (1 - color.Red) * factor,
                color.Green + (1 - color.Green) * factor,
                color.Blue + (1 - color.Blue) * factor
            );
        }

        private static Color Darken(Color color, double factor)
        {
            factor = Math.Clamp(factor, 0, 1);
            return Color.FromRgb(
                color.Red * (1 - factor),
                color.Green * (1 - factor),
                color.Blue * (1 - factor)
            );
        }

        private void ApplyColorTheme(AppColorTheme colorTheme)
        {
            try
            {
                var app = Application.Current;
                if (app?.Resources == null) return;

                var themeColors = GetThemeColors(colorTheme);

                bool isDark;
                if (_currentTheme == Foodbook.Models.AppTheme.Light) isDark = false;
                else if (_currentTheme == Foodbook.Models.AppTheme.Dark) isDark = true;
                else isDark = app.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark || (app.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified && app.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark);

                var primaryLight = themeColors.PrimaryLight;
                var secondaryLight = themeColors.SecondaryLight;
                var tertiaryLight = themeColors.TertiaryLight;
                var accentLight = themeColors.AccentLight;
                var primaryDark = themeColors.PrimaryDark;
                var secondaryDark = themeColors.SecondaryDark;
                var tertiaryDark = themeColors.TertiaryDark;
                var accentDark = themeColors.AccentDark;

                var primaryTextLight = themeColors.PrimaryTextLight;
                var secondaryTextLight = themeColors.SecondaryTextLight;
                var primaryTextDark = themeColors.PrimaryTextDark;
                var secondaryTextDark = themeColors.SecondaryTextDark;

                var primary = isDark ? primaryDark : primaryLight;
                var secondary = isDark ? secondaryDark : secondaryLight;
                var tertiary = isDark ? tertiaryDark : tertiaryLight;
                var accent = isDark ? accentDark : accentLight;
                var primaryText = isDark ? primaryTextDark : primaryTextLight;
                var secondaryText = isDark ? secondaryTextDark : secondaryTextLight;

                if (_isColorfulBackgroundEnabled && !_isWallpaperEnabled && !isDark)
                {
                    primary = Lighten(primary, 0.12);
                    secondary = Lighten(secondary, 0.18);
                    tertiary = Lighten(tertiary, 0.10);
                    accent = Lighten(accent, 0.12);
                }

                // Publish general (current) colors
                app.Resources["Primary"] = primary;
                app.Resources["Secondary"] = secondary;
                app.Resources["Tertiary"] = tertiary;
                app.Resources["Accent"] = accent;
                app.Resources["PrimaryText"] = primaryText;
                app.Resources["SecondaryText"] = secondaryText;
                app.Resources["PrimaryBrush"] = new SolidColorBrush(primary);
                app.Resources["SecondaryBrush"] = new SolidColorBrush(secondary);
                app.Resources["TertiaryBrush"] = new SolidColorBrush(tertiary);

                // Publish light/dark specific keys so converters can query them directly
                app.Resources["PrimaryLight"] = primaryLight;
                app.Resources["SecondaryLight"] = secondaryLight;
                app.Resources["TertiaryLight"] = tertiaryLight;
                app.Resources["AccentLight"] = accentLight;
                app.Resources["PrimaryDark"] = primaryDark;
                app.Resources["SecondaryDark"] = secondaryDark;
                app.Resources["TertiaryDark"] = tertiaryDark;
                app.Resources["AccentDark"] = accentDark;
                app.Resources["PrimaryTextLight"] = primaryTextLight;
                app.Resources["SecondaryTextLight"] = secondaryTextLight;
                app.Resources["PrimaryTextDark"] = primaryTextDark;
                app.Resources["SecondaryTextDark"] = secondaryTextDark;

                // Background
                Color pageBackground;
                ImageSource? pageBackgroundImageSource = null;

                var wallpaperSupported = IsWallpaperAvailableFor(colorTheme);
                var wallpaperEnabled = _isWallpaperEnabled && wallpaperSupported;

                if (wallpaperEnabled)
                {
                    var mapping = _wallpaperMap[colorTheme];
                    if (!string.IsNullOrWhiteSpace(mapping.light) && !string.IsNullOrWhiteSpace(mapping.dark))
                    {
                        pageBackgroundImageSource = ImageSource.FromFile(isDark ? mapping.dark! : mapping.light!);
                    }
                    else if (!string.IsNullOrWhiteSpace(mapping.single))
                    {
                        pageBackgroundImageSource = ImageSource.FromFile(mapping.single!);
                    }

                    // Slightly stronger overlays for better readability in wallpaper mode
                    if (isDark)
                    {
                        // Dark mode: make overlay a bit darker than before (0.45 vs 0.35)
                        // Temporarily disable transparency: use fully opaque overlay (alpha = 1.0)
                        pageBackground = Color.FromRgba(0, 0, 0, 1.0);
                    }
                    else
                    {
                        // Light mode: a touch stronger to improve contrast on bright wallpapers (0.14 vs 0.10)
                        // Temporarily disable transparency: use fully opaque overlay (alpha = 1.0)
                        pageBackground = Color.FromRgba(255, 255, 255, 1.0);
                    }
                }
                else if (_isColorfulBackgroundEnabled)
                {
                    if (isDark)
                    {
                        var darkened = Darken(secondary, 0.12);
                        // Temporarily disable transparency: use fully opaque color
                        pageBackground = Color.FromRgba(darkened.Red, darkened.Green, darkened.Blue, 1.0);
                    }
                    else
                    {
                        var lightened = Lighten(secondary, 0.22);
                        // Temporarily disable transparency: use fully opaque color
                        pageBackground = Color.FromRgba(lightened.Red, lightened.Green, lightened.Blue, 1.0);
                    }
                }
                else
                {
                    pageBackground = isDark ? Color.FromArgb("#1E1E1E") : Color.FromArgb("#F5F5F5");
                    if (isDark)
                    {
                        var darkened = Darken(pageBackground, 0.12);
                        pageBackground = Color.FromRgba(darkened.Red, darkened.Green, darkened.Blue, pageBackground.Alpha);
                    }
                }

                app.Resources["PageBackgroundImage"] = pageBackgroundImageSource;
                app.Resources["PageBackgroundColor"] = pageBackground;
                app.Resources["PageBackgroundBrush"] = new SolidColorBrush(pageBackground);

                // Adaptive Text Color
                Color adaptiveTextColor = (_isColorfulBackgroundEnabled && !wallpaperEnabled)
                    ? ChooseReadableEnhanced(pageBackground, Colors.White, Color.FromArgb("#000000"))
                    : (isDark ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#000000"));
                app.Resources["AdaptiveTextColor"] = adaptiveTextColor;

                // Frame colors
                Color frameBackgroundColor = isDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#FFFFFF");
                Color frameTextColor = isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#2A2A2A");
                app.Resources["FrameBackgroundColor"] = frameBackgroundColor;
                app.Resources["FrameTextColor"] = frameTextColor;

                // Folder card colors (use translucent accents so the card feels subtle over page background)
                // Restore translucency specifically for folder cards while page background remains opaque.
                var folderBg = Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.12);
                var folderStroke = Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.32);
                 Color folderTextColor = frameTextColor;

                 if (wallpaperEnabled)
                 {
                     // Opaque Secondary background for folder cards
                     var opaqueSecondary = Color.FromRgb(secondary.Red, secondary.Green, secondary.Blue);
                     folderBg = opaqueSecondary;
                     folderStroke = isDark ? Lighten(opaqueSecondary, 0.18) : Darken(opaqueSecondary, 0.18);
                     var candidateText = ChooseReadableEnhanced(opaqueSecondary, Colors.White, Color.FromArgb("#000000"));
                     folderTextColor = EnsureContrastEnhanced(candidateText, opaqueSecondary, RelativeLuminance(opaqueSecondary) > 0.45 ? Colors.Black : Colors.White);
                 }
                 else
                 {
                     if (isDark && _isColorfulBackgroundEnabled)
                     {
                         folderStroke = Color.FromArgb("#2A2A2A");
                         folderTextColor = Color.FromArgb("#000000");
                     }
                     else if (!isDark)
                     {
                         folderStroke = Color.FromArgb("#424242");
                     }
                 }

                 app.Resources["FolderCardBackgroundColor"] = folderBg;
                 app.Resources["FolderCardStrokeColor"] = folderStroke;
                 app.Resources["FolderCardTextColor"] = folderTextColor;

                // Buttons & TabBar & Shell
                var buttonPrimaryText = ChooseReadableEnhanced(primary, Colors.White, Color.FromArgb("#000000"));
                var alt = RelativeLuminance(primary) > 0.45 ? Colors.Black : Colors.White;
                buttonPrimaryText = EnsureContrastEnhanced(buttonPrimaryText, primary, alt);
                if (colorTheme == AppColorTheme.Monochrome && isDark) buttonPrimaryText = Color.FromArgb("#000000");
                app.Resources["ButtonPrimaryText"] = buttonPrimaryText;

                var disabledBg = isDark ? Color.FromArgb("#404040") : Color.FromArgb("#C8C8C8");
                var disabledText = ChooseReadableEnhanced(disabledBg, Colors.White, Color.FromArgb("#000000"));
                disabledText = EnsureContrastEnhanced(disabledText, disabledBg, RelativeLuminance(disabledBg) > 0.45 ? Colors.Black : Colors.White);
                if (colorTheme == AppColorTheme.Monochrome && isDark) disabledText = Color.FromArgb("#E0E0E0");
                app.Resources["ButtonDisabledText"] = disabledText;

                var tabBarBg = secondary;
                var (activeColor, unselectedColor) = GetOptimalTabBarColors(tabBarBg, isDark, colorTheme);
                if (!isDark) { activeColor = Color.FromArgb("#000000"); unselectedColor = Color.FromArgb("#424242"); }
                var shellTitleBg = primary;
                var shellTitleColor = ChooseReadableEnhanced(shellTitleBg, Colors.White, Color.FromArgb("#000000"));
                shellTitleColor = EnsureContrastEnhanced(shellTitleColor, shellTitleBg, RelativeLuminance(shellTitleBg) > 0.45 ? Colors.Black : Colors.White);
                app.Resources["TabBarBackground"] = tabBarBg;
                app.Resources["TabBarForeground"] = activeColor;
                app.Resources["TabBarTitle"] = activeColor;
                app.Resources["TabBarUnselected"] = unselectedColor;
                app.Resources["ShellTitleColor"] = shellTitleColor;
                app.Resources["ShellBackgroundColor"] = shellTitleBg;

                ApplySystemBars(shellTitleBg);

                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Failed to apply color theme: {ex.Message}");
            }
        }

        private void ApplySystemBars(Color background)
        {
#if ANDROID
            try
            {
                var activity = Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(Android.App.Activity)) as Android.App.Activity
                               ?? Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Android.App.Activity;
                if (activity?.Window == null) return;
                var window = activity.Window;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    window.SetStatusBarColor(background.ToPlatform());
                    window.SetNavigationBarColor(background.ToPlatform());
                }

                var luminance = RelativeLuminance(background);
                var useDarkIcons = luminance > 0.55;
                var decorView = window.DecorView;
                var controller = new WindowInsetsControllerCompat(window, decorView);
                controller.AppearanceLightStatusBars = useDarkIcons;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    controller.AppearanceLightNavigationBars = useDarkIcons;
                }
            }
            catch { }
#endif
        }

        private Dictionary<AppColorTheme, ThemeColors> InitializeThemes()
        {
            return new Dictionary<AppColorTheme, ThemeColors>
            {
                [AppColorTheme.Default] = new ThemeColors
                {
                    Name = "Default",
                    PrimaryLight = Color.FromArgb("#512BD4"), SecondaryLight = Color.FromArgb("#DFD8F7"), TertiaryLight = Color.FromArgb("#2B0B98"), AccentLight = Color.FromArgb("#512BD4"),
                    PrimaryDark = Color.FromArgb("#ac99ea"), SecondaryDark = Color.FromArgb("#B8A7E8"), TertiaryDark = Color.FromArgb("#7c4dff"), AccentDark = Color.FromArgb("#ac99ea"),
                    PrimaryTextLight = Color.FromArgb("#242424"), SecondaryTextLight = Color.FromArgb("#666666"), PrimaryTextDark = Color.FromArgb("#FFFFFF"), SecondaryTextDark = Color.FromArgb("#E0E0E0")
                },
                [AppColorTheme.Nature] = new ThemeColors
                {
                    Name = "Nature",
                    PrimaryLight = Color.FromArgb("#2E7D32"), SecondaryLight = Color.FromArgb("#C8E6C9"), TertiaryLight = Color.FromArgb("#1B5E20"), AccentLight = Color.FromArgb("#4CAF50"),
                    PrimaryDark = Color.FromArgb("#81C784"), SecondaryDark = Color.FromArgb("#4CAF50"), TertiaryDark = Color.FromArgb("#66BB6A"), AccentDark = Color.FromArgb("#81C784"),
                    PrimaryTextLight = Color.FromArgb("#1B5E20"), SecondaryTextLight = Color.FromArgb("#2E7D32"), PrimaryTextDark = Color.FromArgb("#E8F5E8"), SecondaryTextDark = Color.FromArgb("#C8E6C9")
                },
                [AppColorTheme.Forest] = new ThemeColors
                {
                    Name = "Forest",
                    PrimaryLight = Color.FromArgb("#1B5E20"), SecondaryLight = Color.FromArgb("#E8F5E9"), TertiaryLight = Color.FromArgb("#2E7D32"), AccentLight = Color.FromArgb("#388E3C"),
                    PrimaryDark = Color.FromArgb("#66BB6A"), SecondaryDark = Color.FromArgb("#43A047"), TertiaryDark = Color.FromArgb("#388E3C"), AccentDark = Color.FromArgb("#81C784"),
                    PrimaryTextLight = Color.FromArgb("#1B5E20"), SecondaryTextLight = Color.FromArgb("#2E7D32"), PrimaryTextDark = Color.FromArgb("#E8F5E9"), SecondaryTextDark = Color.FromArgb("#C8E6C9")
                },
                [AppColorTheme.Autumn] = new ThemeColors
                {
                    Name = "Autumn",
                    PrimaryLight = Color.FromArgb("#b06553"), SecondaryLight = Color.FromArgb("#e0d3cc"), TertiaryLight = Color.FromArgb("#824e3e"), AccentLight = Color.FromArgb("#cc7d7a"),
                    PrimaryDark = Color.FromArgb("#BCAAA4"), SecondaryDark = Color.FromArgb("#A1887F"), TertiaryDark = Color.FromArgb("#8D6E63"), AccentDark = Color.FromArgb("#BCAAA4"),
                    PrimaryTextLight = Color.FromArgb("#3E2723"), SecondaryTextLight = Color.FromArgb("#5D4037"), PrimaryTextDark = Color.FromArgb("#EFEBE9"), SecondaryTextDark = Color.FromArgb("#D7CCC8")
                },
                [AppColorTheme.Warm] = new ThemeColors
                {
                    Name = "Warm",
                    PrimaryLight = Color.FromArgb("#F57C00"), SecondaryLight = Color.FromArgb("#FFF3E0"), TertiaryLight = Color.FromArgb("#E65100"), AccentLight = Color.FromArgb("#FF9800"),
                    PrimaryDark = Color.FromArgb("#FFCC02"), SecondaryDark = Color.FromArgb("#FFB74D"), TertiaryDark = Color.FromArgb("#FF8F00"), AccentDark = Color.FromArgb("#FFCC02"),
                    PrimaryTextLight = Color.FromArgb("#E65100"), SecondaryTextLight = Color.FromArgb("#F57C00"), PrimaryTextDark = Color.FromArgb("#FFF8E1"), SecondaryTextDark = Color.FromArgb("#FFCC80")
                },
                [AppColorTheme.Sunset] = new ThemeColors
                {
                    Name = "Sunset",
                    PrimaryLight = Color.FromArgb("#FB8C00"), SecondaryLight = Color.FromArgb("#FFF3E0"), TertiaryLight = Color.FromArgb("#E65100"), AccentLight = Color.FromArgb("#FF9800"),
                    PrimaryDark = Color.FromArgb("#FFB74D"), SecondaryDark = Color.FromArgb("#FF9800"), TertiaryDark = Color.FromArgb("#F57C00"), AccentDark = Color.FromArgb("#FFCC80"),
                    PrimaryTextLight = Color.FromArgb("#E65100"), SecondaryTextLight = Color.FromArgb("#F57C00"), PrimaryTextDark = Color.FromArgb("#FFF3E0"), SecondaryTextDark = Color.FromArgb("#FFE0B2")
                },
                [AppColorTheme.Vibrant] = new ThemeColors
                {
                    Name = "Vibrant",
                    PrimaryLight = Color.FromArgb("#D32F2F"), SecondaryLight = Color.FromArgb("#FCE4EC"), TertiaryLight = Color.FromArgb("#B71C1C"), AccentLight = Color.FromArgb("#E91E63"),
                    PrimaryDark = Color.FromArgb("#F48FB1"), SecondaryDark = Color.FromArgb("#EC407A"), TertiaryDark = Color.FromArgb("#AD1457"), AccentDark = Color.FromArgb("#F48FB1"),
                    PrimaryTextLight = Color.FromArgb("#B71C1C"), SecondaryTextLight = Color.FromArgb("#C2185B"), PrimaryTextDark = Color.FromArgb("#FCE4EC"), SecondaryTextDark = Color.FromArgb("#F8BBD9")
                },
                [AppColorTheme.Monochrome] = new ThemeColors
                {
                    Name = "Monochrome",
                    PrimaryLight = Color.FromArgb("#424242"), SecondaryLight = Color.FromArgb("#F5F5F5"), TertiaryLight = Color.FromArgb("#212121"), AccentLight = Color.FromArgb("#757575"),
                    PrimaryDark = Color.FromArgb("#E0E0E0"), SecondaryDark = Color.FromArgb("#616161"), TertiaryDark = Color.FromArgb("#9E9E0E").ClampFix(), AccentDark = Color.FromArgb("#BDBDBD"),
                    PrimaryTextLight = Color.FromArgb("#212121"), SecondaryTextLight = Color.FromArgb("#616161"), PrimaryTextDark = Color.FromArgb("#FFFFFF"), SecondaryTextDark = Color.FromArgb("#E0E0E0")
                },
                [AppColorTheme.Navy] = new ThemeColors
                {
                    Name = "Navy",
                    PrimaryLight = Color.FromArgb("#074891"), SecondaryLight = Color.FromArgb("#9fbafc"), TertiaryLight = Color.FromArgb("#0D47A1"), AccentLight = Color.FromArgb("#1976D2"),
                    PrimaryDark = Color.FromArgb("#183b99"), SecondaryDark = Color.FromArgb("#5d6ff0"), TertiaryDark = Color.FromArgb("#2196F3"), AccentDark = Color.FromArgb("#64B5F6"),
                    PrimaryTextLight = Color.FromArgb("#0D47A1"), SecondaryTextLight = Color.FromArgb("#1565C0"), PrimaryTextDark = Color.FromArgb("#E3F2FD"), SecondaryTextDark = Color.FromArgb("#BBDEFB")
                },
                [AppColorTheme.Mint] = new ThemeColors
                {
                    Name = "Mint",
                    PrimaryLight = Color.FromArgb("#05f2de"), SecondaryLight = Color.FromArgb("#93faf6"), TertiaryLight = Color.FromArgb("#018585"), AccentLight = Color.FromArgb("#26dad1"),
                    PrimaryDark = Color.FromArgb("#04d9d2"), SecondaryDark = Color.FromArgb("#26dad4"), TertiaryDark = Color.FromArgb("#00d4c9"), AccentDark = Color.FromArgb("#4ddce1"),
                    PrimaryTextLight = Color.FromArgb("#006064"), SecondaryTextLight = Color.FromArgb("#00838F"), PrimaryTextDark = Color.FromArgb("#bef7f7"), SecondaryTextDark = Color.FromArgb("#a8eff7")
                },
                [AppColorTheme.Sky] = new ThemeColors
                {
                    Name = "Sky",
                    PrimaryLight = Color.FromArgb("#03A9F4"), SecondaryLight = Color.FromArgb("#E1F5FE"), TertiaryLight = Color.FromArgb("#0288D1"), AccentLight = Color.FromArgb("#29B6F6"),
                    PrimaryDark = Color.FromArgb("#81D4FA"), SecondaryDark = Color.FromArgb("#4FC3F7"), TertiaryDark = Color.FromArgb("#039BE5"), AccentDark = Color.FromArgb("#81D4FA"),
                    PrimaryTextLight = Color.FromArgb("#01579B"), SecondaryTextLight = Color.FromArgb("#0277BD"), PrimaryTextDark = Color.FromArgb("#E1F5FE"), SecondaryTextDark = Color.FromArgb("#B3E5FC")
                },
                [AppColorTheme.Bubblegum] = new ThemeColors
                {
                    Name = "Bubblegum",
                    PrimaryLight = Color.FromArgb("#F48FB1"), SecondaryLight = Color.FromArgb("#E1F5FE"), TertiaryLight = Color.FromArgb("#F06292"), AccentLight = Color.FromArgb("#81D4FA"),
                    PrimaryDark = Color.FromArgb("#F8BBD0"), SecondaryDark = Color.FromArgb("#4FC3F7"), TertiaryDark = Color.FromArgb("#EC407A"), AccentDark = Color.FromArgb("#29B6F6"),
                    PrimaryTextLight = Color.FromArgb("#AD1457"), SecondaryTextLight = Color.FromArgb("#0288D1"), PrimaryTextDark = Color.FromArgb("#FCE4EC"), SecondaryTextDark = Color.FromArgb("#E1F5FE")
                }
            };
        }
    }

    // Small helper to fix a typo color if needed; no-op for valid values
    internal static class ColorExtensions
    {
        public static Color ClampFix(this Color color) => color;
    }
}