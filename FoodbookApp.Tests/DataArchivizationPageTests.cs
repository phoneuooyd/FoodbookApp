using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Moq;
using Xunit;

namespace FoodbookApp.Tests
{
    public class DataArchivizationPageTests
    {
        private readonly Mock<IDatabaseService> _dbMock = new();
        private readonly Mock<IPreferencesService> _prefsMock = new();

        [Fact]
        public void Constructor_ShouldInitializeDependencies()
        {
            var page = new DataArchivizationPage(_dbMock.Object, _prefsMock.Object);
            Assert.NotNull(page);
        }

        [Fact]
        public void GetDefaultArchiveFolder_ShouldReturnNonEmptyPath()
        {
            var page = new DataArchivizationPage(_dbMock.Object, _prefsMock.Object);
            var method = typeof(DataArchivizationPage).GetMethod("GetDefaultArchiveFolder", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            var path = (string)method!.Invoke(page, null)!;
            Assert.False(string.IsNullOrWhiteSpace(path));
        }

        [Fact]
        public void IsArchivePath_ShouldValidateExtensions()
        {
            var method = typeof(DataArchivizationPage).GetMethod("IsArchivePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            Assert.True((bool)method!.Invoke(null, new object?[] { "backup.fbk" })!);
            Assert.True((bool)method.Invoke(null, new object?[] { "backup.ZIP" })!);
            Assert.False((bool)method.Invoke(null, new object?[] { "image.png" })!);
        }

        [Fact]
        public void CreateItem_ShouldReturnArchiveItemWithMetadata()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "test");
                var method = typeof(DataArchivizationPage).GetMethod("CreateItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.NotNull(method);
                var result = method!.Invoke(null, new object?[] { tmp });
                Assert.NotNull(result);
                var type = result!.GetType();
                var fileName = (string)type.GetProperty("FileName")!.GetValue(result)!;
                var fullPath = (string)type.GetProperty("FullPath")!.GetValue(result)!;
                Assert.Equal(Path.GetFileName(tmp), fileName);
                Assert.Equal(tmp, fullPath);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        [Fact]
        public void SanitizeFileName_ShouldReplaceInvalidChars()
        {
            var page = new DataArchivizationPage(_dbMock.Object, _prefsMock.Object);
            var method = typeof(DataArchivizationPage).GetMethod("SanitizeFileName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            var sanitized = (string)method!.Invoke(null, new object?[] { "inva*lid:na|me?.fbk" })!;
            Assert.DoesNotContain('*', sanitized);
            Assert.DoesNotContain(':', sanitized);
            Assert.DoesNotContain('|', sanitized);
            Assert.EndsWith(".fbk", sanitized);
        }
    }
}
