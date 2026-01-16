using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Foodbook.Data.Migrations
{
    public partial class AddUpdatedAtColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add UpdatedAt columns to tables synced with Supabase
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Recipes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Ingredients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RecipeLabels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Folders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PlannedMeals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ShoppingListItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Plans",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Recipes");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Ingredients");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "RecipeLabels");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Folders");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "PlannedMeals");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "ShoppingListItems");
            migrationBuilder.DropColumn(name: "UpdatedAt", table: "Plans");
        }
    }
}
