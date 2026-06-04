using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListItemPromotionState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PromotionExpiryHandling",
                table: "ListItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PromotionResolvedAt",
                table: "ListItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PromotionSuggestedExpiry",
                table: "ListItems",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListItems_ListId_Status_PromotionResolvedAt",
                table: "ListItems",
                columns: new[] { "ListId", "Status", "PromotionResolvedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ListItems_ListId_Status_PromotionResolvedAt",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "PromotionExpiryHandling",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "PromotionResolvedAt",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "PromotionSuggestedExpiry",
                table: "ListItems");
        }
    }
}
