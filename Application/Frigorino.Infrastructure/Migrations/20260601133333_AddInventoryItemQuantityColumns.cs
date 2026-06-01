using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryItemQuantityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "InventoryItems");

            migrationBuilder.AddColumn<int>(
                name: "QuantityUnit",
                table: "InventoryItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityValue",
                table: "InventoryItems",
                type: "numeric(12,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantityUnit",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "QuantityValue",
                table: "InventoryItems");

            migrationBuilder.AddColumn<string>(
                name: "Quantity",
                table: "InventoryItems",
                type: "text",
                nullable: true);
        }
    }
}
