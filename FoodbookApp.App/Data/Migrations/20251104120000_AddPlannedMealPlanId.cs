using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedMealPlanId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanId",
                table: "PlannedMeals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannedMeals_PlanId",
                table: "PlannedMeals",
                column: "PlanId");

            // Best-effort backfill: attach existing meals to the most recent active Planner plan that covers the meal date
            migrationBuilder.Sql(@"
                UPDATE PlannedMeals
                SET PlanId = (
                    SELECT Id FROM Plans p
                    WHERE p.Type = 0 /* Planner */ AND p.IsArchived = 0 AND PlannedMeals.Date BETWEEN p.StartDate AND p.EndDate
                    ORDER BY p.StartDate DESC
                    LIMIT 1
                )
                WHERE PlanId IS NULL;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannedMeals_Plans_PlanId",
                table: "PlannedMeals",
                column: "PlanId",
                principalTable: "Plans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannedMeals_Plans_PlanId",
                table: "PlannedMeals");

            migrationBuilder.DropIndex(
                name: "IX_PlannedMeals_PlanId",
                table: "PlannedMeals");

            migrationBuilder.DropColumn(
                name: "PlanId",
                table: "PlannedMeals");
        }
    }
}
