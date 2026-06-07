using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Added nullable (no default) so the one-time startup backfill — not a bogus "" — fills
            // existing rows. Tightened to NOT NULL in the deferred contract migration once populated.
            migrationBuilder.AddColumn<string>(
                name: "Rank",
                table: "ListItems",
                type: "text",
                nullable: true,
                collation: "C");

            migrationBuilder.AddColumn<string>(
                name: "Rank",
                table: "InventoryItems",
                type: "text",
                nullable: true,
                collation: "C");

            migrationBuilder.CreateIndex(
                name: "UX_ListItems_ListId_Status_Rank_Active",
                table: "ListItems",
                columns: new[] { "ListId", "Status", "Rank" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "UX_InventoryItems_InventoryId_Rank_Active",
                table: "InventoryItems",
                columns: new[] { "InventoryId", "Rank" },
                unique: true,
                filter: "\"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ListItems_ListId_Status_Rank_Active",
                table: "ListItems");

            migrationBuilder.DropIndex(
                name: "UX_InventoryItems_InventoryId_Rank_Active",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "InventoryItems");
        }
    }
}
