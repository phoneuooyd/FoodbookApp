using Foodbook.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260719120000_AddGooglePlayBillingFields")]
    public partial class AddGooglePlayBillingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentProvider",
                table: "SubscriptionOperations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderProductId",
                table: "SubscriptionOperations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurchaseToken",
                table: "SubscriptionOperations",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseTimeUtc",
                table: "SubscriptionOperations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryTimeUtc",
                table: "SubscriptionOperations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAcknowledged",
                table: "SubscriptionOperations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastProviderStatus",
                table: "SubscriptionOperations",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PaymentProvider", table: "SubscriptionOperations");
            migrationBuilder.DropColumn(name: "ProviderProductId", table: "SubscriptionOperations");
            migrationBuilder.DropColumn(name: "PurchaseToken", table: "SubscriptionOperations");
            migrationBuilder.DropColumn(name: "PurchaseTimeUtc", table: "SubscriptionOperations");
            migrationBuilder.DropColumn(name: "ExpiryTimeUtc", table: "SubscriptionOperations");
            migrationBuilder.DropColumn(name: "IsAcknowledged", table: "SubscriptionOperations");
            migrationBuilder.DropColumn(name: "LastProviderStatus", table: "SubscriptionOperations");
        }
    }
}
