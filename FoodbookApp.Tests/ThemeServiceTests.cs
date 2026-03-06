using System;
using System.Collections.Generic;
using System.Linq;
using Foodbook.Services;
using Foodbook.Models;
using Microsoft.Maui.Controls;
using Xunit;
using AppThemeEnum = Foodbook.Models.AppTheme;

namespace FoodbookApp.Tests
{
    public class ThemeServiceTests
    {
        [Fact]
        public void Constructor_ShouldInitializeAvailableThemes()
        {
            var svc = new ThemeService();
            var map = svc.GetAvailableColorThemes();
            Assert.NotNull(map);
            Assert.True(map.Count > 0);
            Assert.Contains(AppColorTheme.Default, map.Keys);
        }

        [Fact]
        public void DefaultState_ShouldHaveSystemThemeAndDefaultColorTheme()
        {
            var svc = new ThemeService();
            Assert.Equal(AppThemeEnum.System, svc.GetCurrentTheme());
            Assert.Equal(AppColorTheme.Default, svc.GetCurrentColorTheme());
            Assert.False(svc.GetIsColorfulBackgroundEnabled());
            Assert.False(svc.IsWallpaperBackgroundEnabled());
        }

        [Fact]
        public void SetColorfulBackground_ShouldDisableWallpaper()
        {
            var svc = new ThemeService();
            svc.EnableWallpaperBackground(true);
            svc.SetColorfulBackground(true);
            Assert.True(svc.GetIsColorfulBackgroundEnabled());
            Assert.False(svc.IsWallpaperBackgroundEnabled());
        }

        [Fact]
        public void EnableWallpaperBackground_ShouldDisableColorfulBackground()
        {
            var svc = new ThemeService();
            svc.SetColorfulBackground(true);
            svc.EnableWallpaperBackground(true);
            Assert.True(svc.IsWallpaperBackgroundEnabled());
            Assert.False(svc.GetIsColorfulBackgroundEnabled());
        }

        [Fact]
        public void IsWallpaperAvailableFor_ShouldBeFalseForUnknownTheme()
        {
            var svc = new ThemeService();
            // Theme enum contains only defined names; use a value outside the map by casting
            var available = svc.IsWallpaperAvailableFor((AppColorTheme)9999);
            Assert.False(available);
        }
    }
}
