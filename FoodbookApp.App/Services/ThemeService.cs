using Foodbook.Models;
using FoodbookApp.Interfaces;
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
        private bool _isColorfulBackgroundEnabled = false; // NEW: colorful background option
        private readonly Dictionary<AppColorTheme, ThemeColors> _availableColorThemes;

        public ThemeService()
        {
            _availableColorThemes = InitializeThemes();
        }

        public Foodbook.Models.AppTheme GetCurrentTheme() => _currentTheme;
        public AppColorTheme GetCurrentColorTheme() => _currentColorTheme;
        public Dictionary<AppColorTheme, ThemeColors> GetAvailableColorThemes() => _availableColorThemes;
        public ThemeColors GetThemeColors(AppColorTheme colorTheme) => _availableColorThemes.TryGetValue(colorTheme, out var colors) ? colors : _availableColorThemes[AppColorTheme.Default];
        
        // NEW: Colorful background methods
        public bool GetIsColorfulBackgroundEnabled() => _isColorfulBackgroundEnabled;
        
        public void SetColorfulBackground(bool useColorfulBackground)
        {
            _isColorfulBackgroundEnabled = useColorfulBackground;
            // Immediately reapply color theme to update background colors
            ApplyColorTheme(_currentColorTheme);
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Colorful background set to: {useColorfulBackground}");
        }

        public void SetTheme(Foodbook.Models.AppTheme theme)
        {
            _currentTheme = theme;
            try
            {
                var application = Application.Current;
                if (application == null) return;
                application.UserAppTheme = theme switch
                {
                    Foodbook.Models.AppTheme.Light => Microsoft.Maui.ApplicationModel.AppTheme.Light,
                    Foodbook.Models.AppTheme.Dark => Microsoft.Maui.ApplicationModel.AppTheme.Dark,
                    _ => Microsoft.Maui.ApplicationModel.AppTheme.Unspecified
                };
                ApplyColorTheme(_currentColorTheme); // reapply palette (also updates system bars)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Failed to set theme: {ex.Message}");
            }
        }

        public void SetColorTheme(AppColorTheme colorTheme)
        {
            _currentColorTheme = colorTheme;
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
                ApplySystemBars(shellBg); // shellBg is a reference type color
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] UpdateSystemBars error: {ex.Message}");
            }
        }

        private static Color? TryGetColor(Application app, string key)
        {
            if (app.Resources.TryGetValue(key, out var obj) && obj is Color c) return c; return null;
        }

        // --- Helpers ---
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
            var activeContrast = ContrastRatio(active, background); var unselectedContrast = ContrastRatio(unselected, background);
            if (activeContrast < 4.5) active = luminance > 0.5 ? Color.FromArgb("#000000") : Color.FromArgb("#FFFFFF");
            if (unselectedContrast < 3.0) unselected = luminance > 0.5 ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
            return (active, unselected);
        }

        private static Color Lighten(Color color, double factor)
        {
            // factor: 0 -> no change, 1 -> white
            factor = Math.Clamp(factor, 0, 1);
            return Color.FromRgb(
                color.Red + (1 - color.Red) * factor,
                color.Green + (1 - color.Green) * factor,
                color.Blue + (1 - color.Blue) * factor
            );
        }

        private static Color Darken(Color color, double factor)
        {
            // factor: 0 -> no change, 1 -> black
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

                var primary = isDark ? themeColors.PrimaryDark : themeColors.PrimaryLight;
                var secondary = isDark ? themeColors.SecondaryDark : themeColors.SecondaryLight;
                var tertiary = isDark ? themeColors.TertiaryDark : themeColors.TertiaryLight;
                var accent = isDark ? themeColors.AccentDark : themeColors.AccentLight;
                var primaryText = isDark ? themeColors.PrimaryTextDark : themeColors.PrimaryTextLight;
                var secondaryText = isDark ? themeColors.SecondaryTextDark : themeColors.SecondaryTextLight;

                app.Resources["Primary"] = primary;
                app.Resources["Secondary"] = secondary;
                app.Resources["Tertiary"] = tertiary;
                app.Resources["Accent"] = accent;
                app.Resources["PrimaryText"] = primaryText;
                app.Resources["SecondaryText"] = secondaryText;
                app.Resources["PrimaryBrush"] = new SolidColorBrush(primary);
                app.Resources["SecondaryBrush"] = new SolidColorBrush(secondary);
                app.Resources["TertiaryBrush"] = new SolidColorBrush(tertiary);

                // UPDATED: Dynamic page background colors with darker colorful backgrounds
                Color pageBackground;
                if (_isColorfulBackgroundEnabled)
                {
                    var secondaryColor = secondary;
                    if (isDark)
                    {
                        // Darker in dark mode: reduce lightening and opacity
                        var lightened = Lighten(secondaryColor, 0.35); // Reduced from 0.55 to 0.35
                        pageBackground = Color.FromRgba(lightened.Red, lightened.Green, lightened.Blue, 0.35); // Reduced from 0.55 to 0.35
                    }
                    else
                    {
                        // Darker in light mode: darken secondary and reduce opacity
                        var darkened = Darken(secondaryColor, 0.15); // Slightly darken the secondary color
                        pageBackground = Color.FromRgba(darkened.Red, darkened.Green, darkened.Blue, 0.25); // Reduced from 0.36 to 0.25
                    }
                }
                else
                {
                    // Default neutral gray backgrounds
                    pageBackground = isDark ? Color.FromArgb("#1E1E1E") : Color.FromArgb("#F5F5F5");
                }
                
                app.Resources["PageBackgroundColor"] = pageBackground;
                app.Resources["PageBackgroundBrush"] = new SolidColorBrush(pageBackground);

                // NEW: Add text color that adapts to background state
                Color adaptiveTextColor;
                if (_isColorfulBackgroundEnabled)
                {
                    // For colorful backgrounds, ensure good contrast
                    adaptiveTextColor = ChooseReadableEnhanced(pageBackground, Colors.White, Color.FromArgb("#000000"));
                }
                else
                {
                    // For gray backgrounds, use standard text colors
                    adaptiveTextColor = isDark ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#000000");
                }
                app.Resources["AdaptiveTextColor"] = adaptiveTextColor;

                // UPDATED: Frame and content colors that adapt to colorful background state
                Color frameBackgroundColor;
                Color frameTextColor;
                if (_isColorfulBackgroundEnabled)
                {
                    // For colorful backgrounds, use contrasting frame colors
                    frameBackgroundColor = isDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#FFFFFF");
                    frameTextColor = isDark ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#000000");
                }
                else
                {
                    // For gray backgrounds, use enhanced contrast
                    frameBackgroundColor = isDark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#FFFFFF");
                    frameTextColor = isDark ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#2A2A2A");
                }
                app.Resources["FrameBackgroundColor"] = frameBackgroundColor;
                app.Resources["FrameTextColor"] = frameTextColor;

                // UPDATED: Folder card colors derived from Primary (pale/translucent) to follow theme
                var folderBg = Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.12);
                var folderStroke = Color.FromRgba(primary.Red, primary.Green, primary.Blue, 0.32);
                app.Resources["FolderCardBackgroundColor"] = folderBg;
                app.Resources["FolderCardStrokeColor"] = folderStroke;

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

                // Raise ThemeChanged so components can refresh if they need custom handling
                ThemeChanged?.Invoke(this, EventArgs.Empty);
                
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Applied color theme {colorTheme} with colorful background: {_isColorfulBackgroundEnabled}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Failed to apply color theme: {ex.Message}");
            }
        }

        // Platform-specific system bars update (status + navigation) - Android only
        private void ApplySystemBars(Color background)
        {
#if ANDROID
            try
            {
                var activity = Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(Android.App.Activity)) as Android.App.Activity
                               ?? Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as Android.App.Activity;
                if (activity?.Window == null) return;
                var window = activity.Window;

                // Guard platform APIs by OS version to satisfy platform compatibility analyzers
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    window.SetStatusBarColor(background.ToPlatform());
                    window.SetNavigationBarColor(background.ToPlatform());
                }

                var luminance = RelativeLuminance(background);
                var useDarkIcons = luminance > 0.55; // threshold
                var decorView = window.DecorView;

                // Use AndroidX WindowInsetsControllerCompat to avoid deprecated APIs and handle pre/post R consistently
                var controller = new WindowInsetsControllerCompat(window, decorView);
                controller.AppearanceLightStatusBars = useDarkIcons;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    controller.AppearanceLightNavigationBars = useDarkIcons;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] ApplySystemBars error: {ex.Message}");
            }
