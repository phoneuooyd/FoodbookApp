using Foodbook.Models;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace FoodbookApp.Tests
{
    public class IngredientListTests
    {
        [Fact]
        public void IngredientCollection_Initialize_ShouldBeEmpty()
        {
            // Arrange & Act
            var ingredients = new ObservableCollection<Ingredient>();

            // Assert
            Assert.NotNull(ingredients);
            Assert.Empty(ingredients);
        }

        [Fact]
        public void IngredientCollection_AddIngredient_ShouldUpdateCollection()
        {
            // Arrange
            var ingredients = new ObservableCollection<Ingredient>();
            var ingredient = new Ingredient { Name = "Pomidor", Quantity = 200, Unit = Unit.Gram };

            // Act
            ingredients.Add(ingredient);

            // Assert
            Assert.Single(ingredients);
            Assert.Contains(ingredient, ingredients);
        }

        [Fact]
        public void IngredientCollection_RemoveIngredient_ShouldUpdateCollection()
        {
            // Arrange
            var ingredients = new ObservableCollection<Ingredient>();
            var ingredient1 = new Ingredient { Name = "Pomidor", Quantity = 200, Unit = Unit.Gram };
            var ingredient2 = new Ingredient { Name = "Cebula", Quantity = 100, Unit = Unit.Gram };
            ingredients.Add(ingredient1);
            ingredients.Add(ingredient2);

            // Act
            ingredients.Remove(ingredient1);

            // Assert
            Assert.Single(ingredients);
            Assert.DoesNotContain(ingredient1, ingredients);
            Assert.Contains(ingredient2, ingredients);
        }

        [Fact]
        public void IngredientCollection_FilterByName_ShouldReturnMatchingItems()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor", Quantity = 200, Unit = Unit.Gram },
                new Ingredient { Name = "Pomidor koktajlowy", Quantity = 150, Unit = Unit.Gram },
                new Ingredient { Name = "Cebula", Quantity = 100, Unit = Unit.Gram },
                new Ingredient { Name = "Czosnek", Quantity = 50, Unit = Unit.Gram }
            };

            // Act
            var filteredIngredients = ingredients.Where(i => i.Name.Contains("Pomidor")).ToList();

            // Assert
            Assert.Equal(2, filteredIngredients.Count);
            Assert.All(filteredIngredients, ingredient => Assert.Contains("Pomidor", ingredient.Name));
        }

        [Fact]
        public void IngredientCollection_FilterByUnit_ShouldReturnMatchingItems()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor", Quantity = 200, Unit = Unit.Gram },
                new Ingredient { Name = "Mleko", Quantity = 250, Unit = Unit.Milliliter },
                new Ingredient { Name = "Jajka", Quantity = 3, Unit = Unit.Piece },
                new Ingredient { Name = "Mąka", Quantity = 500, Unit = Unit.Gram }
            };

            // Act
            var gramIngredients = ingredients.Where(i => i.Unit == Unit.Gram).ToList();

            // Assert
            Assert.Equal(2, gramIngredients.Count);
            Assert.All(gramIngredients, ingredient => Assert.Equal(Unit.Gram, ingredient.Unit));
        }

        [Fact]
        public void IngredientCollection_SortByName_ShouldReturnOrderedList()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Ziemniak" },
                new Ingredient { Name = "Cebula" },
                new Ingredient { Name = "Pomidor" },
                new Ingredient { Name = "Brokuł" }
            };

            // Act
            var sortedIngredients = ingredients.OrderBy(i => i.Name).ToList();

            // Assert
            Assert.Equal("Brokuł", sortedIngredients[0].Name);
            Assert.Equal("Cebula", sortedIngredients[1].Name);
            Assert.Equal("Pomidor", sortedIngredients[2].Name);
            Assert.Equal("Ziemniak", sortedIngredients[3].Name);
        }

        [Fact]
        public void IngredientCollection_GroupByUnit_ShouldReturnCorrectGroups()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor", Unit = Unit.Gram },
                new Ingredient { Name = "Cebula", Unit = Unit.Gram },
                new Ingredient { Name = "Mleko", Unit = Unit.Milliliter },
                new Ingredient { Name = "Olej", Unit = Unit.Milliliter },
                new Ingredient { Name = "Jajka", Unit = Unit.Piece }
            };

            // Act
            var groupedIngredients = ingredients.GroupBy(i => i.Unit).ToList();

            // Assert
            Assert.Equal(3, groupedIngredients.Count());
            Assert.Contains(groupedIngredients, g => g.Key == Unit.Gram && g.Count() == 2);
            Assert.Contains(groupedIngredients, g => g.Key == Unit.Milliliter && g.Count() == 2);
            Assert.Contains(groupedIngredients, g => g.Key == Unit.Piece && g.Count() == 1);
        }

        [Fact]
        public void IngredientCollection_CalculateTotalCalories_ShouldReturnCorrectSum()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor", Calories = 18 },
                new Ingredient { Name = "Cebula", Calories = 40 },
                new Ingredient { Name = "Mąka", Calories = 364 }
            };

            // Act
            var totalCalories = ingredients.Sum(i => i.Calories);

            // Assert
            Assert.Equal(422, totalCalories);
        }

        [Fact]
        public void IngredientCollection_FindIngredientById_ShouldReturnCorrectItem()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Id = 1, Name = "Pomidor" },
                new Ingredient { Id = 2, Name = "Cebula" },
                new Ingredient { Id = 3, Name = "Czosnek" }
            };

            // Act
            var foundIngredient = ingredients.FirstOrDefault(i => i.Id == 2);

            // Assert
            Assert.NotNull(foundIngredient);
            Assert.Equal("Cebula", foundIngredient.Name);
            Assert.Equal(2, foundIngredient.Id);
        }

        [Fact]
        public void IngredientCollection_CheckAllIngredients_ShouldUpdateIsCheckedProperty()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor", IsChecked = false },
                new Ingredient { Name = "Cebula", IsChecked = false },
                new Ingredient { Name = "Czosnek", IsChecked = false }
            };

            // Act
            foreach (var ingredient in ingredients)
            {
                ingredient.IsChecked = true;
            }

            // Assert
            Assert.All(ingredients, ingredient => Assert.True(ingredient.IsChecked));
        }

        [Fact]
        public void IngredientCollection_GetCheckedIngredients_ShouldReturnOnlyCheckedItems()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor", IsChecked = true },
                new Ingredient { Name = "Cebula", IsChecked = false },
                new Ingredient { Name = "Czosnek", IsChecked = true },
                new Ingredient { Name = "Pietruszka", IsChecked = false }
            };

            // Act
            var checkedIngredients = ingredients.Where(i => i.IsChecked).ToList();

            // Assert
            Assert.Equal(2, checkedIngredients.Count);
            Assert.Contains(checkedIngredients, i => i.Name == "Pomidor");
            Assert.Contains(checkedIngredients, i => i.Name == "Czosnek");
        }

        [Theory]
        [InlineData("pomidor", "Pomidor")]
        [InlineData("CEBULA", "Cebula")]
        [InlineData("czos", "Czosnek")]
        public void IngredientCollection_CaseInsensitiveSearch_ShouldFindIngredients(string searchTerm, string expectedName)
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Name = "Pomidor" },
                new Ingredient { Name = "Cebula" },
                new Ingredient { Name = "Czosnek" }
            };

            // Act
            var foundIngredients = ingredients.Where(i => 
                i.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

            // Assert
            Assert.Single(foundIngredients);
            Assert.Equal(expectedName, foundIngredients.First().Name);
        }

        [Fact]
        public void IngredientCollection_ClearCollection_ShouldBeEmpty()
        {
            // Arrange
            var ingredients = new ObservableCollection<Ingredient>
            {
                new Ingredient { Name = "Pomidor" },
                new Ingredient { Name = "Cebula" }
            };

            // Act
            ingredients.Clear();

            // Assert
            Assert.Empty(ingredients);
        }
    }
}
