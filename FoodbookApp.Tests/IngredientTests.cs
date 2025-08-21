using Foodbook.Models;
using System.ComponentModel;

namespace FoodbookApp.Tests
{
    public class IngredientTests
    {
        [Fact]
        public void Ingredient_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var ingredient = new Ingredient();

            // Assert
            Assert.Equal(0, ingredient.Id);
            Assert.Equal(string.Empty, ingredient.Name);
            Assert.Equal(0, ingredient.Quantity);
            Assert.Equal(Unit.Gram, ingredient.Unit);
            Assert.False(ingredient.IsChecked);
            Assert.Equal(0, ingredient.Calories);
            Assert.Equal(0, ingredient.Protein);
            Assert.Equal(0, ingredient.Fat);
            Assert.Equal(0, ingredient.Carbs);
            Assert.Null(ingredient.RecipeId);
            Assert.Null(ingredient.Recipe);
        }

        [Fact]
        public void Ingredient_SetBasicProperties_ShouldUpdateCorrectly()
        {
            // Arrange
            var ingredient = new Ingredient();

            // Act
            ingredient.Id = 1;
            ingredient.Name = "Pomidor";
            ingredient.Quantity = 150;
            ingredient.Unit = Unit.Gram;
            ingredient.Calories = 18;
            ingredient.Protein = 0.9;
            ingredient.Fat = 0.2;
            ingredient.Carbs = 3.9;

            // Assert
            Assert.Equal(1, ingredient.Id);
            Assert.Equal("Pomidor", ingredient.Name);
            Assert.Equal(150, ingredient.Quantity);
            Assert.Equal(Unit.Gram, ingredient.Unit);
            Assert.Equal(18, ingredient.Calories);
            Assert.Equal(0.9, ingredient.Protein);
            Assert.Equal(0.2, ingredient.Fat);
            Assert.Equal(3.9, ingredient.Carbs);
        }

        [Fact]
        public void IsChecked_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var ingredient = new Ingredient();
            var eventRaised = false;
            string? propertyName = null;

            ingredient.PropertyChanged += (sender, e) =>
            {
                eventRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            ingredient.IsChecked = true;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(Ingredient.IsChecked), propertyName);
            Assert.True(ingredient.IsChecked);
        }

        [Theory]
        [InlineData(Unit.Gram)]
        [InlineData(Unit.Milliliter)]
        [InlineData(Unit.Piece)]
        public void Ingredient_Unit_WhenSetToValidValue_ShouldUpdateCorrectly(Unit unit)
        {
            // Arrange
            var ingredient = new Ingredient();

            // Act
            ingredient.Unit = unit;

            // Assert
            Assert.Equal(unit, ingredient.Unit);
        }

        [Fact]
        public void Ingredient_ImplementsINotifyPropertyChanged()
        {
            // Arrange
            var ingredient = new Ingredient();

            // Act & Assert
            Assert.IsAssignableFrom<INotifyPropertyChanged>(ingredient);
        }
    }
}
