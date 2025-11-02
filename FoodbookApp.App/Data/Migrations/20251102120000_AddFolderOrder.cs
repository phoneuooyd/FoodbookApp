using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Data.Migrations
{
    public partial class AddFolderOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Order column with default 0 to existing Folders table
            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Folders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // If desired, create an index to speed sibling ordering queries
            migrationBuilder.CreateIndex(
                name: "IX_Folders_Order",
                table: "Folders",
                column: "Order");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Folders_Order",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Folders");
        }
    }
}
