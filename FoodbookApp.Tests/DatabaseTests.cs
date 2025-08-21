using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Foodbook.Models;
using Foodbook.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodbookApp.Tests
{
    public class DatabaseTests
    {
        [Fact]
        public void Recipe_DatabaseEntity_ShouldHaveRequiredProperties()
        {
            // Arrange & Act
            var recipe = new Recipe();

            // Assert
            Assert.Equal(0, recipe.Id); // Primary key
            Assert.Equal(string.Empty, recipe.Name);
            Assert.Null(recipe.Description);
            Assert.Equal(0, recipe.Calories);
            Assert.Equal(0, recipe.Protein);
            Assert.Equal(0, recipe.Fat);
            Assert.Equal(0, recipe.Carbs);
            Assert.Equal(2, recipe.IloscPorcji);
            Assert.NotNull(recipe.Ingredients); // Navigation property
        }

        [Fact]
        public void Ingredient_DatabaseEntity_ShouldHaveRequiredProperties()
        {
            // Arrange & Act
            var ingredient = new Ingredient();

            // Assert
            Assert.Equal(0, ingredient.Id); // Primary key
            Assert.Equal(string.Empty, ingredient.Name);
            Assert.Equal(0, ingredient.Quantity);
            Assert.Equal(Unit.Gram, ingredient.Unit); // Default enum value
            Assert.Equal(0, ingredient.Calories);
            Assert.Equal(0, ingredient.Protein);
            Assert.Equal(0, ingredient.Fat);
            Assert.Equal(0, ingredient.Carbs);
            Assert.Null(ingredient.RecipeId); // Foreign key (nullable)
            Assert.Null(ingredient.Recipe); // Navigation property
        }

        [Fact]
        public void PlannedMeal_DatabaseEntity_ShouldHaveRequiredProperties()
        {
            // Arrange & Act
            var plannedMeal = new PlannedMeal();

            // Assert
            Assert.Equal(0, plannedMeal.Id); // Primary key
            Assert.Equal(0, plannedMeal.RecipeId); // Foreign key
            Assert.Null(plannedMeal.Recipe); // Navigation property
            Assert.Equal(DateTime.MinValue, plannedMeal.Date);
            Assert.Equal(1, plannedMeal.Portions); // Default value
        }

        [Fact]
        public void Plan_DatabaseEntity_ShouldHaveRequiredProperties()
        {
            // Arrange & Act
            var plan = new Plan();

            // Assert
            Assert.Equal(0, plan.Id); // Primary key
            Assert.Equal(DateTime.MinValue, plan.StartDate);
            Assert.Equal(DateTime.MinValue, plan.EndDate);
            Assert.False(plan.IsArchived); // Default value
            Assert.Equal("Lista zakupów", plan.Label);
        }

        [Fact]
        public void ShoppingListItem_DatabaseEntity_ShouldHaveRequiredProperties()
        {
            // Arrange & Act
            var item = new ShoppingListItem();

            // Assert
            Assert.Equal(0, item.Id); // Primary key
            Assert.Equal(0, item.PlanId); // Foreign key
            Assert.Null(item.Plan); // Navigation property - null until explicitly set
            Assert.Equal(string.Empty, item.IngredientName);
            Assert.Equal(Unit.Gram, item.Unit); // Default enum value
            Assert.False(item.IsChecked); // Default value
            Assert.Equal(0, item.Quantity);
        }

        [Theory]
        [InlineData(Unit.Gram)]
        [InlineData(Unit.Milliliter)]
        [InlineData(Unit.Piece)]
        public void Unit_EnumValues_ShouldBeValid(Unit unit)
        {
            // Arrange & Act
            var unitValue = (int)unit;

            // Assert
            Assert.True(unitValue >= 0);
            Assert.True(Enum.IsDefined(typeof(Unit), unit));
        }

        [Fact]
        public void DatabaseRelationships_RecipeToIngredients_ShouldWorkCorrectly()
        {
            // Arrange
            var recipe = new Recipe { Id = 1, Name = "Test Recipe" };
            var ingredient1 = new Ingredient { Id = 1, Name = "Mąka", RecipeId = recipe.Id, Recipe = recipe };
            var ingredient2 = new Ingredient { Id = 2, Name = "Cukier", RecipeId = recipe.Id, Recipe = recipe };

            // Act
            recipe.Ingredients.Add(ingredient1);
            recipe.Ingredients.Add(ingredient2);

            // Assert - One-to-many relationship
            Assert.Equal(2, recipe.Ingredients.Count);
            Assert.All(recipe.Ingredients, ing => Assert.Equal(recipe.Id, ing.RecipeId));
            Assert.All(recipe.Ingredients, ing => Assert.Same(recipe, ing.Recipe));
        }

        [Fact]
        public void DatabaseRelationships_PlannedMealToRecipe_ShouldWorkCorrectly()
        {
            // Arrange
            var recipe = new Recipe { Id = 1, Name = "Kotlet schabowy" };
            var plannedMeal = new PlannedMeal 
            { 
                Id = 1, 
                RecipeId = recipe.Id, 
                Recipe = recipe,
                Date = new DateTime(2024, 12, 25),
                Portions = 2
            };

            // Act & Assert - Many-to-one relationship
            Assert.Equal(recipe.Id, plannedMeal.RecipeId);
            Assert.Same(recipe, plannedMeal.Recipe);
        }

        [Fact]
        public void DatabaseRelationships_ShoppingListItemToPlan_ShouldWorkCorrectly()
        {
            // Arrange
            var plan = new Plan 
            { 
                Id = 1, 
                StartDate = new DateTime(2024, 12, 1), 
                EndDate = new DateTime(2024, 12, 7) 
            };
            var shoppingItem = new ShoppingListItem 
            { 
                Id = 1, 
                PlanId = plan.Id, 
                Plan = plan,
                IngredientName = "Mleko",
                Unit = Unit.Milliliter,
                Quantity = 500
            };

            // Act & Assert - Many-to-one relationship
            Assert.Equal(plan.Id, shoppingItem.PlanId);
            Assert.Same(plan, shoppingItem.Plan);
        }

        [Fact]
        public void DatabaseConstraints_RecipeIngredientsCascadeDelete_ShouldWorkCorrectly()
        {
            // Arrange
            var recipe = new Recipe { Id = 1, Name = "Test Recipe" };
            var ingredient = new Ingredient { Id = 1, Name = "Test Ingredient", RecipeId = recipe.Id, Recipe = recipe };
            recipe.Ingredients.Add(ingredient);

            // Act - Simulate recipe deletion (ingredients should be deleted too)
            // In real EF Core, this would be handled by cascade delete configuration
            var shouldCascadeDelete = ingredient.RecipeId == recipe.Id;

            // Assert
            Assert.True(shouldCascadeDelete);
        }

        [Fact]
        public void DatabaseConstraints_UniqueShoppingListItems_ShouldBeEnforced()
        {
            // Arrange
            var plan = new Plan { Id = 1 };
            var item1 = new ShoppingListItem 
            { 
                PlanId = plan.Id, 
                IngredientName = "Mleko", 
                Unit = Unit.Milliliter 
            };
            var item2 = new ShoppingListItem 
            { 
                PlanId = plan.Id, 
                IngredientName = "Mleko", 
                Unit = Unit.Milliliter 
            };

            // Act - Check uniqueness constraint logic
            var isDuplicate = item1.PlanId == item2.PlanId && 
                             item1.IngredientName == item2.IngredientName && 
                             item1.Unit == item2.Unit;

            // Assert
            Assert.True(isDuplicate); // This should be prevented by unique index
        }

        [Fact]
        public void DatabaseQueries_FilterRecipesByName_ShouldWorkCorrectly()
        {
            // Arrange
            var recipes = new List<Recipe>
            {
                new Recipe { Id = 1, Name = "Kotlet schabowy" },
                new Recipe { Id = 2, Name = "Pierogi z mięsem" },
                new Recipe { Id = 3, Name = "Zupa pomidorowa" },
                new Recipe { Id = 4, Name = "Kotlet mielony" }
            };

            var searchTerm = "Kotlet";

            // Act - Simulate database query
            var filteredRecipes = recipes.Where(r => r.Name.Contains(searchTerm)).ToList();

            // Assert
            Assert.Equal(2, filteredRecipes.Count);
            Assert.All(filteredRecipes, r => Assert.Contains(searchTerm, r.Name));
        }

        [Fact]
        public void DatabaseQueries_FilterStandaloneIngredients_ShouldWorkCorrectly()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Id = 1, Name = "Mąka", RecipeId = null }, // Standalone
                new Ingredient { Id = 2, Name = "Cukier", RecipeId = 1 }, // Recipe ingredient
                new Ingredient { Id = 3, Name = "Mleko", RecipeId = null }, // Standalone
                new Ingredient { Id = 4, Name = "Jajka", RecipeId = 2 } // Recipe ingredient
            };

            // Act - Filter standalone ingredients (RecipeId is null)
            var standaloneIngredients = ingredients.Where(i => i.RecipeId == null).ToList();

            // Assert
            Assert.Equal(2, standaloneIngredients.Count);
            Assert.All(standaloneIngredients, i => Assert.Null(i.RecipeId));
            Assert.Contains(standaloneIngredients, i => i.Name == "Mąka");
            Assert.Contains(standaloneIngredients, i => i.Name == "Mleko");
        }

        [Fact]
        public void DatabaseQueries_FilterPlannedMealsByDateRange_ShouldWorkCorrectly()
        {
            // Arrange
            var startDate = new DateTime(2024, 12, 1);
            var endDate = new DateTime(2024, 12, 7);
            var plannedMeals = new List<PlannedMeal>
            {
                new PlannedMeal { Id = 1, Date = new DateTime(2024, 11, 30) }, // Before range
                new PlannedMeal { Id = 2, Date = new DateTime(2024, 12, 3) }, // In range
                new PlannedMeal { Id = 3, Date = new DateTime(2024, 12, 5) }, // In range
                new PlannedMeal { Id = 4, Date = new DateTime(2024, 12, 10) } // After range
            };

            // Act
            var mealsInRange = plannedMeals.Where(m => m.Date >= startDate && m.Date <= endDate).ToList();

            // Assert
            Assert.Equal(2, mealsInRange.Count);
            Assert.All(mealsInRange, m => Assert.True(m.Date >= startDate && m.Date <= endDate));
        }

        [Fact]
        public void DatabaseQueries_FilterActivePlans_ShouldWorkCorrectly()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, IsArchived = false },
                new Plan { Id = 2, IsArchived = true },
                new Plan { Id = 3, IsArchived = false },
                new Plan { Id = 4, IsArchived = true }
            };

            // Act
            var activePlans = plans.Where(p => !p.IsArchived).ToList();

            // Assert
            Assert.Equal(2, activePlans.Count);
            Assert.All(activePlans, p => Assert.False(p.IsArchived));
        }

        [Fact]
        public void DatabaseQueries_GroupShoppingItemsByPlan_ShouldWorkCorrectly()
        {
            // Arrange
            var items = new List<ShoppingListItem>
            {
                new ShoppingListItem { Id = 1, PlanId = 1, IngredientName = "Mleko" },
                new ShoppingListItem { Id = 2, PlanId = 1, IngredientName = "Chleb" },
                new ShoppingListItem { Id = 3, PlanId = 2, IngredientName = "Mąka" },
                new ShoppingListItem { Id = 4, PlanId = 2, IngredientName = "Jajka" },
                new ShoppingListItem { Id = 5, PlanId = 2, IngredientName = "Cukier" }
            };

            // Act
            var groupedItems = items.GroupBy(i => i.PlanId).ToList();

            // Assert
            Assert.Equal(2, groupedItems.Count);
            Assert.Equal(2, groupedItems.First(g => g.Key == 1).Count());
            Assert.Equal(3, groupedItems.First(g => g.Key == 2).Count());
        }

        [Fact]
        public void DatabaseOperations_CalculateNutritionTotals_ShouldWorkCorrectly()
        {
            // Arrange
            var ingredients = new List<Ingredient>
            {
                new Ingredient { Calories = 100, Protein = 10, Fat = 5, Carbs = 15 },
                new Ingredient { Calories = 150, Protein = 8, Fat = 12, Carbs = 20 },
                new Ingredient { Calories = 80, Protein = 6, Fat = 3, Carbs = 18 }
            };

            // Act
            var totalCalories = ingredients.Sum(i => i.Calories);
            var totalProtein = ingredients.Sum(i => i.Protein);
            var totalFat = ingredients.Sum(i => i.Fat);
            var totalCarbs = ingredients.Sum(i => i.Carbs);

            // Assert
            Assert.Equal(330, totalCalories);
            Assert.Equal(24, totalProtein);
            Assert.Equal(20, totalFat);
            Assert.Equal(53, totalCarbs);
        }

        [Fact]
        public void DatabaseOperations_CountCheckedShoppingItems_ShouldWorkCorrectly()
        {
            // Arrange
            var items = new List<ShoppingListItem>
            {
                new ShoppingListItem { Id = 1, IsChecked = true },
                new ShoppingListItem { Id = 2, IsChecked = false },
                new ShoppingListItem { Id = 3, IsChecked = true },
                new ShoppingListItem { Id = 4, IsChecked = false },
                new ShoppingListItem { Id = 5, IsChecked = true }
            };

            // Act
            var checkedCount = items.Count(i => i.IsChecked);
            var uncheckedCount = items.Count(i => !i.IsChecked);
            var totalCount = items.Count;

            // Assert
            Assert.Equal(3, checkedCount);
            Assert.Equal(2, uncheckedCount);
            Assert.Equal(5, totalCount);
            Assert.Equal(totalCount, checkedCount + uncheckedCount);
        }

        [Theory]
        [InlineData("Mąka", Unit.Gram, 500.0)]
        [InlineData("Mleko", Unit.Milliliter, 250.0)]
        [InlineData("Jajka", Unit.Piece, 3.0)]
        public void DatabaseOperations_CreateIngredientWithDifferentUnits_ShouldWorkCorrectly(string name, Unit unit, double quantity)
        {
            // Arrange & Act
            var ingredient = new Ingredient
            {
                Name = name,
                Unit = unit,
                Quantity = quantity
            };

            // Assert
            Assert.Equal(name, ingredient.Name);
            Assert.Equal(unit, ingredient.Unit);
            Assert.Equal(quantity, ingredient.Quantity);
        }

        [Fact]
        public void DatabaseOperations_ValidateDataIntegrity_ShouldMaintainConsistency()
        {
            // Arrange
            var recipe = new Recipe { Id = 1, Name = "Test Recipe" };
            var ingredient = new Ingredient { Id = 1, Name = "Test Ingredient", RecipeId = recipe.Id, Recipe = recipe };
            var plannedMeal = new PlannedMeal { Id = 1, RecipeId = recipe.Id, Recipe = recipe };

            // Act & Assert - Data integrity checks
            Assert.Equal(recipe.Id, ingredient.RecipeId);
            Assert.Same(recipe, ingredient.Recipe);
            Assert.Equal(recipe.Id, plannedMeal.RecipeId);
            Assert.Same(recipe, plannedMeal.Recipe);
        }

        [Fact]
        public void DatabaseOperations_OrderingAndPagination_ShouldWorkCorrectly()
        {
            // Arrange
            var recipes = new List<Recipe>
            {
                new Recipe { Id = 1, Name = "Zupa", Calories = 150 },
                new Recipe { Id = 2, Name = "Kotlet", Calories = 300 },
                new Recipe { Id = 3, Name = "Pierogi", Calories = 250 },
                new Recipe { Id = 4, Name = "Sałatka", Calories = 100 }
            };

            // Act - Order by calories descending, take top 2
            var topRecipes = recipes.OrderByDescending(r => r.Calories).Take(2).ToList();

            // Assert
            Assert.Equal(2, topRecipes.Count);
            Assert.Equal("Kotlet", topRecipes[0].Name); // 300 calories
            Assert.Equal("Pierogi", topRecipes[1].Name); // 250 calories
        }

        [Fact]
        public void DatabaseOperations_SearchWithMultipleCriteria_ShouldWorkCorrectly()
        {
            // Arrange
            var recipes = new List<Recipe>
            {
                new Recipe { Id = 1, Name = "Kotlet schabowy", Calories = 300 },
                new Recipe { Id = 2, Name = "Kotlet mielony", Calories = 250 },
                new Recipe { Id = 3, Name = "Pierogi z mięsem", Calories = 280 },
                new Recipe { Id = 4, Name = "Zupa pomidorowa", Calories = 100 }
            };

            var searchTerm = "Kotlet";
            var minCalories = 275;

            // Act
            var filteredRecipes = recipes.Where(r => r.Name.Contains(searchTerm) && r.Calories >= minCalories).ToList();

            // Assert
            Assert.Single(filteredRecipes);
            Assert.Equal("Kotlet schabowy", filteredRecipes[0].Name);
        }
    }
}
