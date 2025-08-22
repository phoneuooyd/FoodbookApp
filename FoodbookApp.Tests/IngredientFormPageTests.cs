using FluentAssertions;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.ViewModels;
using Foodbook.Views;
using Microsoft.Maui.Controls;
using Moq;
using System.Globalization;

namespace FoodbookApp.Tests
{
    public class IngredientFormPageTests
    {
        private readonly Mock<IIngredientService> _mockIngredientService;
        private readonly IngredientFormViewModel _viewModel;

        public IngredientFormPageTests()
        {
            _mockIngredientService = new Mock<IIngredientService>();
            _viewModel = new IngredientFormViewModel(_mockIngredientService.Object);
        }

        [Fact]
        public void IngredientFormViewModel_WhenCreated_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var viewModel = new IngredientFormViewModel(_mockIngredientService.Object);

            // Assert
            viewModel.Name.Should().BeEmpty();
            viewModel.Quantity.Should().Be("100");
            viewModel.SelectedUnit.Should().Be(Unit.Gram);
            viewModel.Calories.Should().Be("0");
            viewModel.Protein.Should().Be("0");
            viewModel.Fat.Should().Be("0");
            viewModel.Carbs.Should().Be("0");
            viewModel.Title.Should().Be("Nowy składnik");
            viewModel.SaveButtonText.Should().Be("Dodaj składnik");
        }

        [Fact]
        public void IngredientFormViewModel_WhenNameIsEmpty_ShouldHaveValidationError()
        {
            // Arrange & Act
            _viewModel.Name = "";

            // Assert
            _viewModel.HasValidationError.Should().BeTrue();
            _viewModel.ValidationMessage.Should().Be("Nazwa składnika jest wymagana");
        }

        [Fact]
        public void IngredientFormViewModel_WhenQuantityIsInvalid_ShouldHaveValidationError()
        {
            // Arrange
            _viewModel.Name = "Test Ingredient";

            // Act
            _viewModel.Quantity = "invalid";

            // Assert
            _viewModel.HasValidationError.Should().BeTrue();
            _viewModel.ValidationMessage.Should().Be("Ilość musi być liczbą");
        }

        [Fact]
        public void IngredientFormViewModel_WhenQuantityIsZero_ShouldHaveValidationError()
        {
            // Arrange
            _viewModel.Name = "Test Ingredient";

            // Act
            _viewModel.Quantity = "0";

            // Assert
            _viewModel.HasValidationError.Should().BeTrue();
            _viewModel.ValidationMessage.Should().Be("Ilość musi być większa od zera");
        }

        [Fact]
        public async Task IngredientFormViewModel_LoadAsync_ShouldLoadExistingIngredient()
        {
            // Arrange
            var ingredient = new Ingredient
            {
                Id = 1,
                Name = "Test Ingredient",
                Quantity = 150,
                Unit = Unit.Milliliter,
                Calories = 100,
                Protein = 5,
                Fat = 2,
                Carbs = 15
            };

            _mockIngredientService.Setup(s => s.GetIngredientAsync(1))
                .ReturnsAsync(ingredient);

            // Act
            await _viewModel.LoadAsync(1);

            // Assert
            _viewModel.Name.Should().Be("Test Ingredient");
            // W polskiej lokalizacji liczby używają przecinka jako separatora dziesiętnego
            _viewModel.Quantity.Should().BeOneOf("150.00", "150,00");
            _viewModel.SelectedUnit.Should().Be(Unit.Milliliter);
            _viewModel.Calories.Should().BeOneOf("100.0", "100,0");
            _viewModel.Title.Should().Be("Edytuj składnik");
            _viewModel.SaveButtonText.Should().Be("Zapisz zmiany");
        }
    }
}
