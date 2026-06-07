using FluentAssertions;
using Foodbook.Models;
using Foodbook.Services;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
using Microsoft.Maui.Controls;
using Moq;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;

namespace FoodbookApp.Tests
{
    public class AddRecipePageTests : IDisposable
    {
        private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUICulture;
        private readonly Mock<IRecipeService> _mockRecipeService;
        private readonly Mock<IIngredientService> _mockIngredientService;
        private readonly Mock<IFolderService> _mockFolderService;
        private readonly Mock<IRecipeLabelService> _mockLabelService;
        private readonly RecipeImporter _recipeImporter;
        private readonly AddRecipeViewModel _viewModel;

        public AddRecipePageTests()
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUICulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = PolishCulture;
            CultureInfo.CurrentUICulture = PolishCulture;

            _mockRecipeService = new Mock<IRecipeService>();
            _mockIngredientService = new Mock<IIngredientService>();
            _mockFolderService = new Mock<IFolderService>();
            _mockLabelService = new Mock<IRecipeLabelService>();

            // Setup basic empty list for ingredient service
            _mockIngredientService.Setup(s => s.GetIngredientsAsync())
                .ReturnsAsync(new List<Ingredient>());

            // Setup folder service to avoid nulls/background loads
            _mockFolderService.Setup(s => s.GetFolderHierarchyAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Folder>());

            // Setup label service with empty list by default (per-test override as needed)
            _mockLabelService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<RecipeLabel>());

            // Tworzenie rzeczywistego RecipeImporter z mock HttpClient
            var mockHttpClient = new HttpClient();
            _recipeImporter = new RecipeImporter(mockHttpClient, _mockIngredientService.Object);

