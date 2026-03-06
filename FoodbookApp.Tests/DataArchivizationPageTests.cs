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
    }
}
