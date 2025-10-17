using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeLabels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ColorHex = table.Column<string>(type: "TEXT", maxLength: 9, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeLabels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLabels_Name",
                table: "RecipeLabels",
                column: "Name");

            migrationBuilder.CreateTable(
                name: "RecipeRecipeLabel",
                columns: table => new
                {
                    RecipesId = table.Column<int>(type: "INTEGER", nullable: false),
                    LabelsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeRecipeLabel", x => new { x.RecipesId, x.LabelsId });
                    table.ForeignKey(
                        name: "FK_RecipeRecipeLabel_RecipeLabels_LabelsId",
                        column: x => x.LabelsId,
                        principalTable: "RecipeLabels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeRecipeLabel_Recipes_RecipesId",
                        column: x => x.RecipesId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeRecipeLabel_LabelsId",
                table: "RecipeRecipeLabel",
                column: "LabelsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeRecipeLabel");

            migrationBuilder.DropTable(
                name: "RecipeLabels");
        }
    }
}
