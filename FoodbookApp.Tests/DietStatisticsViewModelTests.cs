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
        var recipeService = new Mock<IRecipeService>();
        var planService = new Mock<IPlanService>();
        var plannerService = new Mock<IPlannerService>();
        var localizationService = new Mock<ILocalizationService>();

        localizationService.SetupGet(s => s.CurrentCulture).Returns(System.Globalization.CultureInfo.InvariantCulture);
        localizationService.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>())).Returns("text");

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
            .Setup(s => s.GetRecipeAsync(It.IsAny<Guid>()))
            .ReturnsAsync(recipe);

        var sut = new DietStatisticsViewModel(
            recipeService.Object,
            planService.Object,
            plannerService.Object,
            localizationService.Object);

        await sut.LoadAsync();

        sut.ConsumedCalories.Should().Be(400);
        sut.ConsumedCarbs.Should().Be(40);
        sut.ConsumedFat.Should().Be(10);
        sut.ConsumedProtein.Should().Be(20);
        sut.MealSlots.Should().HaveCount(1);
        sut.ActualCarbsPercent.Should().BeApproximately(48.5, 0.2);
        sut.ActualFatPercent.Should().BeApproximately(27.3, 0.2);
        sut.ActualProteinPercent.Should().BeApproximately(24.2, 0.2);
    }

    [Fact]
    public async Task SelectPlanCommand_WhenExecuted_ShouldSwitchToPlanRange()
    {
        var recipeService = new Mock<IRecipeService>();
        var planService = new Mock<IPlanService>();
        var plannerService = new Mock<IPlannerService>();
        var localizationService = new Mock<ILocalizationService>();

        localizationService.SetupGet(s => s.CurrentCulture).Returns(System.Globalization.CultureInfo.InvariantCulture);
        localizationService.Setup(s => s.GetString(It.IsAny<string>(), It.IsAny<string>())).Returns("text");

        plannerService
            .Setup(s => s.GetPlannedMealsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<PlannedMeal>());

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
            recipeService.Object,
            planService.Object,
            plannerService.Object,
            localizationService.Object);

        await sut.LoadAsync();
        sut.SelectPlanCommand.Execute(selectedPlan);
        await Task.Delay(20);

        sut.SelectedFilter.Should().Be(Foodbook.ViewModels.FilterMode.Plan);
        sut.SelectedPlan.Should().Be(selectedPlan);
        sut.FilterStartDate.Should().Be(selectedPlan.StartDate.Date);
        sut.FilterEndDate.Should().Be(selectedPlan.EndDate.Date);
    }
}
