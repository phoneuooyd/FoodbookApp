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

        public Foodbook.Models.AppTheme GetCurrentTheme()
        {
            return _currentTheme;
        }

        public void SetTheme(Foodbook.Models.AppTheme theme)
        {
            _currentTheme = theme;
            
            try
            {
                var application = Application.Current;
                if (application == null) return;

                switch (theme)
                {
                    case Foodbook.Models.AppTheme.Light:
                        application.UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Light;
                        break;
                    case Foodbook.Models.AppTheme.Dark:
                        application.UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Dark;
                        break;
                    case Foodbook.Models.AppTheme.System:
                        application.UserAppTheme = Microsoft.Maui.ApplicationModel.AppTheme.Unspecified;
                        break;
                }
                
                // Reapply color theme to ensure colors are updated for the new theme
                ApplyColorTheme(_currentColorTheme);
                
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Applied theme: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Failed to set theme: {ex.Message}");
            }
        }

        public AppColorTheme GetCurrentColorTheme()
        {
            return _currentColorTheme;
        }

        public void SetColorTheme(AppColorTheme colorTheme)
        {
            _currentColorTheme = colorTheme;
            ApplyColorTheme(colorTheme);
            System.Diagnostics.Debug.WriteLine($"[ThemeService] Applied color theme: {colorTheme}");
        }

        public Dictionary<AppColorTheme, ThemeColors> GetAvailableColorThemes()
        {
            return _availableColorThemes;
        }

        public ThemeColors GetThemeColors(AppColorTheme colorTheme)
        {
            return _availableColorThemes.TryGetValue(colorTheme, out var colors) 
                ? colors 
                : _availableColorThemes[AppColorTheme.Default];
        }

        private void ApplyColorTheme(AppColorTheme colorTheme)
        {
            try
            {
                var application = Application.Current;
                if (application?.Resources == null) return;

                var themeColors = GetThemeColors(colorTheme);
                var isDarkMode = application.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark ||
                               (application.UserAppTheme == Microsoft.Maui.ApplicationModel.AppTheme.Unspecified &&
                                application.RequestedTheme == Microsoft.Maui.ApplicationModel.AppTheme.Dark);

                // Update dynamic resources based on current theme mode
                application.Resources["Primary"] = isDarkMode ? themeColors.PrimaryDark : themeColors.PrimaryLight;
                application.Resources["Secondary"] = isDarkMode ? themeColors.SecondaryDark : themeColors.SecondaryLight;
                application.Resources["Tertiary"] = isDarkMode ? themeColors.TertiaryDark : themeColors.TertiaryLight;
                application.Resources["Accent"] = isDarkMode ? themeColors.AccentDark : themeColors.AccentLight;
                
                // Update text colors
                application.Resources["PrimaryText"] = isDarkMode ? themeColors.PrimaryTextDark : themeColors.PrimaryTextLight;
                application.Resources["SecondaryText"] = isDarkMode ? themeColors.SecondaryTextDark : themeColors.SecondaryTextLight;
                
                // Update brushes for compatibility
                application.Resources["PrimaryBrush"] = new SolidColorBrush(isDarkMode ? themeColors.PrimaryDark : themeColors.PrimaryLight);
                application.Resources["SecondaryBrush"] = new SolidColorBrush(isDarkMode ? themeColors.SecondaryDark : themeColors.SecondaryLight);
                application.Resources["TertiaryBrush"] = new SolidColorBrush(isDarkMode ? themeColors.TertiaryDark : themeColors.TertiaryLight);
                
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Applied color theme: {colorTheme} for {(isDarkMode ? "dark" : "light")} mode");
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
                    PrimaryLight = Color.FromArgb("#512BD4"),
                    SecondaryLight = Color.FromArgb("#DFD8F7"),
                    TertiaryLight = Color.FromArgb("#2B0B98"),
                    AccentLight = Color.FromArgb("#512BD4"),
                    PrimaryDark = Color.FromArgb("#ac99ea"),
                    SecondaryDark = Color.FromArgb("#B8A7E8"),
                    TertiaryDark = Color.FromArgb("#7c4dff"),
                    AccentDark = Color.FromArgb("#ac99ea"),
                    PrimaryTextLight = Color.FromArgb("#242424"),
                    SecondaryTextLight = Color.FromArgb("#666666"),
                    PrimaryTextDark = Color.FromArgb("#FFFFFF"),
                    SecondaryTextDark = Color.FromArgb("#E0E0E0")
                },
                
                // Nature Green Theme
                [AppColorTheme.Nature] = new ThemeColors
                {
                    Name = "Nature",
                    PrimaryLight = Color.FromArgb("#2E7D32"),
                    SecondaryLight = Color.FromArgb("#C8E6C9"),
                    TertiaryLight = Color.FromArgb("#1B5E20"),
                    AccentLight = Color.FromArgb("#4CAF50"),
                    PrimaryDark = Color.FromArgb("#81C784"),
                    SecondaryDark = Color.FromArgb("#4CAF50"),
                    TertiaryDark = Color.FromArgb("#66BB6A"),
                    AccentDark = Color.FromArgb("#81C784"),
                    PrimaryTextLight = Color.FromArgb("#1B5E20"),
                    SecondaryTextLight = Color.FromArgb("#2E7D32"),
                    PrimaryTextDark = Color.FromArgb("#E8F5E8"),
                    SecondaryTextDark = Color.FromArgb("#C8E6C9")
                },
                
                // Warm Orange/Amber Theme
                [AppColorTheme.Warm] = new ThemeColors
                {
                    Name = "Warm",
                    PrimaryLight = Color.FromArgb("#F57C00"),
                    SecondaryLight = Color.FromArgb("#FFF3E0"),
                    TertiaryLight = Color.FromArgb("#E65100"),
                    AccentLight = Color.FromArgb("#FF9800"),
                    PrimaryDark = Color.FromArgb("#FFCC02"),
                    SecondaryDark = Color.FromArgb("#FFB74D"),
                    TertiaryDark = Color.FromArgb("#FF8F00"),
                    AccentDark = Color.FromArgb("#FFCC02"),
                    PrimaryTextLight = Color.FromArgb("#E65100"),
                    SecondaryTextLight = Color.FromArgb("#F57C00"),
                    PrimaryTextDark = Color.FromArgb("#FFF8E1"),
                    SecondaryTextDark = Color.FromArgb("#FFCC80")
                },
                
                // Vibrant Red/Pink Theme
                [AppColorTheme.Vibrant] = new ThemeColors
                {
                    Name = "Vibrant",
                    PrimaryLight = Color.FromArgb("#D32F2F"),
                    SecondaryLight = Color.FromArgb("#FCE4EC"),
                    TertiaryLight = Color.FromArgb("#B71C1C"),
                    AccentLight = Color.FromArgb("#E91E63"),
                    PrimaryDark = Color.FromArgb("#F48FB1"),
                    SecondaryDark = Color.FromArgb("#EC407A"),
                    TertiaryDark = Color.FromArgb("#AD1457"),
                    AccentDark = Color.FromArgb("#F48FB1"),
                    PrimaryTextLight = Color.FromArgb("#B71C1C"),
                    SecondaryTextLight = Color.FromArgb("#C2185B"),
                    PrimaryTextDark = Color.FromArgb("#FCE4EC"),
                    SecondaryTextDark = Color.FromArgb("#F8BBD9")
                },
                
                // Monochrome Theme
                [AppColorTheme.Monochrome] = new ThemeColors
                {
                    Name = "Monochrome",
                    PrimaryLight = Color.FromArgb("#424242"),
                    SecondaryLight = Color.FromArgb("#F5F5F5"),
                    TertiaryLight = Color.FromArgb("#212121"),
                    AccentLight = Color.FromArgb("#757575"),
                    PrimaryDark = Color.FromArgb("#E0E0E0"),
                    SecondaryDark = Color.FromArgb("#616161"),
                    TertiaryDark = Color.FromArgb("#9E9E9E"),
                    AccentDark = Color.FromArgb("#BDBDBD"),
                    PrimaryTextLight = Color.FromArgb("#212121"),
                    SecondaryTextLight = Color.FromArgb("#616161"),
                    PrimaryTextDark = Color.FromArgb("#FFFFFF"),
                    SecondaryTextDark = Color.FromArgb("#E0E0E0")
                }
            };
        }
    }
}