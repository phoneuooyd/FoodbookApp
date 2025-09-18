using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class Baseline2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBeingDragged",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "IsBeingDraggedOver",
                table: "Recipes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBeingDragged",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBeingDraggedOver",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
