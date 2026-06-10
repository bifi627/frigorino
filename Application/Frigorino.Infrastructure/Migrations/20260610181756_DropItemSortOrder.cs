using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropItemSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ListItems_ListId_Status_SortOrder",
                table: "ListItems");

            migrationBuilder.DropIndex(
                name: "IX_ListItems_SortOrder",
                table: "ListItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_InventoryId_SortOrder",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SortOrder",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "InventoryItems");

            // Contract phase: Rank was added nullable so the (now-removed) startup backfill could
            // fill pre-existing rows. Stage + prod are confirmed populated, so tighten the column to
            // NOT NULL to match the EF model (which already maps Rank required). Hand-written — the
            // snapshot already records Rank as required, so `migrations add` won't scaffold this.
            // Type/collation are restated so the AlterColumn doesn't drop the "C" ordinal collation.
            migrationBuilder.AlterColumn<string>(
                name: "Rank",
                table: "ListItems",
                type: "text",
                nullable: false,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "C",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Rank",
                table: "InventoryItems",
                type: "text",
                nullable: false,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "C",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the NOT NULL tightening back to the expand-phase nullable column.
            migrationBuilder.AlterColumn<string>(
                name: "Rank",
                table: "ListItems",
                type: "text",
                nullable: true,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "C");

            migrationBuilder.AlterColumn<string>(
                name: "Rank",
                table: "InventoryItems",
                type: "text",
                nullable: true,
                collation: "C",
                oldClrType: typeof(string),
                oldType: "text",
                oldCollation: "C");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ListItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ListItems_ListId_Status_SortOrder",
                table: "ListItems",
                columns: new[] { "ListId", "Status", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ListItems_SortOrder",
                table: "ListItems",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_InventoryId_SortOrder",
                table: "InventoryItems",
                columns: new[] { "InventoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SortOrder",
                table: "InventoryItems",
                column: "SortOrder");
        }
    }
}
