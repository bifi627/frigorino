using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Inventory_System : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HouseholdId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventories_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Inventories_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "ExternalId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Quantity = table.Column<string>(type: "text", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Inventories_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "Inventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_CreatedAt",
                table: "Inventories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_CreatedByUserId",
                table: "Inventories",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_HouseholdId",
                table: "Inventories",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_HouseholdId_IsActive",
                table: "Inventories",
                columns: new[] { "HouseholdId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_IsActive",
                table: "Inventories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CreatedAt",
                table: "InventoryItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ExpiryDate",
                table: "InventoryItems",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ExpiryDate_IsActive",
                table: "InventoryItems",
                columns: new[] { "ExpiryDate", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId",
                table: "InventoryItems",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId_IsActive",
                table: "InventoryItems",
                columns: new[] { "InventoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId_SortOrder",
                table: "InventoryItems",
                columns: new[] { "InventoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_IsActive",
                table: "InventoryItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SortOrder",
                table: "InventoryItems",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Inventories");
        }
    }
}
