using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodbookApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OperationType = table.Column<int>(type: "INTEGER", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SyncedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    BatchId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncQueue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsCloudSyncEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitialSyncCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    InitialSyncStartedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    InitialSyncCompletedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncAttemptUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TotalItemsSynced = table.Column<int>(type: "INTEGER", nullable: false),
                    PendingItemsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    LastKnownServerHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncStates_AuthAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "AuthAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_AccountId",
                table: "SyncQueue",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_AccountId_Entity",
                table: "SyncQueue",
                columns: new[] { "AccountId", "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_AccountId_Status",
                table: "SyncQueue",
                columns: new[] { "AccountId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_BatchId",
                table: "SyncQueue",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncQueue_Processing",
                table: "SyncQueue",
                columns: new[] { "Status", "Priority", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncStates_AccountId",
                table: "SyncStates",
                column: "AccountId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncQueue");

            migrationBuilder.DropTable(
                name: "SyncStates");
        }
    }
}
