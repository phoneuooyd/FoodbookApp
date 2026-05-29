using FluentAssertions;
using Foodbook.Models;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
using Foodbook.Views.Components;
using Microsoft.Maui.Controls;
using Moq;

namespace FoodbookApp.Tests
{
    public class IngredientsPageTests
    {
        private readonly Mock<IIngredientService> _mockIngredientService;
        private readonly IngredientsViewModel _viewModel;

        public IngredientsPageTests()
        {
            _mockIngredientService = new Mock<IIngredientService>();
            _viewModel = new IngredientsViewModel(_mockIngredientService.Object);
        }

        [Fact]
        public void IngredientsViewModel_WhenCreated_ShouldInitializeWithEmptyCollection()
        {
            // Arrange & Act
            var viewModel = new IngredientsViewModel(_mockIngredientService.Object);

            // Assert
            viewModel.Ingredients.Should().BeEmpty();
            viewModel.IsLoading.Should().BeFalse();
            viewModel.IsRefreshing.Should().BeFalse();
            viewModel.SearchText.Should().BeEmpty();
        }

        [Fact]
        public void IngredientsViewModel_WhenSearchTextSet_ShouldUpdateSearchText()
        {
            // Arrange
            var searchText = "test ingredient";

            // Act
            _viewModel.SearchText = searchText;

            // Assert
            _viewModel.SearchText.Should().Be(searchText);
        }

        [Fact]
        public async Task IngredientsViewModel_LoadAsync_ShouldLoadIngredientsFromService()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new() { Id = Guid.NewGuid(), Name = "Test Ingredient 1", Quantity = 100, Unit = Unit.Gram },
                new() { Id = Guid.NewGuid(), Name = "Test Ingredient 2", Quantity = 200, Unit = Unit.Milliliter }
            };

            _mockIngredientService.Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(ingredients);

            // Act
            await _viewModel.LoadAsync();

            // Assert
            _viewModel.Ingredients.Should().HaveCount(2);
            _viewModel.Ingredients.First().Name.Should().Be("Test Ingredient 1");
            _viewModel.IsLoading.Should().BeFalse();
        }

        [Fact]
        public void IngredientsViewModel_DeleteCommand_ShouldBeExecutable()
        {
            // Arrange
            var ingredient = new Ingredient { Id = Guid.NewGuid(), Name = "Test Ingredient", Quantity = 100, Unit = Unit.Gram };
            _viewModel.Ingredients.Add(ingredient);

            _mockIngredientService.Setup(s => s.DeleteIngredientAsync(ingredient.Id))
                .Returns(Task.CompletedTask);

            // Act
            var canExecute = ((Command<Ingredient>)_viewModel.DeleteCommand).CanExecute(ingredient);

            // Assert
            canExecute.Should().BeTrue();
        }

        [Fact]
        public void IngredientsViewModel_BulkVerifyCommand_WhenNoIngredients_ShouldNotExecute()
        {
            // Arrange
            _viewModel.Ingredients.Clear();

            // Act
            var canExecute = ((Command)_viewModel.BulkVerifyCommand).CanExecute(null);

            // Assert
            canExecute.Should().BeFalse();
        }

        [Fact]
        public async Task IngredientsViewModel_ApplySorting_WhenCaloriesDesc_ShouldSortByCaloriesDescending()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new() { Id = Guid.NewGuid(), Name = "Low", Calories = 40, Protein = 2, Fat = 1, Carbs = 8 },
                new() { Id = Guid.NewGuid(), Name = "High", Calories = 220, Protein = 10, Fat = 12, Carbs = 5 },
                new() { Id = Guid.NewGuid(), Name = "Mid", Calories = 120, Protein = 6, Fat = 4, Carbs = 14 }
            };

            _mockIngredientService.Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(ingredients);

            await _viewModel.LoadAsync();

            // Act
            _viewModel.ApplySorting(SortBy.CaloriesDesc);

            // Assert
            _viewModel.CurrentSortBy.Should().Be(SortBy.CaloriesDesc);
            _viewModel.SortOrder.Should().Be(SortOrder.Desc);
            _viewModel.Ingredients.Select(i => i.Name).Should().ContainInOrder("High", "Mid", "Low");
        }

        [Fact]
        public async Task IngredientsViewModel_ApplySorting_WhenProteinAsc_ShouldSortByProteinAscending()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new() { Id = Guid.NewGuid(), Name = "Steak", Calories = 200, Protein = 26, Fat = 11, Carbs = 0 },
                new() { Id = Guid.NewGuid(), Name = "Rice", Calories = 130, Protein = 2.7, Fat = 0.3, Carbs = 28 },
                new() { Id = Guid.NewGuid(), Name = "Egg", Calories = 155, Protein = 13, Fat = 11, Carbs = 1.1 }
            };

            _mockIngredientService.Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(ingredients);

            await _viewModel.LoadAsync();

            // Act
            _viewModel.ApplySorting(SortBy.ProteinAsc);

            // Assert
            _viewModel.CurrentSortBy.Should().Be(SortBy.ProteinAsc);
            _viewModel.SortOrder.Should().Be(SortOrder.Asc);
            _viewModel.Ingredients.Select(i => i.Name).Should().ContainInOrder("Rice", "Egg", "Steak");
        }

        [Fact]
        public async Task IngredientsViewModel_WhenSortOrderSetToDesc_ShouldMapToNameDescSorting()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new() { Id = Guid.NewGuid(), Name = "Banana" },
                new() { Id = Guid.NewGuid(), Name = "Apple" },
                new() { Id = Guid.NewGuid(), Name = "Carrot" }
            };

            _mockIngredientService.Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(ingredients);

            await _viewModel.LoadAsync();

            // Act
            _viewModel.SortOrder = SortOrder.Desc;

            // Assert
            _viewModel.CurrentSortBy.Should().Be(SortBy.NameDesc);
            _viewModel.Ingredients.Select(i => i.Name).Should().ContainInOrder("Carrot", "Banana", "Apple");
        }
    }
}
