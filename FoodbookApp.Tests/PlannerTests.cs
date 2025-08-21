using Foodbook.Models;
using System.ComponentModel;
using Xunit;

namespace FoodbookApp.Tests
{
    public class PlannerTests
    {
        [Fact]
        public void PlannerDay_DefaultConstructor_ShouldInitializeCorrectly()
        {
            // Arrange
            var date = new DateTime(2024, 12, 25);

            // Act
            var plannerDay = new PlannerDay(date);

            // Assert
            Assert.Equal(date, plannerDay.Date);
            Assert.NotNull(plannerDay.Meals);
            Assert.Empty(plannerDay.Meals);
        }

        [Fact]
        public void PlannerDay_AddMeal_ShouldUpdateMealsCollection()
        {
            // Arrange
            var date = new DateTime(2024, 12, 25);
            var plannerDay = new PlannerDay(date);
            var meal = new PlannedMeal { RecipeId = 1, Date = date, Portions = 2 };

            // Act
            plannerDay.Meals.Add(meal);

            // Assert
            Assert.Single(plannerDay.Meals);
            Assert.Contains(meal, plannerDay.Meals);
        }

        [Fact]
        public void PlannedMeal_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var plannedMeal = new PlannedMeal();

            // Assert
            Assert.Equal(0, plannedMeal.Id);
            Assert.Equal(0, plannedMeal.RecipeId);
            Assert.Null(plannedMeal.Recipe);
            Assert.Equal(DateTime.MinValue, plannedMeal.Date);
            Assert.Equal(1, plannedMeal.Portions); // Default value is 1
        }

        [Fact]
        public void PlannedMeal_SetBasicProperties_ShouldUpdateCorrectly()
        {
            // Arrange
            var plannedMeal = new PlannedMeal();
            var date = new DateTime(2024, 12, 25);
            var recipe = new Recipe { Id = 1, Name = "Kotlet schabowy" };

            // Act
            plannedMeal.Id = 1;
            plannedMeal.RecipeId = 1;
            plannedMeal.Recipe = recipe;
            plannedMeal.Date = date;
            plannedMeal.Portions = 4;

            // Assert
            Assert.Equal(1, plannedMeal.Id);
            Assert.Equal(1, plannedMeal.RecipeId);
            Assert.Same(recipe, plannedMeal.Recipe);
            Assert.Equal(date, plannedMeal.Date);
            Assert.Equal(4, plannedMeal.Portions);
        }

        [Fact]
        public void PlannedMeal_ImplementsINotifyPropertyChanged()
        {
            // Arrange
            var plannedMeal = new PlannedMeal();

            // Act & Assert
            Assert.IsAssignableFrom<INotifyPropertyChanged>(plannedMeal);
        }

        [Fact]
        public void PlannedMeal_Portions_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var plannedMeal = new PlannedMeal();
            var eventRaised = false;
            string? propertyName = null;

            plannedMeal.PropertyChanged += (sender, e) =>
            {
                eventRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            plannedMeal.Portions = 3;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(PlannedMeal.Portions), propertyName);
            Assert.Equal(3, plannedMeal.Portions);
        }

        [Fact]
        public void PlannedMeal_Recipe_WhenChanged_ShouldRaisePropertyChangedEvent()
        {
            // Arrange
            var plannedMeal = new PlannedMeal();
            var recipe = new Recipe { Id = 1, Name = "Pierogi" };
            var eventRaised = false;
            string? propertyName = null;

            plannedMeal.PropertyChanged += (sender, e) =>
            {
                eventRaised = true;
                propertyName = e.PropertyName;
            };

            // Act
            plannedMeal.Recipe = recipe;

            // Assert
            Assert.True(eventRaised);
            Assert.Equal(nameof(PlannedMeal.Recipe), propertyName);
            Assert.Same(recipe, plannedMeal.Recipe);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(6)]
        public void PlannedMeal_Portions_WhenSetToValidValue_ShouldUpdateCorrectly(int portions)
        {
            // Arrange
            var plannedMeal = new PlannedMeal();

            // Act
            plannedMeal.Portions = portions;

            // Assert
            Assert.Equal(portions, plannedMeal.Portions);
        }

        [Fact]
        public void Plan_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var plan = new Plan();

            // Assert
            Assert.Equal(0, plan.Id);
            Assert.Equal(DateTime.MinValue, plan.StartDate);
            Assert.Equal(DateTime.MinValue, plan.EndDate);
            Assert.False(plan.IsArchived); // Default value is false
            Assert.Equal("Lista zakupów", plan.Label);
        }

        [Fact]
        public void Plan_SetBasicProperties_ShouldUpdateCorrectly()
        {
            // Arrange
            var plan = new Plan();
            var startDate = new DateTime(2024, 12, 1);
            var endDate = new DateTime(2024, 12, 7);

            // Act
            plan.Id = 1;
            plan.StartDate = startDate;
            plan.EndDate = endDate;
            plan.IsArchived = true;

            // Assert
            Assert.Equal(1, plan.Id);
            Assert.Equal(startDate, plan.StartDate);
            Assert.Equal(endDate, plan.EndDate);
            Assert.True(plan.IsArchived);
            Assert.Equal("Lista zakupów", plan.Label);
        }

        [Fact]
        public void PlannerDay_RemoveMeal_ShouldUpdateMealsCollection()
        {
            // Arrange
            var date = new DateTime(2024, 12, 25);
            var plannerDay = new PlannerDay(date);
            var meal1 = new PlannedMeal { RecipeId = 1, Date = date };
            var meal2 = new PlannedMeal { RecipeId = 2, Date = date };
            plannerDay.Meals.Add(meal1);
            plannerDay.Meals.Add(meal2);

            // Act
            plannerDay.Meals.Remove(meal1);

            // Assert
            Assert.Single(plannerDay.Meals);
            Assert.DoesNotContain(meal1, plannerDay.Meals);
            Assert.Contains(meal2, plannerDay.Meals);
        }

        [Fact]
        public void PlannerDay_ClearMeals_ShouldEmptyCollection()
        {
            // Arrange
            var date = new DateTime(2024, 12, 25);
            var plannerDay = new PlannerDay(date);
            plannerDay.Meals.Add(new PlannedMeal { RecipeId = 1 });
            plannerDay.Meals.Add(new PlannedMeal { RecipeId = 2 });

            // Act
            plannerDay.Meals.Clear();

            // Assert
            Assert.Empty(plannerDay.Meals);
        }

        [Fact]
        public void PlannerDay_WithMultipleMeals_ShouldMaintainCorrectCount()
        {
            // Arrange
            var date = new DateTime(2024, 12, 25);
            var plannerDay = new PlannerDay(date);
            var meals = new List<PlannedMeal>
            {
                new PlannedMeal { RecipeId = 1, Date = date, Portions = 2 },
                new PlannedMeal { RecipeId = 2, Date = date, Portions = 1 },
                new PlannedMeal { RecipeId = 3, Date = date, Portions = 3 }
            };

            // Act
            foreach (var meal in meals)
            {
                plannerDay.Meals.Add(meal);
            }

            // Assert
            Assert.Equal(3, plannerDay.Meals.Count);
            Assert.All(meals, meal => Assert.Contains(meal, plannerDay.Meals));
        }
    }
}
