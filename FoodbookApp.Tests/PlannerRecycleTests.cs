using Foodbook.Models;
using Foodbook.ViewModels;
using FoodbookApp.Interfaces;
using Moq;

namespace FoodbookApp.Tests;

public class PlannerRecycleTests
{
    [Fact]
    public async Task LoadFromArchiveAsync_ShouldMapMealsToDefaultWeek()
    {
        var sourcePlanId = Guid.NewGuid();
        var recipeA = new Recipe { Id = Guid.NewGuid(), Name = "A", IloscPorcji = 2 };
        var recipeB = new Recipe { Id = Guid.NewGuid(), Name = "B", IloscPorcji = 4 };

        var archivedPlan = new Plan
        {
            Id = sourcePlanId,
            Type = PlanType.Planner,
            IsArchived = true,
            StartDate = new DateTime(2024, 01, 01),
            EndDate = new DateTime(2024, 01, 07),
            PlannedMeals = new List<PlannedMeal>
            {
                new() { Id = Guid.NewGuid(), RecipeId = recipeA.Id, Recipe = recipeA, Date = new DateTime(2024, 01, 01), Portions = 2 },
                new() { Id = Guid.NewGuid(), RecipeId = recipeB.Id, Recipe = recipeB, Date = new DateTime(2024, 01, 02), Portions = 3 },
            }
        };

        var plannerServiceMock = new Mock<IPlannerService>(MockBehavior.Strict);
        var recipeServiceMock = new Mock<IRecipeService>(MockBehavior.Strict);
        var planServiceMock = new Mock<IPlanService>(MockBehavior.Strict);

        recipeServiceMock.Setup(x => x.GetRecipesAsync()).ReturnsAsync(new List<Recipe> { recipeA, recipeB });
        planServiceMock.Setup(x => x.GetArchivedPlanWithMealsAsync(sourcePlanId)).ReturnsAsync(archivedPlan);

        var vm = new PlannerViewModel(plannerServiceMock.Object, recipeServiceMock.Object, planServiceMock.Object);

        await vm.LoadFromArchiveAsync(sourcePlanId);

        Assert.Equal(DateTime.Today, vm.StartDate);
        Assert.Equal(DateTime.Today.AddDays(6), vm.EndDate);
        Assert.Equal(7, vm.Days.Count);

        Assert.Single(vm.Days[0].Meals.Where(m => m.RecipeId == recipeA.Id));
        Assert.Single(vm.Days[1].Meals.Where(m => m.RecipeId == recipeB.Id));

        Assert.Equal(recipeA, vm.Days[0].Meals.First(m => m.RecipeId == recipeA.Id).Recipe);
        Assert.Equal(2, vm.Days[0].Meals.First(m => m.RecipeId == recipeA.Id).Portions);
    }

    [Fact]
    public async Task LoadFromArchiveAsync_ShouldAdjustMealsPerDayBasedOnSource()
    {
        var sourcePlanId = Guid.NewGuid();
        var recipe = new Recipe { Id = Guid.NewGuid(), Name = "Meal", IloscPorcji = 1 };

        var archivedPlan = new Plan
        {
            Id = sourcePlanId,
            Type = PlanType.Planner,
            IsArchived = true,
            PlannedMeals = new List<PlannedMeal>
            {
                new() { RecipeId = recipe.Id, Recipe = recipe, Date = new DateTime(2024, 01, 01), Portions = 1 },
                new() { RecipeId = recipe.Id, Recipe = recipe, Date = new DateTime(2024, 01, 01), Portions = 2 },
                new() { RecipeId = recipe.Id, Recipe = recipe, Date = new DateTime(2024, 01, 01), Portions = 3 }
            }
        };

        var plannerServiceMock = new Mock<IPlannerService>(MockBehavior.Strict);
        var recipeServiceMock = new Mock<IRecipeService>(MockBehavior.Strict);
        var planServiceMock = new Mock<IPlanService>(MockBehavior.Strict);

        recipeServiceMock.Setup(x => x.GetRecipesAsync()).ReturnsAsync(new List<Recipe> { recipe });
        planServiceMock.Setup(x => x.GetArchivedPlanWithMealsAsync(sourcePlanId)).ReturnsAsync(archivedPlan);

        var vm = new PlannerViewModel(plannerServiceMock.Object, recipeServiceMock.Object, planServiceMock.Object);

        await vm.LoadFromArchiveAsync(sourcePlanId);

        Assert.Equal(3, vm.MealsPerDay);
        Assert.True(vm.Days.All(d => d.Meals.Count == 3));
    }
}
