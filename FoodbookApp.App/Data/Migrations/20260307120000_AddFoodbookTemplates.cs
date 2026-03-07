using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Migrations
{
    public partial class AddFoodbookTemplates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FoodbookTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DurationDays = table.Column<int>(type: "INTEGER", nullable: false),
                    MealsPerDay = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FoodbookTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateMeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FoodbookTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DayOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Portions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateMeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateMeals_FoodbookTemplates_FoodbookTemplateId",
                        column: x => x.FoodbookTemplateId,
                        principalTable: "FoodbookTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TemplateMeals_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FoodbookTemplates_UserId_CreatedAt",
                table: "FoodbookTemplates",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateMeals_RecipeId",
                table: "TemplateMeals",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateMeals_Template_Day_Slot",
                table: "TemplateMeals",
                columns: new[] { "FoodbookTemplateId", "DayOffset", "SlotIndex" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateMeals");

            migrationBuilder.DropTable(
                name: "FoodbookTemplates");
        }
    }
}
