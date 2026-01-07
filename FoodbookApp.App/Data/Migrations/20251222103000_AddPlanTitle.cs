using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Plans",
                type: "TEXT",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Plans");
        }
    }
}
