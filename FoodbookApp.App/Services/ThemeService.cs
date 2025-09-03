using Foodbook.Models;
using Application = Microsoft.Maui.Controls.Application;

namespace Foodbook.Services
{
    public class ThemeService : IThemeService
    {
        private Foodbook.Models.AppTheme _currentTheme = Foodbook.Models.AppTheme.System;
        private AppColorTheme _currentColorTheme = AppColorTheme.Default;
        private readonly Dictionary<AppColorTheme, ThemeColors> _availableColorThemes;

        public ThemeService()
        {
            _availableColorThemes = InitializeThemes();
        }

        public Foodbook.Models.AppTheme GetCurrentTheme() => _currentTheme;
        public AppColorTheme GetCurrentColorTheme() => _currentColorTheme;
        public Dictionary<AppColorTheme, ThemeColors> GetAvailableColorThemes() => _availableColorThemes;
        public ThemeColors GetThemeColors(AppColorTheme colorTheme) => _availableColorThemes.TryGetValue(colorTheme, out var colors) ? colors : _availableColorThemes[AppColorTheme.Default];

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
                ApplyColorTheme(_currentColorTheme); // reapply palette
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

        // --- Enhanced Contrast helpers ---
        private static double RelativeLuminance(Color c)
        {
            double Channel(double ch) { ch /= 255.0; return ch <= 0.03928 ? ch / 12.92 : Math.Pow((ch + 0.055) / 1.055, 2.4); }
            return 0.2126 * Channel(c.Red) + 0.7152 * Channel(c.Green) + 0.0722 * Channel(c.Blue);
        }
        
        private static double ContrastRatio(Color a, Color b)
        {
            var l1 = RelativeLuminance(a) + 0.05; var l2 = RelativeLuminance(b) + 0.05; return l1 > l2 ? l1 / l2 : l2 / l1;
        }
        
        /// <summary>
        /// Enhanced readable color selection with higher contrast thresholds for better readability
        /// </summary>
        private static Color ChooseReadableEnhanced(Color background, Color preferredLight, Color preferredDark) 
        {
            var luminance = RelativeLuminance(background);
            // Lowered threshold from 0.55 to 0.45 for more aggressive contrast selection
            return luminance > 0.45 ? preferredDark : preferredLight;
        }
        
        /// <summary>
        /// Ensures minimum contrast ratio of 4.5:1 (WCAG AA standard) instead of 3.0:1
        /// </summary>
        private static Color EnsureContrastEnhanced(Color foreground, Color background, Color fallback) 
        {
            // Enhanced minimum contrast ratio from 3.0 to 4.5 for better readability
            return ContrastRatio(foreground, background) < 4.5 ? fallback : foreground;
        }
        
        /// <summary>
        /// Gets high contrast text colors optimized for TabBar visibility
        /// </summary>
        private static (Color active, Color unselected) GetOptimalTabBarColors(Color background, bool isDark, AppColorTheme colorTheme)
        {
            var luminance = RelativeLuminance(background);
            
            Color active, unselected;
            
            if (luminance > 0.4) // Light background - use very dark text
            {
                // For light backgrounds, use pure black or very dark gray for maximum contrast
                active = Color.FromArgb("#000000"); // Pure black for active
                unselected = Color.FromArgb("#424242"); // Dark gray for unselected
            }
            else // Dark background - use very light text
            {
                // For dark backgrounds, use pure white or very light gray for maximum contrast
                active = Color.FromArgb("#FFFFFF"); // Pure white for active
                unselected = Color.FromArgb("#E0E0E0"); // Light gray for unselected
            }
            
            // Special handling for Monochrome theme in dark mode
            if (colorTheme == AppColorTheme.Monochrome && isDark)
            {
                if (luminance < 0.3) 
                {
                    active = Color.FromArgb("#FFFFFF"); // Pure white for maximum contrast
                    unselected = Color.FromArgb("#CCCCCC"); // Light gray for unselected
                }
            }
            
            // Ensure minimum contrast ratios are met
            if (ContrastRatio(active, background) < 4.5)
            {
                active = luminance > 0.5 ? Color.FromArgb("#000000") : Color.FromArgb("#FFFFFF");
            }
            
            if (ContrastRatio(unselected, background) < 3.0)
            {
                unselected = luminance > 0.5 ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
            }
            
            return (active, unselected);
        }

        private void ApplyColorTheme(AppColorTheme colorTheme)
        {
            try
            {
                var app = Application.Current; if (app?.Resources == null) return;
                var themeColors = GetThemeColors(colorTheme);
                bool isDark = app.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark ||
                              (app.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified && app.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark);

                // Base palette
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

                // Button text (primary buttons) - using enhanced contrast methods
                var buttonPrimaryText = ChooseReadableEnhanced(primary, Colors.White, Color.FromArgb("#000000"));
                var alt = RelativeLuminance(primary) > 0.45 ? Colors.Black : Colors.White;
                buttonPrimaryText = EnsureContrastEnhanced(buttonPrimaryText, primary, alt);

                // Monochrome DARK special case: force dark text on light primary (#E0E0E0) to meet user request
                if (colorTheme == AppColorTheme.Monochrome && isDark)
                {
                    buttonPrimaryText = Color.FromArgb("#000000"); // Pure black for maximum contrast
                }
                app.Resources["ButtonPrimaryText"] = buttonPrimaryText;

                // Disabled button text: ensure readable on disabled backgrounds using enhanced contrast
                var disabledBg = isDark ? Color.FromArgb("#404040") : Color.FromArgb("#C8C8C8");
                var disabledText = ChooseReadableEnhanced(disabledBg, Colors.White, Color.FromArgb("#000000"));
                disabledText = EnsureContrastEnhanced(disabledText, disabledBg, RelativeLuminance(disabledBg) > 0.45 ? Colors.Black : Colors.White);
                if (colorTheme == AppColorTheme.Monochrome && isDark)
                    disabledText = Color.FromArgb("#E0E0E0"); // light text on dark disabled background
                app.Resources["ButtonDisabledText"] = disabledText;

                // TabBar colors - using optimized high-contrast method
                var tabBarBg = secondary;
                var (activeColor, unselectedColor) = GetOptimalTabBarColors(tabBarBg, isDark, colorTheme);
                
                app.Resources["TabBarBackground"] = tabBarBg;
                app.Resources["TabBarForeground"] = activeColor;
                app.Resources["TabBarTitle"] = activeColor;
                app.Resources["TabBarUnselected"] = unselectedColor;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Failed to apply color theme: {ex.Message}");
            }
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
                }
            };
        }
    }
}