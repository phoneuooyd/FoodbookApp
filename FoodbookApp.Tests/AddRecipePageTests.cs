using FluentAssertions;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.ViewModels;
using Microsoft.Maui.Controls;
using Moq;
using System.Globalization;

namespace FoodbookApp.Tests
{
    public class AddRecipePageTests
    {
        private readonly Mock<IRecipeService> _mockRecipeService;
        private readonly Mock<IIngredientService> _mockIngredientService;
        private readonly Mock<IFolderService> _mockFolderService;
        private readonly RecipeImporter _recipeImporter;
        private readonly AddRecipeViewModel _viewModel;

        public AddRecipePageTests()
        {
            _mockRecipeService = new Mock<IRecipeService>();
            _mockIngredientService = new Mock<IIngredientService>();
            _mockFolderService = new Mock<IFolderService>();
            
            // Setup basic empty list for ingredient service
            _mockIngredientService.Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(new List<Ingredient>());
            
            // Setup folder service to avoid nulls/background loads
            _mockFolderService.Setup(s => s.GetFolderHierarchyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());
            
            // Tworzenie rzeczywistego RecipeImporter z mock HttpClient
            var mockHttpClient = new HttpClient();
            _recipeImporter = new RecipeImporter(mockHttpClient, _mockIngredientService.Object);
            
            _viewModel = new AddRecipeViewModel(
                _mockRecipeService.Object, 
                _mockIngredientService.Object, 
                _recipeImporter,
                _mockFolderService.Object);
        }

        [Fact]
        public void AddRecipeViewModel_WhenCreated_ShouldInitializeWithDefaultValues()
        {
            // Arrange
            var mockHttpClient = new HttpClient();
            var recipeImporter = new RecipeImporter(mockHttpClient, _mockIngredientService.Object);

            // Act
            var viewModel = new AddRecipeViewModel(
                _mockRecipeService.Object, 
                _mockIngredientService.Object, 
                recipeImporter,
                _mockFolderService.Object);

            // Assert
            viewModel.Name.Should().BeEmpty();
            viewModel.Description.Should().BeEmpty();
            viewModel.IloscPorcji.Should().Be("2");
            viewModel.Calories.Should().Be("0");
            viewModel.Protein.Should().Be("0");
            viewModel.Fat.Should().Be("0");
            viewModel.Carbs.Should().Be("0");
            viewModel.UseCalculatedValues.Should().BeTrue();
            viewModel.IsManualMode.Should().BeTrue();
            viewModel.Title.Should().Be("Nowy przepis");
            viewModel.SaveButtonText.Should().Be("Dodaj przepis");
        }

        [Fact]
        public void AddRecipeViewModel_WhenNameIsEmpty_ShouldHaveValidationError()
        {
            // Arrange & Act
            _viewModel.Name = "";

            // Assert
            _viewModel.HasValidationError.Should().BeTrue();
            _viewModel.ValidationMessage.Should().Be("Nazwa przepisu jest wymagana");
        }

        [Fact]
        public void AddRecipeViewModel_WhenPortionsIsInvalid_ShouldHaveValidationError()
        {
            // Arrange
            _viewModel.Name = "Test Recipe";

            // Act
            _viewModel.IloscPorcji = "invalid";

            // Assert
            _viewModel.HasValidationError.Should().BeTrue();
            _viewModel.ValidationMessage.Should().Be("Ilość porcji musi być liczbą całkowitą większą od 0");
        }

        [Fact]
        public void AddRecipeViewModel_AddIngredientCommand_ShouldAddIngredientToCollection()
        {
            // Act
            ((Command)_viewModel.AddIngredientCommand).Execute(null);

            // Assert
            _viewModel.Ingredients.Should().HaveCount(1);
            _viewModel.Ingredients.First().Quantity.Should().Be(1);
            _viewModel.Ingredients.First().Unit.Should().Be(Unit.Gram);
        }

        [Fact]
        public void AddRecipeViewModel_RemoveIngredientCommand_ShouldRemoveIngredientFromCollection()
        {
            // Arrange
            var ingredient = new Ingredient { Name = "Test", Quantity = 100, Unit = Unit.Gram };
            _viewModel.Ingredients.Add(ingredient);

            // Act
            ((Command<Ingredient>)_viewModel.RemoveIngredientCommand).Execute(ingredient);

            // Assert
            _viewModel.Ingredients.Should().BeEmpty();
        }
    }
}
