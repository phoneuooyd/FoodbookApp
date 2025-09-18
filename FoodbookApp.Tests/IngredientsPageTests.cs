using FluentAssertions;
using Foodbook.Models;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
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
                new() { Id = 1, Name = "Test Ingredient 1", Quantity = 100, Unit = Unit.Gram },
                new() { Id = 2, Name = "Test Ingredient 2", Quantity = 200, Unit = Unit.Milliliter }
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
            var ingredient = new Ingredient { Id = 1, Name = "Test Ingredient", Quantity = 100, Unit = Unit.Gram };
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
    }
}
