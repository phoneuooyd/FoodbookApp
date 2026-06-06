using FluentAssertions;
using Foodbook.Models;
using Foodbook.ViewModels;
using Foodbook.Views;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Moq;
using System.Globalization;

namespace FoodbookApp.Tests
{
    public class IngredientFormPageTests
    {
        private readonly Mock<IIngredientService> _mockIngredientService;
        private readonly Mock<IOpenFoodFactsService> _mockOpenFoodFactsService;
        private readonly IngredientFormViewModel _viewModel;
        private readonly CultureInfo _originalCulture;

        public IngredientFormPageTests()
        {
            _originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            _mockIngredientService = new Mock<IIngredientService>();
            _mockOpenFoodFactsService = new Mock<IOpenFoodFactsService>();
            _viewModel = new IngredientFormViewModel(
                _mockIngredientService.Object,
                _mockOpenFoodFactsService.Object);
        }

        [Fact]
        public void Constructor_ShouldInitializeProperties()
        {
            _viewModel.Name.Should().BeEmpty();
            _viewModel.Quantity.Should().Be("100");
            _viewModel.SelectedUnit.Should().Be(Unit.Gram);
            _viewModel.Calories.Should().Be("0");
            _viewModel.Protein.Should().Be("0");
            _viewModel.Fat.Should().Be("0");
            _viewModel.Carbs.Should().Be("0");
            _viewModel.SaveCommand.Should().NotBeNull();
            _viewModel.OpenScannerCommand.Should().NotBeNull();
        }

        [Fact]
        public void ApplyProductData_ShouldPopulateFields()
        {
            var product = new OpenFoodFactsProductResult
            {
                Barcode = "5901234123457",
                Name = "Test Product",
                Calories100g = 250,
                Protein100g = 12.5,
                Fat100g = 8.3,
                Carbs100g = 30.1
            };

            InvokeApplyProductData(product);

            _viewModel.Name.Should().Be("Test Product");
            _viewModel.Quantity.Should().Be("100");
            _viewModel.SelectedUnit.Should().Be(Unit.Gram);
            _viewModel.Calories.Should().Be("250.0");
            _viewModel.Protein.Should().Be("12.5");
            _viewModel.Fat.Should().Be("8.3");
            _viewModel.Carbs.Should().Be("30.1");
        }

        [Fact]
        public void ApplyProductData_WithEmptyName_ShouldNotOverwriteName()
        {
            _viewModel.Name = "Existing Ingredient";
            var product = new OpenFoodFactsProductResult
            {
                Barcode = "5901234123457",
                Name = "",
                Calories100g = 100,
                Protein100g = 5,
                Fat100g = 2,
                Carbs100g = 10
            };

            InvokeApplyProductData(product);

            _viewModel.Name.Should().Be("Existing Ingredient");
            _viewModel.Calories.Should().Be("100.0");
        }

        [Fact]
        public void ApplyProductData_WithNegativeNutrition_ShouldNotOverwrite()
        {
            _viewModel.Name = "Test";
            var product = new OpenFoodFactsProductResult
            {
                Barcode = "5901234123457",
                Name = "New Name",
                Calories100g = -1,
                Protein100g = -1,
                Fat100g = -1,
                Carbs100g = -1
            };

            InvokeApplyProductData(product);

            _viewModel.Calories.Should().Be("0");
            _viewModel.Protein.Should().Be("0");
        }

        private void InvokeApplyProductData(OpenFoodFactsProductResult product)
        {
            var method = _viewModel.GetType()
                .GetMethod("ApplyProductData",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method!.Invoke(_viewModel, new object[] { product });
        }
    }
}
