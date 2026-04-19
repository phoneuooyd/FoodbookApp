using Foodbook.Models;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
using FluentAssertions;
using Moq;

namespace FoodbookApp.Tests;

public class DietStatisticsViewModelTests
{
    [Fact]
    public async Task LoadAsync_WithPlannedMeals_ShouldCalculateCaloriesAndMacros()
    {
        var plannerService = new Mock<IPlannerService>();
        var planService = new Mock<IPlanService>();
        var recipeService = new Mock<IRecipeService>();
        var ingredientService = new Mock<IIngredientService>();
        var localizationService = new Mock<ILocalizationService>();
        var preferencesService = new Mock<IPreferencesService>();

        localizationService.SetupGet(s => s.CurrentCulture).Returns(System.Globalization.CultureInfo.InvariantCulture);
        localizationService.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>())).Returns("text");
        preferencesService.Setup(s => s.GetDietStatisticsMeals()).Returns(Array.Empty<Foodbook.Models.DTOs.DietStatisticsMealDto>());

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = "Test recipe",
            Calories = 400,
            Carbs = 40,
            Fat = 10,
            Protein = 20,
            IloscPorcji = 2
        };

        var meal = new PlannedMeal
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Recipe = recipe,
            Portions = 2,
            Date = DateTime.Today
        };

        plannerService
            .Setup(s => s.GetPlannedMealsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<PlannedMeal> { meal });

        planService
            .Setup(s => s.GetPlansAsync())
            .ReturnsAsync(new List<Plan>());

        recipeService
            .Setup(s => s.GetRecipesAsync())
            .ReturnsAsync(new List<Recipe> { recipe });

        var sut = new DietStatisticsViewModel(
            plannerService.Object,
            planService.Object,
            recipeService.Object,
            ingredientService.Object,
            localizationService.Object,
            preferencesService.Object);

        await sut.LoadAsync();

        sut.ConsumedCalories.Should().Be(400);
        sut.ConsumedCarbs.Should().Be(40);
        sut.ConsumedFat.Should().Be(10);
        sut.ConsumedProtein.Should().Be(20);
        sut.MealSlots.Should().HaveCount(1);
        sut.ActualCarbsPercent.Should().BeApproximately(57.1, 0.2);
        sut.ActualFatPercent.Should().BeApproximately(14.3, 0.2);
        sut.ActualProteinPercent.Should().BeApproximately(28.6, 0.2);
    }

    [Fact]
    public async Task SelectPlanCommand_WhenExecuted_ShouldSwitchToPlanRange()
    {
        var plannerService = new Mock<IPlannerService>();
        var planService = new Mock<IPlanService>();
        var recipeService = new Mock<IRecipeService>();
        var ingredientService = new Mock<IIngredientService>();
        var localizationService = new Mock<ILocalizationService>();
        var preferencesService = new Mock<IPreferencesService>();

        localizationService.SetupGet(s => s.CurrentCulture).Returns(System.Globalization.CultureInfo.InvariantCulture);
        localizationService.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>())).Returns("text");
        preferencesService.Setup(s => s.GetDietStatisticsMeals()).Returns(Array.Empty<Foodbook.Models.DTOs.DietStatisticsMealDto>());

        plannerService
            .Setup(s => s.GetPlannedMealsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<PlannedMeal>());
        plannerService
            .Setup(s => s.GetPlannedMealsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<PlannedMeal>());

        recipeService
            .Setup(s => s.GetRecipesAsync())
            .ReturnsAsync(new List<Recipe>());

        var selectedPlan = new Plan
        {
            Id = Guid.NewGuid(),
            Type = PlanType.Planner,
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2026, 4, 7)
        };

        planService
            .Setup(s => s.GetPlansAsync())
            .ReturnsAsync(new List<Plan> { selectedPlan });

        var sut = new DietStatisticsViewModel(
            plannerService.Object,
            planService.Object,
            recipeService.Object,
            ingredientService.Object,
            localizationService.Object,
            preferencesService.Object);

        await sut.LoadAsync();
        sut.SelectPlanCommand.Execute(selectedPlan);
        await Task.Delay(20);

        sut.SelectedFilter.Should().Be(Foodbook.ViewModels.FilterMode.Plan);
        sut.SelectedPlan.Should().Be(selectedPlan);
        sut.FilterStartDate.Should().Be(selectedPlan.StartDate.Date);
        sut.FilterEndDate.Should().Be(selectedPlan.EndDate.Date);
    }

    [Fact]
    public async Task LoadAsync_ShouldExposeOnlyTodaysMealsInMealSlots()
    {
        var plannerService = new Mock<IPlannerService>();
        var planService = new Mock<IPlanService>();
        var recipeService = new Mock<IRecipeService>();
        var ingredientService = new Mock<IIngredientService>();
        var localizationService = new Mock<ILocalizationService>();
        var preferencesService = new Mock<IPreferencesService>();

        localizationService.SetupGet(s => s.CurrentCulture).Returns(System.Globalization.CultureInfo.InvariantCulture);
        localizationService.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>())).Returns("text");
        preferencesService.Setup(s => s.GetDietStatisticsMeals()).Returns(Array.Empty<Foodbook.Models.DTOs.DietStatisticsMealDto>());

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            Name = "Test recipe",
            Calories = 300,
            Carbs = 30,
            Fat = 10,
            Protein = 20,
            IloscPorcji = 1
        };

        var todayMeal = new PlannedMeal
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Recipe = recipe,
            Portions = 1,
            Date = DateTime.Today
        };

        var olderMeal = new PlannedMeal
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            Recipe = recipe,
            Portions = 1,
            Date = DateTime.Today.AddDays(-2)
        };

        plannerService
            .Setup(s => s.GetPlannedMealsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((DateTime from, DateTime to) =>
            {
                if (from.Date == DateTime.Today && to.Date == DateTime.Today)
                {
                    return new List<PlannedMeal> { todayMeal };
                }

                return new List<PlannedMeal> { olderMeal, todayMeal };
            });

        planService
            .Setup(s => s.GetPlansAsync())
            .ReturnsAsync(new List<Plan>());

        recipeService
            .Setup(s => s.GetRecipesAsync())
            .ReturnsAsync(new List<Recipe> { recipe });

        var sut = new DietStatisticsViewModel(
            plannerService.Object,
            planService.Object,
            recipeService.Object,
            ingredientService.Object,
            localizationService.Object,
            preferencesService.Object);

        await sut.LoadAsync();

        sut.MealSlots.Should().HaveCount(1);
        sut.MealSlots[0].MealDate.Should().Be(DateTime.Today);
    }
}
