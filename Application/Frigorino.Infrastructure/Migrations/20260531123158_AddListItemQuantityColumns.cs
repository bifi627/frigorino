using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListItemQuantityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "ListItems");

            migrationBuilder.AddColumn<int>(
                name: "QuantityUnit",
                table: "ListItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityValue",
                table: "ListItems",
                type: "numeric(12,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantityUnit",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "QuantityValue",
                table: "ListItems");

            migrationBuilder.AddColumn<string>(
                name: "Quantity",
                table: "ListItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
