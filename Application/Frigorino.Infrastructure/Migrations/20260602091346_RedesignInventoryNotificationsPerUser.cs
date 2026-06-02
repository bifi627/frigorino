using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignInventoryNotificationsPerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiryLeadDays",
                table: "InventorySettings");

            migrationBuilder.DropColumn(
                name: "ExpiryNotificationsEnabled",
                table: "InventorySettings");

            migrationBuilder.RenameColumn(
                name: "HouseholdId",
                table: "NotificationDispatches",
                newName: "InventoryId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationDispatches_UserId_HouseholdId_SentOn",
                table: "NotificationDispatches",
                newName: "IX_NotificationDispatches_UserId_InventoryId_SentOn");

            migrationBuilder.CreateTable(
                name: "UserInventoryNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LeadDays = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInventoryNotificationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInventoryNotificationSettings_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserInventoryNotificationSettings_InventoryId",
                table: "UserInventoryNotificationSettings",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInventoryNotificationSettings_UserId_InventoryId",
                table: "UserInventoryNotificationSettings",
                columns: new[] { "UserId", "InventoryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserInventoryNotificationSettings");

            migrationBuilder.RenameColumn(
                name: "InventoryId",
                table: "NotificationDispatches",
                newName: "HouseholdId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationDispatches_UserId_InventoryId_SentOn",
                table: "NotificationDispatches",
                newName: "IX_NotificationDispatches_UserId_HouseholdId_SentOn");

            migrationBuilder.AddColumn<int>(
                name: "ExpiryLeadDays",
                table: "InventorySettings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExpiryNotificationsEnabled",
                table: "InventorySettings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }
    }
}
