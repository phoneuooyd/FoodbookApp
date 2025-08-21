using Foodbook.Models;
using System.ComponentModel;
using Xunit;

namespace FoodbookApp.Tests
{
    public class RecipeTests
    {
        [Fact]
        public void Recipe_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var recipe = new Recipe();

            // Assert
            Assert.Equal(0, recipe.Id);
            Assert.Equal(string.Empty, recipe.Name);
            Assert.Null(recipe.Description);
            Assert.Equal(0, recipe.Calories);
            Assert.Equal(0, recipe.Protein);
            Assert.Equal(0, recipe.Fat);
            Assert.Equal(0, recipe.Carbs);
            Assert.Equal(2, recipe.IloscPorcji); // Default value is 2
            Assert.NotNull(recipe.Ingredients);
            Assert.Empty(recipe.Ingredients);
        }

        [Fact]
        public void Recipe_SetBasicProperties_ShouldUpdateCorrectly()
        {
            // Arrange
            var recipe = new Recipe();

            // Act
            recipe.Id = 1;
            recipe.Name = "Pierogi z mięsem";
            recipe.Description = "Tradycyjne polskie pierogi z farszem mięsnym";
            recipe.Calories = 280;
            recipe.Protein = 12.5;
            recipe.Fat = 8.3;
            recipe.Carbs = 35.2;
            recipe.IloscPorcji = 4;

            // Assert
            Assert.Equal(1, recipe.Id);
            Assert.Equal("Pierogi z mięsem", recipe.Name);
            Assert.Equal("Tradycyjne polskie pierogi z farszem mięsnym", recipe.Description);
            Assert.Equal(280, recipe.Calories);
            Assert.Equal(12.5, recipe.Protein);
            Assert.Equal(8.3, recipe.Fat);
            Assert.Equal(35.2, recipe.Carbs);
            Assert.Equal(4, recipe.IloscPorcji);
        }

        [Fact]
        public void Recipe_SetDescription_ToNull_ShouldAcceptNullValue()
        {
            // Arrange
            var recipe = new Recipe();

            // Act
            recipe.Description = null;

            // Assert
            Assert.Null(recipe.Description);
        }

        [Fact]
        public void Recipe_AddIngredients_ShouldUpdateIngredientsCollection()
        {
            // Arrange
            var recipe = new Recipe();
            var ingredient1 = new Ingredient { Name = "Mąka", Quantity = 500, Unit = Unit.Gram };
            var ingredient2 = new Ingredient { Name = "Mięso mielone", Quantity = 300, Unit = Unit.Gram };

            // Act
            recipe.Ingredients.Add(ingredient1);
            recipe.Ingredients.Add(ingredient2);

            // Assert
            Assert.Equal(2, recipe.Ingredients.Count);
            Assert.Contains(ingredient1, recipe.Ingredients);
            Assert.Contains(ingredient2, recipe.Ingredients);
        }

        [Fact]
        public void Recipe_RemoveIngredients_ShouldUpdateIngredientsCollection()
        {
            // Arrange
            var recipe = new Recipe();
            var ingredient1 = new Ingredient { Name = "Mąka", Quantity = 500, Unit = Unit.Gram };
            var ingredient2 = new Ingredient { Name = "Mięso mielone", Quantity = 300, Unit = Unit.Gram };
            recipe.Ingredients.Add(ingredient1);
            recipe.Ingredients.Add(ingredient2);

            // Act
            recipe.Ingredients.Remove(ingredient1);

            // Assert
            Assert.Single(recipe.Ingredients);
            Assert.DoesNotContain(ingredient1, recipe.Ingredients);
            Assert.Contains(ingredient2, recipe.Ingredients);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(6)]
        public void Recipe_IloscPorcji_WhenSetToValidValue_ShouldUpdateCorrectly(int portions)
        {
            // Arrange
            var recipe = new Recipe();

            // Act
            recipe.IloscPorcji = portions;

            // Assert
            Assert.Equal(portions, recipe.IloscPorcji);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(150.5)]
        [InlineData(500.0)]
        public void Recipe_NutritionalValues_WhenSetToValidValues_ShouldUpdateCorrectly(double value)
        {
            // Arrange
            var recipe = new Recipe();

            // Act
            recipe.Calories = value;
            recipe.Protein = value;
            recipe.Fat = value;
            recipe.Carbs = value;

            // Assert
            Assert.Equal(value, recipe.Calories);
            Assert.Equal(value, recipe.Protein);
            Assert.Equal(value, recipe.Fat);
            Assert.Equal(value, recipe.Carbs);
        }

        [Fact]
        public void Recipe_IngredientWithRecipeRelation_ShouldMaintainBidirectionalRelationship()
        {
            // Arrange
            var recipe = new Recipe { Id = 1, Name = "Test Recipe" };
            var ingredient = new Ingredient 
            { 
                Name = "Test Ingredient", 
                RecipeId = recipe.Id,
                Recipe = recipe
            };

            // Act
            recipe.Ingredients.Add(ingredient);

            // Assert
            Assert.Equal(recipe.Id, ingredient.RecipeId);
            Assert.Same(recipe, ingredient.Recipe);
            Assert.Contains(ingredient, recipe.Ingredients);
        }

        [Fact]
        public void Recipe_ClearIngredients_ShouldEmptyCollection()
        {
            // Arrange
            var recipe = new Recipe();
            recipe.Ingredients.Add(new Ingredient { Name = "Ingredient 1" });
            recipe.Ingredients.Add(new Ingredient { Name = "Ingredient 2" });

            // Act
            recipe.Ingredients.Clear();

            // Assert
            Assert.Empty(recipe.Ingredients);
        }

        [Fact]
        public void Recipe_WithMultipleIngredients_ShouldMaintainCorrectCount()
        {
            // Arrange
            var recipe = new Recipe();
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Mąka", Quantity = 500, Unit = Unit.Gram },
                new Ingredient { Name = "Jajka", Quantity = 3, Unit = Unit.Piece },
                new Ingredient { Name = "Mleko", Quantity = 250, Unit = Unit.Milliliter },
                new Ingredient { Name = "Sól", Quantity = 5, Unit = Unit.Gram }
            };

            // Act
            foreach (var ingredient in ingredients)
            {
                recipe.Ingredients.Add(ingredient);
            }

            // Assert
            Assert.Equal(4, recipe.Ingredients.Count);
            Assert.All(ingredients, ingredient => Assert.Contains(ingredient, recipe.Ingredients));
        }
    }
}
