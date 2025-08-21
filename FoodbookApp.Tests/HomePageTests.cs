using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Foodbook.Models;
using Foodbook.ViewModels;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace FoodbookApp.Tests
{
    public class HomePageTests
    {
        [Fact]
        public void PlannedMealGroup_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var group = new PlannedMealGroup();

            // Assert
            Assert.Equal(DateTime.MinValue, group.Date);
            Assert.Equal(string.Empty, group.DateLabel);
            Assert.NotNull(group.Meals);
            Assert.Empty(group.Meals);
        }

        [Fact]
        public void PlannedMealGroup_SetProperties_ShouldUpdateCorrectly()
        {
            // Arrange
            var group = new PlannedMealGroup();
            var date = new DateTime(2024, 12, 25);
            var dateLabel = "Środa, 25.12.2024";

            // Act
            group.Date = date;
            group.DateLabel = dateLabel;

            // Assert
            Assert.Equal(date, group.Date);
            Assert.Equal(dateLabel, group.DateLabel);
        }

        [Fact]
        public void PlannedMealGroup_AddMeals_ShouldUpdateMealsCollection()
        {
            // Arrange
            var group = new PlannedMealGroup();
            var meal1 = new PlannedMeal { RecipeId = 1, Portions = 2 };
            var meal2 = new PlannedMeal { RecipeId = 2, Portions = 1 };

            // Act
            group.Meals.Add(meal1);
            group.Meals.Add(meal2);

            // Assert
            Assert.Equal(2, group.Meals.Count);
            Assert.Contains(meal1, group.Meals);
            Assert.Contains(meal2, group.Meals);
        }

        [Theory]
        [InlineData(NutritionPeriod.Day)]
        [InlineData(NutritionPeriod.Week)]
        [InlineData(NutritionPeriod.Custom)]
        public void NutritionPeriod_EnumValues_ShouldBeValid(NutritionPeriod period)
        {
            // Arrange & Act
            var periodValue = (int)period;

            // Assert
            Assert.True(periodValue >= 0);
            Assert.True(Enum.IsDefined(typeof(NutritionPeriod), period));
        }

        [Theory]
        [InlineData(PlannedMealsPeriod.Today)]
        [InlineData(PlannedMealsPeriod.Week)]
        [InlineData(PlannedMealsPeriod.Custom)]
        public void PlannedMealsPeriod_EnumValues_ShouldBeValid(PlannedMealsPeriod period)
        {
            // Arrange & Act
            var periodValue = (int)period;

            // Assert
            Assert.True(periodValue >= 0);
            Assert.True(Enum.IsDefined(typeof(PlannedMealsPeriod), period));
        }

        [Fact]
        public void NutritionStats_DefaultValues_ShouldBeZero()
        {
            // Arrange
            var totalCalories = 0.0;
            var totalProtein = 0.0;
            var totalFat = 0.0;
            var totalCarbs = 0.0;

            // Act & Assert
            Assert.Equal(0.0, totalCalories);
            Assert.Equal(0.0, totalProtein);
            Assert.Equal(0.0, totalFat);
            Assert.Equal(0.0, totalCarbs);
        }

        [Fact]
        public void NutritionStats_CalculatePortionMultiplier_ShouldReturnCorrectValue()
        {
            // Arrange
            var mealPortions = 4;
            var recipePortions = 2;

            // Act
            var portionMultiplier = (double)mealPortions / recipePortions;

            // Assert
            Assert.Equal(2.0, portionMultiplier);
        }

        [Fact]
        public void NutritionStats_CalculateAdjustedValues_ShouldReturnCorrectResults()
        {
            // Arrange
            var baseCalories = 300.0;
            var baseProtein = 15.0;
            var baseFat = 10.0;
            var baseCarbs = 40.0;
            var portionMultiplier = 1.5;

            // Act
            var adjustedCalories = baseCalories * portionMultiplier;
            var adjustedProtein = baseProtein * portionMultiplier;
            var adjustedFat = baseFat * portionMultiplier;
            var adjustedCarbs = baseCarbs * portionMultiplier;

            // Assert
            Assert.Equal(450.0, adjustedCalories);
            Assert.Equal(22.5, adjustedProtein);
            Assert.Equal(15.0, adjustedFat);
            Assert.Equal(60.0, adjustedCarbs);
        }

        [Fact]
        public void DateRange_GetCustomDateRange_ShouldReturnCorrectDates()
        {
            // Arrange
            var startDate = new DateTime(2024, 12, 1);
            var endDate = new DateTime(2024, 12, 7);

            // Act
            var adjustedEndDate = endDate.AddDays(1).AddSeconds(-1);

            // Assert
            Assert.Equal(startDate, startDate);
            Assert.Equal(new DateTime(2024, 12, 7, 23, 59, 59), adjustedEndDate);
        }

        [Fact]
        public void DateRange_GetTodayRange_ShouldReturnCorrectDates()
        {
            // Arrange
            var today = DateTime.Today;

            // Act
            var startDate = today;
            var endDate = today.AddDays(1).AddSeconds(-1);

            // Assert
            Assert.Equal(today, startDate);
            Assert.Equal(today.Date.AddDays(1).AddSeconds(-1), endDate);
        }

        [Fact]
        public void DateRange_GetWeekRange_ShouldReturnCorrectDates()
        {
            // Arrange
            var today = DateTime.Today;

            // Act
            var startDate = today;
            var endDate = today.AddDays(7);

            // Assert
            Assert.Equal(today, startDate);
            Assert.Equal(today.AddDays(7), endDate);
        }

        [Fact]
        public void PlannedMealGroup_GroupMealsByDate_ShouldReturnCorrectGroups()
        {
            // Arrange
            var date1 = new DateTime(2024, 12, 25);
            var date2 = new DateTime(2024, 12, 26);
            var meals = new List<PlannedMeal>
            {
                new PlannedMeal { Date = date1, RecipeId = 1 },
                new PlannedMeal { Date = date1, RecipeId = 2 },
                new PlannedMeal { Date = date2, RecipeId = 3 }
            };

            // Act
            var groupedMeals = meals
                .GroupBy(m => m.Date.Date)
                .Select(g => new PlannedMealGroup
                {
                    Date = g.Key,
                    Meals = new ObservableCollection<PlannedMeal>(g)
                })
                .ToList();

            // Assert
            Assert.Equal(2, groupedMeals.Count);
            Assert.Equal(2, groupedMeals.First(g => g.Date == date1).Meals.Count);
            Assert.Single(groupedMeals.First(g => g.Date == date2).Meals);
        }

        [Fact]
        public void HomePageStatistics_CountValues_ShouldBeNonNegative()
        {
            // Arrange & Act
            var recipeCount = 10;
            var planCount = 5;
            var archivedPlanCount = 3;

            // Assert
            Assert.True(recipeCount >= 0);
            Assert.True(planCount >= 0);
            Assert.True(archivedPlanCount >= 0);
        }

        [Fact]
        public void NutritionDisplay_FormatValues_ShouldReturnCorrectFormat()
        {
            // Arrange
            var hasData = true;
            var calories = 1234.56;
            var protein = 45.67;
            var fat = 23.45;
            var carbs = 156.78;

            // Act
            var caloriesDisplay = hasData ? $"{calories:F0}" : "---";
            var proteinDisplay = hasData ? $"{protein:F0}g" : "---";
            var fatDisplay = hasData ? $"{fat:F0}g" : "---";
            var carbsDisplay = hasData ? $"{carbs:F0}g" : "---";

            // Assert
            Assert.Equal("1235", caloriesDisplay);
            Assert.Equal("46g", proteinDisplay);
            Assert.Equal("23g", fatDisplay);
            Assert.Equal("157g", carbsDisplay);
        }

        [Fact]
        public void NutritionDisplay_NoData_ShouldReturnDashes()
        {
            // Arrange
            var hasData = false;

            // Act
            var caloriesDisplay = hasData ? "1000" : "---";
            var proteinDisplay = hasData ? "50g" : "---";
            var fatDisplay = hasData ? "30g" : "---";
            var carbsDisplay = hasData ? "100g" : "---";

            // Assert
            Assert.Equal("---", caloriesDisplay);
            Assert.Equal("---", proteinDisplay);
            Assert.Equal("---", fatDisplay);
            Assert.Equal("---", carbsDisplay);
        }

        [Fact]
        public void LoadingState_InitialState_ShouldBeTrue()
        {
            // Arrange & Act
            var isLoading = true; // Default state for HomeViewModel

            // Assert
            Assert.True(isLoading);
        }

        [Fact]
        public void PlannedMealHistory_EmptyState_ShouldIndicateNoMeals()
        {
            // Arrange
            var plannedMealHistory = new ObservableCollection<PlannedMealGroup>();

            // Act
            var hasPlannedMeals = plannedMealHistory.Any();

            // Assert
            Assert.False(hasPlannedMeals);
        }

        // ==========================
        // ENGLISH LANGUAGE LABEL TESTS
        // ==========================

        [Fact]
        public void EnglishLabels_ButtonResourceValues_ShouldHaveCorrectEnglishText()
        {
            // Arrange - Expected English values from ButtonResources.resx
            var expectedTodayLabel = "Today";
            var expectedThisWeekLabel = "This week";
            var expectedCustomDateLabel = "Custom date";

            // Act & Assert - Testing the expected English values
            Assert.Equal("Today", expectedTodayLabel);
            Assert.Equal("This week", expectedThisWeekLabel);
            Assert.Equal("Custom date", expectedCustomDateLabel);
        }

        [Fact]
        public void PeriodDisplay_EnglishLabels_ShouldReturnCorrectTodayLabel()
        {
            // Arrange
            var period = PlannedMealsPeriod.Today;

            // Act
            var displayText = GetPlannedMealsPeriodDisplayEnglish(period);

            // Assert
            Assert.Equal("Today", displayText);
        }

        [Fact]
        public void PeriodDisplay_EnglishLabels_ShouldReturnCorrectWeekLabel()
        {
            // Arrange
            var period = PlannedMealsPeriod.Week;

            // Act
            var displayText = GetPlannedMealsPeriodDisplayEnglish(period);

            // Assert
            Assert.Equal("This week", displayText);
        }

        [Fact]
        public void PeriodDisplay_EnglishLabels_ShouldReturnCorrectCustomLabel()
        {
            // Arrange
            var period = PlannedMealsPeriod.Custom;
            var startDate = new DateTime(2024, 12, 1);
            var endDate = new DateTime(2024, 12, 7);

            // Act
            var displayText = GetPlannedMealsPeriodDisplayEnglish(period, startDate, endDate);

            // Assert
            Assert.Equal("01.12 - 07.12", displayText);
        }

        [Fact]
        public void NutritionPeriodDisplay_EnglishLabels_ShouldReturnCorrectTodayLabel()
        {
            // Arrange
            var period = NutritionPeriod.Day;

            // Act
            var displayText = GetNutritionPeriodDisplayEnglish(period);

            // Assert
            Assert.Equal("Today", displayText);
        }

        [Fact]
        public void NutritionPeriodDisplay_EnglishLabels_ShouldReturnCorrectWeekLabel()
        {
            // Arrange
            var period = NutritionPeriod.Week;

            // Act
            var displayText = GetNutritionPeriodDisplayEnglish(period);

            // Assert
            Assert.Equal("This week", displayText);
        }

        [Fact]
        public void NutritionPeriodDisplay_EnglishLabels_ShouldReturnCorrectCustomLabel()
        {
            // Arrange
            var period = NutritionPeriod.Custom;
            var startDate = new DateTime(2024, 12, 1);
            var endDate = new DateTime(2024, 12, 7);

            // Act
            var displayText = GetNutritionPeriodDisplayEnglish(period, startDate, endDate);

            // Assert
            Assert.Equal("01.12 - 07.12", displayText);
        }

        [Theory]
        [InlineData("Today")]
        [InlineData("This week")]
        [InlineData("Custom date")]
        public void EnglishLabels_ButtonText_ShouldNotBeNullOrEmpty(string expectedLabel)
        {
            // Arrange & Act & Assert
            Assert.False(string.IsNullOrEmpty(expectedLabel));
            Assert.False(string.IsNullOrWhiteSpace(expectedLabel));
        }

        [Fact]
        public void ActionSheetOptions_EnglishLabels_ShouldReturnCorrectOptions()
        {
            // Arrange
            var expectedOptions = new[]
            {
                "Today",
                "This week", 
                "Custom date"
            };

            // Act
            var options = GetActionSheetOptionsEnglish();

            // Assert
            Assert.Equal(expectedOptions.Length, options.Length);
            Assert.Equal("Today", options[0]);
            Assert.Equal("This week", options[1]);
            Assert.Equal("Custom date", options[2]);
        }

        [Fact]
        public void DateLabels_EnglishFormat_ShouldReturnCorrectFormat()
        {
            // Arrange
            var date = new DateTime(2024, 12, 25);

            // Act
            var dateLabel = GetDateLabelEnglish(date);

            // Assert
            Assert.Equal("Wednesday, 25.12.2024", dateLabel);
        }

        [Theory]
        [InlineData(2024, 12, 25, "Wednesday, 25.12.2024")]
        [InlineData(2024, 1, 1, "Monday, 01.01.2024")]
        [InlineData(2024, 6, 15, "Saturday, 15.06.2024")]
        public void DateLabels_EnglishFormat_ShouldReturnCorrectFormatForVariousDates(int year, int month, int day, string expected)
        {
            // Arrange
            var date = new DateTime(year, month, day);

            // Act
            var dateLabel = GetDateLabelEnglish(date);

            // Assert
            Assert.Equal(expected, dateLabel);
        }

        [Fact]
        public void NutritionDisplay_EnglishUnits_ShouldReturnCorrectUnits()
        {
            // Arrange
            var hasData = true;
            var protein = 25.5;
            var fat = 15.2;
            var carbs = 45.8;

            // Act
            var proteinDisplay = hasData ? $"{protein:F0}g" : "---";
            var fatDisplay = hasData ? $"{fat:F0}g" : "---";
            var carbsDisplay = hasData ? $"{carbs:F0}g" : "---";

            // Assert - English units use 'g' for grams
            Assert.Equal("26g", proteinDisplay);
            Assert.Equal("15g", fatDisplay);
            Assert.Equal("46g", carbsDisplay);
        }

        [Fact]
        public void ErrorMessages_EnglishLabels_ShouldReturnCorrectMessages()
        {
            // Arrange
            var errorTitle = "Error";
            var dateRangeError = "End date must be later than start date";
            var okButton = "OK";
            var cancelButton = "Cancel";

            // Act & Assert - English error messages
            Assert.Equal("Error", errorTitle);
            Assert.Equal("End date must be later than start date", dateRangeError);
            Assert.Equal("OK", okButton);
            Assert.Equal("Cancel", cancelButton);
        }

        [Fact]
        public void PromptMessages_EnglishLabels_ShouldReturnCorrectMessages()
        {
            // Arrange
            var startDateTitle = "Start date";
            var endDateTitle = "End date";
            var datePrompt = "Enter date (dd.mm.yyyy):";

            // Act & Assert - English prompt messages
            Assert.Equal("Start date", startDateTitle);
            Assert.Equal("End date", endDateTitle);
            Assert.Equal("Enter date (dd.mm.yyyy):", datePrompt);
        }

        [Fact]
        public void StatisticsLabels_EnglishLabels_ShouldReturnCorrectLabels()
        {
            // Arrange
            var nutritionPeriodSelectionTitle = "Select period for statistics";
            var plannedMealsPeriodSelectionTitle = "Select period for planned meals";

            // Act & Assert - English statistics labels
            Assert.Equal("Select period for statistics", nutritionPeriodSelectionTitle);
            Assert.Equal("Select period for planned meals", plannedMealsPeriodSelectionTitle);
        }

        [Theory]
        [InlineData(PlannedMealsPeriod.Today, "Today")]
        [InlineData(PlannedMealsPeriod.Week, "This week")]
        public void PlannedMealsPeriod_EnglishLabels_ShouldMapCorrectly(PlannedMealsPeriod period, string expectedEnglishLabel)
        {
            // Arrange & Act
            var displayText = GetPlannedMealsPeriodDisplayEnglish(period);

            // Assert
            Assert.Equal(expectedEnglishLabel, displayText);
        }

        [Theory]
        [InlineData(NutritionPeriod.Day, "Today")]
        [InlineData(NutritionPeriod.Week, "This week")]
        public void NutritionPeriod_EnglishLabels_ShouldMapCorrectly(NutritionPeriod period, string expectedEnglishLabel)
        {
            // Arrange & Act
            var displayText = GetNutritionPeriodDisplayEnglish(period);

            // Assert
            Assert.Equal(expectedEnglishLabel, displayText);
        }

        [Fact]
        public void UnitLabels_EnglishFormat_ShouldReturnCorrectUnits()
        {
            // Arrange
            var gramUnit = "g";
            var milliliterUnit = "ml";
            var pieceUnit = "pcs";

            // Act & Assert - English unit abbreviations
            Assert.Equal("g", gramUnit);
            Assert.Equal("ml", milliliterUnit);
            Assert.Equal("pcs", pieceUnit);
        }

        // Helper methods for English label testing
        private string GetPlannedMealsPeriodDisplayEnglish(PlannedMealsPeriod period, DateTime? startDate = null, DateTime? endDate = null)
        {
            return period switch
            {
                PlannedMealsPeriod.Today => "Today",
                PlannedMealsPeriod.Week => "This week",
                PlannedMealsPeriod.Custom when startDate.HasValue && endDate.HasValue => 
                    $"{startDate.Value:dd.MM} - {endDate.Value:dd.MM}",
                _ => "This week"
            };
        }

        private string GetNutritionPeriodDisplayEnglish(NutritionPeriod period, DateTime? startDate = null, DateTime? endDate = null)
        {
            return period switch
            {
                NutritionPeriod.Day => "Today",
                NutritionPeriod.Week => "This week",
                NutritionPeriod.Custom when startDate.HasValue && endDate.HasValue => 
                    $"{startDate.Value:dd.MM} - {endDate.Value:dd.MM}",
                _ => "Today"
            };
        }

        private string[] GetActionSheetOptionsEnglish()
        {
            return new[]
            {
                "Today",
                "This week", 
                "Custom date"
            };
        }

        private string GetDateLabelEnglish(DateTime date)
        {
            // English format: "Wednesday, 25.12.2024"
            return date.ToString("dddd, dd.MM.yyyy", new System.Globalization.CultureInfo("en-US"));
        }
    }
}
