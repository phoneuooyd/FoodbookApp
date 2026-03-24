using FluentAssertions;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
using FoodbookApp.Services;
using Microsoft.Maui.Controls;
using Moq;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;

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

        [Fact]
        public async Task ImportRecipeAsync_FreeWithUnmatchedIngredients_ShouldSkipAiAndShowPremiumMessage()
        {
            // Arrange
            var aiServiceMock = new Mock<IAIService>(MockBehavior.Strict);
            var ingredientServiceMock = new Mock<IIngredientService>();
            ingredientServiceMock.Setup(s => s.GetIngredientsAsync()).ReturnsAsync(new List<Ingredient>());

            var featureAccessMock = new Mock<IFeatureAccessService>();
            featureAccessMock.Setup(s => s.CanUsePremiumFeatureAsync(PremiumFeature.AiRecipeCreation)).ReturnsAsync(false);

            var importer = new RecipeImporter(
                CreateHttpClientWithHtml("<html><body><h2>Składniki</h2><ul><li>1 marchewka</li></ul></body></html>"),
                ingredientServiceMock.Object,
                aiServiceMock.Object);
            var viewModel = CreateViewModel(importer, ingredientServiceMock.Object, featureAccessMock.Object);
            viewModel.ImportUrl = "https://example.com/free";

            // Act
            await InvokeImportRecipeAsync(viewModel);

            // Assert
            viewModel.ImportStatus.Should().Be("Import AI dostępny w Premium");
            aiServiceMock.Verify(s => s.GetAIResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ImportRecipeAsync_PremiumWithFullLocalMatch_ShouldNotCallAiAndShowImportedStatus()
        {
            // Arrange
            var aiServiceMock = new Mock<IAIService>(MockBehavior.Strict);
            var ingredientServiceMock = new Mock<IIngredientService>();
            ingredientServiceMock
                .Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(new List<Ingredient> { new() { Name = "mleko", Calories = 64 } });

            var featureAccessMock = new Mock<IFeatureAccessService>();
            featureAccessMock.Setup(s => s.CanUsePremiumFeatureAsync(PremiumFeature.AiRecipeCreation)).ReturnsAsync(true);

            var importer = new RecipeImporter(
                CreateHttpClientWithHtml("<html><body><h2>Składniki</h2><ul><li>200 ml mleko</li></ul></body></html>"),
                ingredientServiceMock.Object,
                aiServiceMock.Object);
            var viewModel = CreateViewModel(importer, ingredientServiceMock.Object, featureAccessMock.Object);
            viewModel.ImportUrl = "https://example.com/premium-local";

            // Act
            await InvokeImportRecipeAsync(viewModel);

            // Assert
            viewModel.ImportStatus.Should().Be("Zaimportowano!");
            aiServiceMock.Verify(s => s.GetAIResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ImportRecipeAsync_PremiumWithUnmatchedIngredients_ShouldCallAiAndShowFallbackStatus()
        {
            // Arrange
            var aiServiceMock = new Mock<IAIService>();
            aiServiceMock
                .Setup(s => s.GetAIResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("""{"title":"Test","calories":0,"protein":0,"fat":0,"carbs":0,"ingredients":[{"raw_name":"1 marchewka","quantity":1,"unit":"piece","matched_db_name":null}]}""");

            var ingredientServiceMock = new Mock<IIngredientService>();
            ingredientServiceMock.Setup(s => s.GetIngredientsAsync()).ReturnsAsync(new List<Ingredient>());

            var featureAccessMock = new Mock<IFeatureAccessService>();
            featureAccessMock.Setup(s => s.CanUsePremiumFeatureAsync(PremiumFeature.AiRecipeCreation)).ReturnsAsync(true);

            var importer = new RecipeImporter(
                CreateHttpClientWithHtml("<html><body><h2>Składniki</h2><ul><li>1 marchewka</li></ul></body></html>"),
                ingredientServiceMock.Object,
                aiServiceMock.Object);
            var viewModel = CreateViewModel(importer, ingredientServiceMock.Object, featureAccessMock.Object);
            viewModel.ImportUrl = "https://example.com/premium-ai";

            // Act
            await InvokeImportRecipeAsync(viewModel);

            // Assert
            viewModel.ImportStatus.Should().Be("Uruchomiono AI fallback");
            aiServiceMock.Verify(s => s.GetAIResponseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private AddRecipeViewModel CreateViewModel(RecipeImporter importer, IIngredientService ingredientService, IFeatureAccessService featureAccessService)
            => new(
                _mockRecipeService.Object,
                ingredientService,
                importer,
                _mockFolderService.Object,
                featureAccessService: featureAccessService);

        private static async Task InvokeImportRecipeAsync(AddRecipeViewModel viewModel)
        {
            var method = typeof(AddRecipeViewModel).GetMethod("ImportRecipeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            var task = method!.Invoke(viewModel, null) as Task;
            task.Should().NotBeNull();
            await task!;
        }

        private static HttpClient CreateHttpClientWithHtml(string html)
            => new(new StubHttpMessageHandler(html))
            {
                BaseAddress = new Uri("https://example.com")
            };

        private sealed class StubHttpMessageHandler(string html) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html, Encoding.UTF8, "text/html")
                });
        }
    }
}
