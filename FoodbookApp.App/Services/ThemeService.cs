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
            System.Diagnostics.Debug.WriteLine($"[ThemeService] GetOptimalTabBarColors - Background: {background}, Luminance: {luminance:F3}, isDark: {isDark}");
            
            Color active, unselected;
            
            if (luminance > 0.4) // Light background
            {
                active = Color.FromArgb("#000000");
                unselected = Color.FromArgb("#424242");
            }
            else
            {
                active = Color.FromArgb("#FFFFFF");
                unselected = Color.FromArgb("#E0E0E0");
            }
            
            if (colorTheme == AppColorTheme.Monochrome && isDark && luminance < 0.3)
            {
                active = Color.FromArgb("#FFFFFF");
                unselected = Color.FromArgb("#CCCCCC");
            }
            
            var activeContrast = ContrastRatio(active, background);
            var unselectedContrast = ContrastRatio(unselected, background);
            
            if (activeContrast < 4.5)
            {
                active = luminance > 0.5 ? Color.FromArgb("#000000") : Color.FromArgb("#FFFFFF");
            }
            
            if (unselectedContrast < 3.0)
            {
                unselected = luminance > 0.5 ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
            }
            
            return (active, unselected);
        }

        private void ApplyColorTheme(AppColorTheme colorTheme)
        {
            try
            {
                var app = Application.Current; 
                if (app?.Resources == null) return;
                var themeColors = GetThemeColors(colorTheme);
                
                bool isDark;
                if (_currentTheme == Foodbook.Models.AppTheme.Light)
                    isDark = false;
                else if (_currentTheme == Foodbook.Models.AppTheme.Dark)
                    isDark = true;
                else
                    isDark = app.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark ||
                             (app.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified && app.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark);

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

                var buttonPrimaryText = ChooseReadableEnhanced(primary, Colors.White, Color.FromArgb("#000000"));
                var alt = RelativeLuminance(primary) > 0.45 ? Colors.Black : Colors.White;
                buttonPrimaryText = EnsureContrastEnhanced(buttonPrimaryText, primary, alt);
                if (colorTheme == AppColorTheme.Monochrome && isDark)
                    buttonPrimaryText = Color.FromArgb("#000000");
                app.Resources["ButtonPrimaryText"] = buttonPrimaryText;

                var disabledBg = isDark ? Color.FromArgb("#404040") : Color.FromArgb("#C8C8C8");
                var disabledText = ChooseReadableEnhanced(disabledBg, Colors.White, Color.FromArgb("#000000"));
                disabledText = EnsureContrastEnhanced(disabledText, disabledBg, RelativeLuminance(disabledBg) > 0.45 ? Colors.Black : Colors.White);
                if (colorTheme == AppColorTheme.Monochrome && isDark)
                    disabledText = Color.FromArgb("#E0E0E0");
                app.Resources["ButtonDisabledText"] = disabledText;

                var tabBarBg = secondary;
                var (activeColor, unselectedColor) = GetOptimalTabBarColors(tabBarBg, isDark, colorTheme);

                // W jasnym motywie wymuszamy czarny kolor aktywny i tytu³u, aby zapewniæ czytelnoœæ
                if (!isDark)
                {
                    activeColor = Color.FromArgb("#000000");
                    unselectedColor = Color.FromArgb("#424242");
                }
                
                var shellTitleBg = primary;
                var shellTitleColor = ChooseReadableEnhanced(shellTitleBg, Colors.White, Color.FromArgb("#000000"));
                shellTitleColor = EnsureContrastEnhanced(shellTitleColor, shellTitleBg, RelativeLuminance(shellTitleBg) > 0.45 ? Colors.Black : Colors.White);
                
                System.Diagnostics.Debug.WriteLine($"[ThemeService] TabBar Background: {tabBarBg}, Active: {activeColor}, Unselected: {unselectedColor}");
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Shell Title Background: {shellTitleBg}, Title Color: {shellTitleColor}");
                
                app.Resources["TabBarBackground"] = tabBarBg;
                app.Resources["TabBarForeground"] = activeColor; // aktywna ikona/tekst
                app.Resources["TabBarTitle"] = activeColor;      // tytu³ aktywnej zak³adki
                app.Resources["TabBarUnselected"] = unselectedColor; // nieaktywne ikony/teksty
                app.Resources["ShellTitleColor"] = shellTitleColor;
                app.Resources["ShellBackgroundColor"] = shellTitleBg;
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
                [AppColorTheme.Warm] = new ThemeColors
                {
                    Name = "Warm",
                    PrimaryLight = Color.FromArgb("#F57C00"), SecondaryLight = Color.FromArgb("#FFF3E0"), TertiaryLight = Color.FromArgb("#E65100"), AccentLight = Color.FromArgb("#FF9800"),
                    PrimaryDark = Color.FromArgb("#FFCC02"), SecondaryDark = Color.FromArgb("#FFB74D"), TertiaryDark = Color.FromArgb("#FF8F00"), AccentDark = Color.FromArgb("#FFCC02"),
                    PrimaryTextLight = Color.FromArgb("#E65100"), SecondaryTextLight = Color.FromArgb("#F57C00"), PrimaryTextDark = Color.FromArgb("#FFF8E1"), SecondaryTextDark = Color.FromArgb("#FFCC80")
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
                    PrimaryDark = Color.FromArgb("#E0E0E0"), SecondaryDark = Color.FromArgb("#616161"), TertiaryDark = Color.FromArgb("#9E9E9E"), AccentDark = Color.FromArgb("#BDBDBD"),
                    PrimaryTextLight = Color.FromArgb("#212121"), SecondaryTextLight = Color.FromArgb("#616161"), PrimaryTextDark = Color.FromArgb("#FFFFFF"), SecondaryTextDark = Color.FromArgb("#E0E0E0")
                }
            };
        }
    }
}