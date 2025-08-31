using Foodbook.Models;
using Application = Microsoft.Maui.Controls.Application;

namespace Foodbook.Services
{
    public class ThemeService : IThemeService
    {
        private Foodbook.Models.AppTheme _currentTheme = Foodbook.Models.AppTheme.System;

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
                
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Applied theme: {theme}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThemeService] Failed to set theme: {ex.Message}");
            }
        }
    }
}