            _viewModel = new AddRecipeViewModel(
                _mockRecipeService.Object,
                _mockIngredientService.Object,
                _recipeImporter,
                _mockFolderService.Object,
                databaseService: null,
                labelService: _mockLabelService.Object);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUICulture;
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
                _mockFolderService.Object,
                databaseService: null,
                labelService: _mockLabelService.Object);

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

        // ─────────────────────────────────────────────────────────────────────
        // Label persistence tests (regression coverage for ManageLabels modal flow)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task SaveRecipeCommand_WhenLabelsSelected_ShouldAddRecipeWithLabels()
        {
            // Arrange
            _viewModel.Name = "Test Recipe";
            var label1 = new RecipeLabel { Id = Guid.NewGuid(), Name = "Obiad", ColorHex = "#FF0000" };
            var label2 = new RecipeLabel { Id = Guid.NewGuid(), Name = "Wegetariańskie", ColorHex = "#00FF00" };
            _viewModel.SelectedLabels.Add(label1);
            _viewModel.SelectedLabels.Add(label2);

            var capturedTcs = new TaskCompletionSource<Recipe>();
            _mockRecipeService
                .Setup(s => s.AddRecipeAsync(It.IsAny<Recipe>()))
                .Callback<Recipe>(r => capturedTcs.TrySetResult(CloneRecipeSnapshot(r)))
                .Returns(Task.CompletedTask);

            // Act
            ((Command)_viewModel.SaveRecipeCommand).Execute(null);
            var captured = await capturedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            captured.Should().NotBeNull();
            captured.Name.Should().Be("Test Recipe");
            captured.Labels.Should().HaveCount(2);
            captured.Labels.Select(l => l.Id).Should().Contain(new[] { label1.Id, label2.Id });
            captured.Labels.Select(l => l.Name).Should().Contain(new[] { "Obiad", "Wegetariańskie" });
        }

        [Fact]
        public async Task SaveRecipeCommand_WhenNoLabelsSelected_ShouldAddRecipeWithEmptyLabels()
        {
            // Arrange
            _viewModel.Name = "Plain Recipe";

            var capturedTcs = new TaskCompletionSource<Recipe>();
            _mockRecipeService
                .Setup(s => s.AddRecipeAsync(It.IsAny<Recipe>()))
                .Callback<Recipe>(r => capturedTcs.TrySetResult(CloneRecipeSnapshot(r)))
                .Returns(Task.CompletedTask);

            // Act
            ((Command)_viewModel.SaveRecipeCommand).Execute(null);
            var captured = await capturedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            captured.Labels.Should().NotBeNull();
            captured.Labels.Should().BeEmpty();
        }

        [Fact]
        public async Task SaveRecipeCommand_AfterApplySelectedLabelIds_ShouldPersistInRecipe()
        {
            // Arrange — symuluje user flow: AvailableLabels załadowane, modal zwraca wybrane Id
            _viewModel.Name = "Recipe With Applied Label";
            var availableLabel = new RecipeLabel { Id = Guid.NewGuid(), Name = "Śniadanie", ColorHex = "#8B72FF" };
            _viewModel.AvailableLabels.Add(availableLabel);

            _viewModel.ApplySelectedLabelIds(new[] { availableLabel.Id });

            var capturedTcs = new TaskCompletionSource<Recipe>();
            _mockRecipeService
                .Setup(s => s.AddRecipeAsync(It.IsAny<Recipe>()))
                .Callback<Recipe>(r => capturedTcs.TrySetResult(CloneRecipeSnapshot(r)))
                .Returns(Task.CompletedTask);

            // Act
            ((Command)_viewModel.SaveRecipeCommand).Execute(null);
            var captured = await capturedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            captured.Labels.Should().ContainSingle();
            captured.Labels[0].Id.Should().Be(availableLabel.Id);
            captured.Labels[0].Name.Should().Be("Śniadanie");
        }

        [Fact]
        public async Task SaveRecipeCommand_InEditMode_ShouldUpdateRecipeWithNewlyAddedLabel()
        {
            // Arrange — istniejący przepis bez etykiet, user dodaje jedną przez modal
            var existingRecipeId = Guid.NewGuid();
            var existingRecipe = new Recipe
            {
                Id = existingRecipeId,
                Name = "Existing Recipe",
                IloscPorcji = 4,
                Labels = new List<RecipeLabel>()
            };

            // Przejdź w tryb edycji przez bezpośrednie ustawienie _editingRecipe (omija MainThread w LoadRecipeAsync)
            SetPrivateField(_viewModel, "_editingRecipe", existingRecipe);
            _viewModel.Name = existingRecipe.Name;
            _viewModel.IloscPorcji = existingRecipe.IloscPorcji.ToString();

            var addedLabel = new RecipeLabel { Id = Guid.NewGuid(), Name = "Kolacja", ColorHex = "#EC4899" };
            _viewModel.SelectedLabels.Add(addedLabel);

            var capturedTcs = new TaskCompletionSource<Recipe>();
            _mockRecipeService
                .Setup(s => s.UpdateRecipeAsync(It.IsAny<Recipe>()))
                .Callback<Recipe>(r => capturedTcs.TrySetResult(CloneRecipeSnapshot(r)))
                .Returns(Task.CompletedTask);

            // Act
            ((Command)_viewModel.SaveRecipeCommand).Execute(null);
            var captured = await capturedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            _mockRecipeService.Verify(s => s.AddRecipeAsync(It.IsAny<Recipe>()), Times.Never,
                "edit mode should call UpdateRecipeAsync, not AddRecipeAsync");
            captured.Id.Should().Be(existingRecipeId);
            captured.Labels.Should().ContainSingle();
            captured.Labels[0].Id.Should().Be(addedLabel.Id);
            captured.Labels[0].Name.Should().Be("Kolacja");
        }

        [Fact]
        public void ApplySelectedLabelIds_ShouldPopulateSelectedLabelsFromAvailable()
        {
            // Arrange
            var l1 = new RecipeLabel { Id = Guid.NewGuid(), Name = "A" };
            var l2 = new RecipeLabel { Id = Guid.NewGuid(), Name = "B" };
            var l3 = new RecipeLabel { Id = Guid.NewGuid(), Name = "C" };
            _viewModel.AvailableLabels.Add(l1);
            _viewModel.AvailableLabels.Add(l2);
            _viewModel.AvailableLabels.Add(l3);

            // Act — wybieramy 1 i 3
            _viewModel.ApplySelectedLabelIds(new[] { l1.Id, l3.Id });

            // Assert
            _viewModel.SelectedLabels.Should().HaveCount(2);
            _viewModel.SelectedLabels.Select(l => l.Id).Should().BeEquivalentTo(new[] { l1.Id, l3.Id });
            _viewModel.SelectedLabels.Should().NotContain(l2);
        }

        [Fact]
        public void SelectedLabels_ClearAndAdd_ShouldFireCollectionChanged()
        {
            // Arrange — guard przed regresją: fix opiera się na in-place mutation,
            // jeśli ktoś zmieni na reassign kolekcji, ten test pomoże złapać efekt uboczny
            var preexisting = new RecipeLabel { Id = Guid.NewGuid(), Name = "Old" };
            _viewModel.SelectedLabels.Add(preexisting);

            var eventCount = 0;
            NotifyCollectionChangedEventHandler handler = (_, _) => eventCount++;
            _viewModel.SelectedLabels.CollectionChanged += handler;

            try
            {
                // Act — symuluje to co robi fix po zamknięciu modala
                _viewModel.SelectedLabels.Clear();
                var newLabel = new RecipeLabel { Id = Guid.NewGuid(), Name = "New" };
                _viewModel.SelectedLabels.Add(newLabel);

                // Assert
                eventCount.Should().BeGreaterThanOrEqualTo(2, "Clear oraz Add powinny wyemitować zdarzenia");
                _viewModel.SelectedLabels.Should().ContainSingle();
                _viewModel.SelectedLabels[0].Name.Should().Be("New");
            }
            finally
            {
                _viewModel.SelectedLabels.CollectionChanged -= handler;
            }
        }

        // ─── HELPERS ─────────────────────────────────────────────────────────

        private static Recipe CloneRecipeSnapshot(Recipe r) => new Recipe
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IloscPorcji = r.IloscPorcji,
            Calories = r.Calories,
            Protein = r.Protein,
            Fat = r.Fat,
            Carbs = r.Carbs,
            FolderId = r.FolderId,
            Ingredients = r.Ingredients?.ToList() ?? new List<Ingredient>(),
            Labels = r.Labels?.Select(l => new RecipeLabel
            {
                Id = l.Id,
                Name = l.Name,
                ColorHex = l.ColorHex,
                CreatedAt = l.CreatedAt
            }).ToList() ?? new List<RecipeLabel>()
        };

        private static void SetPrivateField(object target, string fieldName, object? value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException($"Pole '{fieldName}' nie znalezione w {target.GetType().Name}");
            field.SetValue(target, value);
        }
    }
}
