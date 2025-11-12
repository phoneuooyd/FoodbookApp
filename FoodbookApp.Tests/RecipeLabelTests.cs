using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Foodbook.Models;
using Xunit;

namespace FoodbookApp.Tests
{
    public class RecipeLabelTests
    {
        [Fact]
        public void RecipeLabel_DefaultValues_ShouldBeCorrect()
        {
            var label = new RecipeLabel();

            Assert.Equal(0, label.Id);
            Assert.Equal(string.Empty, label.Name);
            Assert.Null(label.ColorHex);
            Assert.False(label.IsSelected);
            Assert.True((DateTime.UtcNow - label.CreatedAt).TotalSeconds < 5);
        }

        [Fact]
        public void RecipeLabel_SetProperties_ShouldUpdateCorrectly()
        {
            var label = new RecipeLabel
            {
                Id = 5,
                Name = "Obiad",
                ColorHex = "#FFAA00",
                CreatedAt = new DateTime(2024, 12, 24, 12, 0, 0, DateTimeKind.Utc),
                IsSelected = true
            };

            Assert.Equal(5, label.Id);
            Assert.Equal("Obiad", label.Name);
            Assert.Equal("#FFAA00", label.ColorHex);
            Assert.Equal(new DateTime(2024, 12, 24, 12, 0, 0, DateTimeKind.Utc), label.CreatedAt);
            Assert.True(label.IsSelected);
        }

        [Fact]
        public void RecipeLabel_Name_ShouldHaveRequiredAndMaxLengthAttributes()
        {
            var prop = typeof(RecipeLabel).GetProperty(nameof(RecipeLabel.Name));
            Assert.NotNull(prop);

            var required = prop!.GetCustomAttributes(typeof(RequiredAttribute), inherit: false).FirstOrDefault();
            var maxLength = prop.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false).FirstOrDefault() as MaxLengthAttribute;

            Assert.NotNull(required);
            Assert.NotNull(maxLength);
            Assert.Equal(100, maxLength!.Length);
        }

        [Fact]
        public void RecipeLabel_ColorHex_ShouldHaveMaxLength9()
        {
            var prop = typeof(RecipeLabel).GetProperty(nameof(RecipeLabel.ColorHex));
            Assert.NotNull(prop);

            var maxLength = prop!.GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false).FirstOrDefault() as MaxLengthAttribute;
            Assert.NotNull(maxLength);
            Assert.Equal(9, maxLength!.Length);
        }

        [Fact]
        public void RecipeLabel_IsSelected_Toggle_ShouldWork()
        {
            var label = new RecipeLabel();
            Assert.False(label.IsSelected);
            label.IsSelected = true;
            Assert.True(label.IsSelected);
            label.IsSelected = false;
            Assert.False(label.IsSelected);
        }
    }
}
