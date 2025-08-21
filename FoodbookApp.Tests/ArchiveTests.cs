using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Foodbook.Models;
using Foodbook.ViewModels;
using System.ComponentModel;

namespace FoodbookApp.Tests
{
    public class ArchiveTests
    {
        [Fact]
        public void Plan_IsArchived_DefaultValue_ShouldBeFalse()
        {
            // Arrange & Act
            var plan = new Plan();

            // Assert
            Assert.False(plan.IsArchived);
        }

        [Fact]
        public void Plan_SetIsArchived_ShouldUpdateCorrectly()
        {
            // Arrange
            var plan = new Plan();

            // Act
            plan.IsArchived = true;

            // Assert
            Assert.True(plan.IsArchived);
        }

        [Fact]
        public void Plan_ArchiveAndRestore_ShouldToggleCorrectly()
        {
            // Arrange
            var plan = new Plan();

            // Act - Archive
            plan.IsArchived = true;

            // Assert - Archived
            Assert.True(plan.IsArchived);

            // Act - Restore
            plan.IsArchived = false;

            // Assert - Restored
            Assert.False(plan.IsArchived);
        }

        [Fact]
        public void Plan_ArchivedPlan_ShouldRetainAllProperties()
        {
            // Arrange
            var startDate = new DateTime(2024, 12, 1);
            var endDate = new DateTime(2024, 12, 7);
            var plan = new Plan
            {
                Id = 1,
                StartDate = startDate,
                EndDate = endDate
            };

            // Act
            plan.IsArchived = true;

            // Assert
            Assert.Equal(1, plan.Id);
            Assert.Equal(startDate, plan.StartDate);
            Assert.Equal(endDate, plan.EndDate);
            Assert.True(plan.IsArchived);
            Assert.Equal("Lista zakupów", plan.Label);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Plan_IsArchived_WhenSetToValue_ShouldRetainValue(bool isArchived)
        {
            // Arrange
            var plan = new Plan();

            // Act
            plan.IsArchived = isArchived;

            // Assert
            Assert.Equal(isArchived, plan.IsArchived);
        }

        [Fact]
        public void ArchiveOperations_FilterArchivedPlans_ShouldReturnOnlyArchivedPlans()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, StartDate = new DateTime(2024, 11, 1), EndDate = new DateTime(2024, 11, 7), IsArchived = false },
                new Plan { Id = 2, StartDate = new DateTime(2024, 11, 8), EndDate = new DateTime(2024, 11, 14), IsArchived = true },
                new Plan { Id = 3, StartDate = new DateTime(2024, 11, 15), EndDate = new DateTime(2024, 11, 21), IsArchived = true },
                new Plan { Id = 4, StartDate = new DateTime(2024, 11, 22), EndDate = new DateTime(2024, 11, 28), IsArchived = false }
            };

            // Act
            var archivedPlans = plans.Where(p => p.IsArchived).ToList();