#endif
        }

        private Dictionary<AppColorTheme, ThemeColors> InitializeThemes()
        {
            return new Dictionary<AppColorTheme, ThemeColors>
            {
                // Default Purple Theme
                [AppColorTheme.Default] = new ThemeColors
                {
                    Name = "Default",
                    PrimaryLight = Color.FromArgb("#512BD4"), SecondaryLight = Color.FromArgb("#DFD8F7"), TertiaryLight = Color.FromArgb("#2B0B98"), AccentLight = Color.FromArgb("#512BD4"),
                    PrimaryDark = Color.FromArgb("#ac99ea"), SecondaryDark = Color.FromArgb("#B8A7E8"), TertiaryDark = Color.FromArgb("#7c4dff"), AccentDark = Color.FromArgb("#ac99ea"),
                    PrimaryTextLight = Color.FromArgb("#242424"), SecondaryTextLight = Color.FromArgb("#666666"), PrimaryTextDark = Color.FromArgb("#FFFFFF"), SecondaryTextDark = Color.FromArgb("#E0E0E0")
                },
                // Nature Green Theme
                [AppColorTheme.Nature] = new ThemeColors
                {
                    Name = "Nature",
                    PrimaryLight = Color.FromArgb("#2E7D32"), SecondaryLight = Color.FromArgb("#C8E6C9"), TertiaryLight = Color.FromArgb("#1B5E20"), AccentLight = Color.FromArgb("#4CAF50"),
                    PrimaryDark = Color.FromArgb("#81C784"), SecondaryDark = Color.FromArgb("#4CAF50"), TertiaryDark = Color.FromArgb("#66BB6A"), AccentDark = Color.FromArgb("#81C784"),
                    PrimaryTextLight = Color.FromArgb("#1B5E20"), SecondaryTextLight = Color.FromArgb("#2E7D32"), PrimaryTextDark = Color.FromArgb("#E8F5E8"), SecondaryTextDark = Color.FromArgb("#C8E6C9")
                },
                // Warm Theme
                [AppColorTheme.Warm] = new ThemeColors
                {
                    Name = "Warm",
                    PrimaryLight = Color.FromArgb("#F57C00"), SecondaryLight = Color.FromArgb("#FFF3E0"), TertiaryLight = Color.FromArgb("#E65100"), AccentLight = Color.FromArgb("#FF9800"),
                    PrimaryDark = Color.FromArgb("#FFCC02"), SecondaryDark = Color.FromArgb("#FFB74D"), TertiaryDark = Color.FromArgb("#FF8F00"), AccentDark = Color.FromArgb("#FFCC02"),
                    PrimaryTextLight = Color.FromArgb("#E65100"), SecondaryTextLight = Color.FromArgb("#F57C00"), PrimaryTextDark = Color.FromArgb("#FFF8E1"), SecondaryTextDark = Color.FromArgb("#FFCC80")
                },
                // Vibrant Theme
                [AppColorTheme.Vibrant] = new ThemeColors
                {
                    Name = "Vibrant",
                    PrimaryLight = Color.FromArgb("#D32F2F"), SecondaryLight = Color.FromArgb("#FCE4EC"), TertiaryLight = Color.FromArgb("#B71C1C"), AccentLight = Color.FromArgb("#E91E63"),
                    PrimaryDark = Color.FromArgb("#F48FB1"), SecondaryDark = Color.FromArgb("#EC407A"), TertiaryDark = Color.FromArgb("#AD1457"), AccentDark = Color.FromArgb("#F48FB1"),
                    PrimaryTextLight = Color.FromArgb("#B71C1C"), SecondaryTextLight = Color.FromArgb("#C2185B"), PrimaryTextDark = Color.FromArgb("#FCE4EC"), SecondaryTextDark = Color.FromArgb("#F8BBD9")
                },
                // Monochrome Theme
                [AppColorTheme.Monochrome] = new ThemeColors
                {
                    Name = "Monochrome",
                    PrimaryLight = Color.FromArgb("#424242"), SecondaryLight = Color.FromArgb("#F5F5F5"), TertiaryLight = Color.FromArgb("#212121"), AccentLight = Color.FromArgb("#757575"),
                    PrimaryDark = Color.FromArgb("#E0E0E0"), SecondaryDark = Color.FromArgb("#616161"), TertiaryDark = Color.FromArgb("#9E9E9E"), AccentDark = Color.FromArgb("#BDBDBD"),
                    PrimaryTextLight = Color.FromArgb("#212121"), SecondaryTextLight = Color.FromArgb("#616161"), PrimaryTextDark = Color.FromArgb("#FFFFFF"), SecondaryTextDark = Color.FromArgb("#E0E0E0")
                },
                // NEW: Navy Theme (Ciemny Niebieski)
                [AppColorTheme.Navy] = new ThemeColors
                {
                    Name = "Navy",
                    PrimaryLight = Color.FromArgb("#1565C0"), SecondaryLight = Color.FromArgb("#E3F2FD"), TertiaryLight = Color.FromArgb("#0D47A1"), AccentLight = Color.FromArgb("#1976D2"),
                    PrimaryDark = Color.FromArgb("#64B5F6"), SecondaryDark = Color.FromArgb("#42A5F5"), TertiaryDark = Color.FromArgb("#2196F3"), AccentDark = Color.FromArgb("#64B5F6"),
                    PrimaryTextLight = Color.FromArgb("#0D47A1"), SecondaryTextLight = Color.FromArgb("#1565C0"), PrimaryTextDark = Color.FromArgb("#E3F2FD"), SecondaryTextDark = Color.FromArgb("#BBDEFB")
                },
                // NEW: Autumn Theme (Br¹zowy Jesienny)
                [AppColorTheme.Autumn] = new ThemeColors
                {
                    Name = "Autumn",
                    PrimaryLight = Color.FromArgb("#8D6E63"), SecondaryLight = Color.FromArgb("#EFEBE9"), TertiaryLight = Color.FromArgb("#5D4037"), AccentLight = Color.FromArgb("#A1887F"),
                    PrimaryDark = Color.FromArgb("#BCAAA4"), SecondaryDark = Color.FromArgb("#A1887F"), TertiaryDark = Color.FromArgb("#8D6E63"), AccentDark = Color.FromArgb("#BCAAA4"),
                    PrimaryTextLight = Color.FromArgb("#3E2723"), SecondaryTextLight = Color.FromArgb("#5D4037"), PrimaryTextDark = Color.FromArgb("#EFEBE9"), SecondaryTextDark = Color.FromArgb("#D7CCC8")
                },
                // NEW: Mint Theme (Miêtowy)
                [AppColorTheme.Mint] = new ThemeColors
                {
                    Name = "Mint",
                    PrimaryLight = Color.FromArgb("#00ACC1"), SecondaryLight = Color.FromArgb("#E0F7FA"), TertiaryLight = Color.FromArgb("#006064"), AccentLight = Color.FromArgb("#26C6DA"),
                    PrimaryDark = Color.FromArgb("#4DD0E1"), SecondaryDark = Color.FromArgb("#26C6DA"), TertiaryDark = Color.FromArgb("#00BCD4"), AccentDark = Color.FromArgb("#4DD0E1"),
                    PrimaryTextLight = Color.FromArgb("#006064"), SecondaryTextLight = Color.FromArgb("#00838F"), PrimaryTextDark = Color.FromArgb("#E0F7FA"), SecondaryTextDark = Color.FromArgb("#B2EBF2")
                },
                // NEW: Forest Theme (Ciemnozielony)
                [AppColorTheme.Forest] = new ThemeColors
                {
                    Name = "Forest",
                    PrimaryLight = Color.FromArgb("#1B5E20"), SecondaryLight = Color.FromArgb("#E8F5E9"), TertiaryLight = Color.FromArgb("#2E7D32"), AccentLight = Color.FromArgb("#388E3C"),
                    PrimaryDark = Color.FromArgb("#66BB6A"), SecondaryDark = Color.FromArgb("#43A047"), TertiaryDark = Color.FromArgb("#388E3C"), AccentDark = Color.FromArgb("#81C784"),
                    PrimaryTextLight = Color.FromArgb("#1B5E20"), SecondaryTextLight = Color.FromArgb("#2E7D32"), PrimaryTextDark = Color.FromArgb("#E8F5E9"), SecondaryTextDark = Color.FromArgb("#C8E6C9")
                },
                // NEW: Sunset Theme (Pomarañczowy)
                [AppColorTheme.Sunset] = new ThemeColors
                {
                    Name = "Sunset",
                    PrimaryLight = Color.FromArgb("#FB8C00"), SecondaryLight = Color.FromArgb("#FFF3E0"), TertiaryLight = Color.FromArgb("#E65100"), AccentLight = Color.FromArgb("#FF9800"),
                    PrimaryDark = Color.FromArgb("#FFB74D"), SecondaryDark = Color.FromArgb("#FF9800"), TertiaryDark = Color.FromArgb("#F57C00"), AccentDark = Color.FromArgb("#FFCC80"),
                    PrimaryTextLight = Color.FromArgb("#E65100"), SecondaryTextLight = Color.FromArgb("#F57C00"), PrimaryTextDark = Color.FromArgb("#FFF3E0"), SecondaryTextDark = Color.FromArgb("#FFE0B2")
                },
                // NEW: Bubblegum Theme (Ró¿ + B³êkit)
                [AppColorTheme.Bubblegum] = new ThemeColors
                {
                    Name = "Bubblegum",
                    PrimaryLight = Color.FromArgb("#F48FB1"), SecondaryLight = Color.FromArgb("#E1F5FE"), TertiaryLight = Color.FromArgb("#F06292"), AccentLight = Color.FromArgb("#81D4FA"),
                    PrimaryDark = Color.FromArgb("#F8BBD0"), SecondaryDark = Color.FromArgb("#4FC3F7"), TertiaryDark = Color.FromArgb("#EC407A"), AccentDark = Color.FromArgb("#29B6F6"),
                    PrimaryTextLight = Color.FromArgb("#AD1457"), SecondaryTextLight = Color.FromArgb("#0288D1"), PrimaryTextDark = Color.FromArgb("#FCE4EC"), SecondaryTextDark = Color.FromArgb("#E1F5FE")
                },
                // NEW: Sky Theme (SkyBlue)
                [AppColorTheme.Sky] = new ThemeColors
                {
                    Name = "Sky",
                    PrimaryLight = Color.FromArgb("#03A9F4"), SecondaryLight = Color.FromArgb("#E1F5FE"), TertiaryLight = Color.FromArgb("#0288D1"), AccentLight = Color.FromArgb("#29B6F6"),
                    PrimaryDark = Color.FromArgb("#81D4FA"), SecondaryDark = Color.FromArgb("#4FC3F7"), TertiaryDark = Color.FromArgb("#039BE5"), AccentDark = Color.FromArgb("#81D4FA"),
                    PrimaryTextLight = Color.FromArgb("#01579B"), SecondaryTextLight = Color.FromArgb("#0277BD"), PrimaryTextDark = Color.FromArgb("#E1F5FE"), SecondaryTextDark = Color.FromArgb("#B3E5FC")
                }

            };
        }
    }
}