            // Assert
            Assert.Equal(2, archivedPlans.Count);
            Assert.All(archivedPlans, plan => Assert.True(plan.IsArchived));
            Assert.Contains(archivedPlans, p => p.Id == 2);
            Assert.Contains(archivedPlans, p => p.Id == 3);
        }

        [Fact]
        public void ArchiveOperations_FilterActivePlans_ShouldReturnOnlyActivePlans()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, StartDate = new DateTime(2024, 11, 1), EndDate = new DateTime(2024, 11, 7), IsArchived = false },
                new Plan { Id = 2, StartDate = new DateTime(2024, 11, 8), EndDate = new DateTime(2024, 11, 14), IsArchived = true },
                new Plan { Id = 3, StartDate = new DateTime(2024, 11, 15), EndDate = new DateTime(2024, 11, 21), IsArchived = false }
            };

            // Act
            var activePlans = plans.Where(p => !p.IsArchived).ToList();

            // Assert
            Assert.Equal(2, activePlans.Count);
            Assert.All(activePlans, plan => Assert.False(plan.IsArchived));
            Assert.Contains(activePlans, p => p.Id == 1);
            Assert.Contains(activePlans, p => p.Id == 3);
        }

        [Fact]
        public void ArchiveOperations_SortArchivedPlansByDate_ShouldOrderByStartDateDescending()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, StartDate = new DateTime(2024, 10, 1), IsArchived = true },
                new Plan { Id = 2, StartDate = new DateTime(2024, 12, 1), IsArchived = true },
                new Plan { Id = 3, StartDate = new DateTime(2024, 11, 1), IsArchived = true }
            };

            // Act
            var sortedPlans = plans.Where(p => p.IsArchived)
                                  .OrderByDescending(p => p.StartDate)
                                  .ToList();

            // Assert
            Assert.Equal(3, sortedPlans.Count);
            Assert.Equal(2, sortedPlans[0].Id); // December 2024
            Assert.Equal(3, sortedPlans[1].Id); // November 2024
            Assert.Equal(1, sortedPlans[2].Id); // October 2024
        }

        [Fact]
        public void ArchiveOperations_CheckDateRangeOverlap_ShouldIgnoreArchivedPlans()
        {
            // Arrange
            var existingPlans = new List<Plan>
            {
                new Plan { Id = 1, StartDate = new DateTime(2024, 12, 1), EndDate = new DateTime(2024, 12, 7), IsArchived = false },
                new Plan { Id = 2, StartDate = new DateTime(2024, 12, 5), EndDate = new DateTime(2024, 12, 12), IsArchived = true }, // Archived - should be ignored
                new Plan { Id = 3, StartDate = new DateTime(2024, 12, 15), EndDate = new DateTime(2024, 12, 21), IsArchived = false }
            };

            var newPlanStart = new DateTime(2024, 12, 5);
            var newPlanEnd = new DateTime(2024, 12, 10);

            // Act - Check overlap only with active plans (ignoring archived)
            var hasOverlapWithActive = existingPlans
                .Where(p => !p.IsArchived) // Ignore archived plans
                .Any(p => p.StartDate <= newPlanEnd && p.EndDate >= newPlanStart);

            // Assert
            Assert.True(hasOverlapWithActive); // Should overlap with plan Id=1, but not with archived plan Id=2
        }

        [Fact]
        public void ArchiveOperations_RestorePlanWithoutConflict_ShouldBeAllowed()
        {
            // Arrange
            var existingActivePlans = new List<Plan>
            {
                new Plan { Id = 1, StartDate = new DateTime(2024, 12, 1), EndDate = new DateTime(2024, 12, 7), IsArchived = false },
                new Plan { Id = 2, StartDate = new DateTime(2024, 12, 15), EndDate = new DateTime(2024, 12, 21), IsArchived = false }
            };

            var archivedPlan = new Plan 
            { 
                Id = 3, 
                StartDate = new DateTime(2024, 12, 8), 
                EndDate = new DateTime(2024, 12, 14), 
                IsArchived = true 
            };

            // Act - Check if archived plan can be restored without conflict
            var hasConflict = existingActivePlans
                .Any(p => !p.IsArchived && p.Id != archivedPlan.Id && 
                         p.StartDate <= archivedPlan.EndDate && p.EndDate >= archivedPlan.StartDate);

            // Assert
            Assert.False(hasConflict); // No conflict, restoration should be allowed
        }

        [Fact]
        public void ArchiveOperations_RestorePlanWithConflict_ShouldBeBlocked()
        {
            // Arrange
            var existingActivePlans = new List<Plan>
            {
                new Plan { Id = 1, StartDate = new DateTime(2024, 12, 1), EndDate = new DateTime(2024, 12, 7), IsArchived = false },
                new Plan { Id = 2, StartDate = new DateTime(2024, 12, 10), EndDate = new DateTime(2024, 12, 16), IsArchived = false }
            };

            var archivedPlan = new Plan 
            { 
                Id = 3, 
                StartDate = new DateTime(2024, 12, 5), 
                EndDate = new DateTime(2024, 12, 12), 
                IsArchived = true 
            };

            // Act - Check if archived plan can be restored (would conflict with both active plans)
            var hasConflict = existingActivePlans
                .Any(p => !p.IsArchived && p.Id != archivedPlan.Id && 
                         p.StartDate <= archivedPlan.EndDate && p.EndDate >= archivedPlan.StartDate);

            // Assert
            Assert.True(hasConflict); // Conflict exists, restoration should be blocked
        }

        [Fact]
        public void ArchiveOperations_EmptyArchive_ShouldReturnEmptyCollection()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, IsArchived = false },
                new Plan { Id = 2, IsArchived = false },
                new Plan { Id = 3, IsArchived = false }
            };

            // Act
            var archivedPlans = plans.Where(p => p.IsArchived).ToList();

            // Assert
            Assert.Empty(archivedPlans);
        }

        [Fact]
        public void ArchiveOperations_AllPlansArchived_ShouldReturnAllPlans()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, IsArchived = true },
                new Plan { Id = 2, IsArchived = true },
                new Plan { Id = 3, IsArchived = true }
            };

            // Act
            var archivedPlans = plans.Where(p => p.IsArchived).ToList();

            // Assert
            Assert.Equal(3, archivedPlans.Count);
            Assert.All(archivedPlans, plan => Assert.True(plan.IsArchived));
        }

        [Fact]
        public void ArchiveOperations_CountArchivedVsActive_ShouldReturnCorrectCounts()
        {
            // Arrange
            var plans = new List<Plan>
            {
                new Plan { Id = 1, IsArchived = false },
                new Plan { Id = 2, IsArchived = true },
                new Plan { Id = 3, IsArchived = false },
                new Plan { Id = 4, IsArchived = true },
                new Plan { Id = 5, IsArchived = true }
            };

            // Act
            var archivedCount = plans.Count(p => p.IsArchived);
            var activeCount = plans.Count(p => !p.IsArchived);
            var totalCount = plans.Count;

            // Assert
            Assert.Equal(3, archivedCount);
            Assert.Equal(2, activeCount);
            Assert.Equal(5, totalCount);
            Assert.Equal(totalCount, archivedCount + activeCount);
        }

        [Theory]
        [InlineData("2024-01-01", "2024-01-07")]
        [InlineData("2024-06-15", "2024-06-22")]
        [InlineData("2024-12-25", "2024-12-31")]
        public void Plan_ArchiveWithDifferentDateRanges_ShouldRetainDates(string startDateStr, string endDateStr)
        {
            // Arrange
            var startDate = DateTime.Parse(startDateStr);
            var endDate = DateTime.Parse(endDateStr);
            var plan = new Plan
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Act
            plan.IsArchived = true;

            // Assert
            Assert.True(plan.IsArchived);
            Assert.Equal(startDate, plan.StartDate);
            Assert.Equal(endDate, plan.EndDate);
        }

        [Fact]
        public void ArchiveOperations_PlanDateRangeValidation_ShouldWorkCorrectly()
        {
            // Arrange
            var plan = new Plan
            {
                StartDate = new DateTime(2024, 12, 1),
                EndDate = new DateTime(2024, 12, 7)
            };

            // Act
            var isValidRange = plan.StartDate <= plan.EndDate;
            var daysDifference = (plan.EndDate - plan.StartDate).Days;

            // Assert
            Assert.True(isValidRange);
            Assert.Equal(6, daysDifference);
        }

        [Fact]
        public void ArchiveOperations_PlanLabel_ShouldRemainConstant()
        {
            // Arrange
            var plan = new Plan();

            // Act
            plan.IsArchived = true;
            var labelBeforeArchive = plan.Label;
            plan.IsArchived = false;
            var labelAfterRestore = plan.Label;

            // Assert
            Assert.Equal("Lista zakupów", labelBeforeArchive);
            Assert.Equal("Lista zakupów", labelAfterRestore);
            Assert.Equal(labelBeforeArchive, labelAfterRestore);
        }
    }
}
